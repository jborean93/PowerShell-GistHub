using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace GistHub.GitHub;

internal sealed partial class GitHubClient
{
    private const string GitHubApiUrl = "https://api.github.com/";
    private const string GitHubApiVersion = "2022-11-28";
    private static string GistHubVersion = typeof(GitHubClient).Assembly.GetName().Version!.ToString();

    private readonly static HttpClient _client = new();

    static GitHubClient()
    {
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GistHub", GistHubVersion));
        _client.DefaultRequestHeaders.Add("X-GistHub-Version", GitHubApiVersion);
    }

    public static async Task<bool> TestGitHubUser(
        string username,
        BearerToken? token = null,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{GitHubApiUrl}users/{username}");
        HttpResponseMessage response = await SendMessageAsync(
            request,
            token,
            true,
            pipeline,
            cancellationToken).ConfigureAwait(false);
        return response.StatusCode == System.Net.HttpStatusCode.OK;
    }

    internal static Dictionary<string, string> ParseLinkHeader(string? linkHeader)
    {
        Dictionary<string, string> links = new();
        if (string.IsNullOrWhiteSpace(linkHeader))
        {
            return links;
        }

        string[] linkValues = linkHeader!.Split(',');
        foreach (string linkValue in linkValues)
        {
            string[] linkParts = linkValue.Split(';');
            if (linkParts.Length != 2)
            {
                continue;
            }

            linkParts[0] = linkParts[0].Trim();
            string url = linkParts[0].Substring(1, linkParts[0].Length - 2);

            linkParts[1] = linkParts[1].Trim();
            string rel = linkParts[1].Substring(5, linkParts[1].Length - 6);
            links[rel] = url;
        }

        return links;
    }

    private static async Task<T> SendMessageWithJsonResponseAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<T> jsonType,
        BearerToken? token,
        AsyncPipeline? pipeline,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendMessageAsync(
            request,
            token,
            false,
            pipeline,
            cancellationToken).ConfigureAwait(false);
        return await ReadResponseJsonAsync(
            response,
            jsonType,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendMessageAsync(
        HttpRequestMessage request,
        BearerToken? token,
        bool skipStatusCheck,
        AsyncPipeline? pipeline,
        CancellationToken cancellationToken)
    {
        if (token is not null)
        {
            // FUTURE: Deal with expiry and refresh.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        if (pipeline is not null)
        {
            await pipeline.WriteVerboseAsync($"Sending HTTP {request.Method} {request.RequestUri}").ConfigureAwait(false);
        }

        HttpResponseMessage response = await _client.SendAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (!skipStatusCheck)
        {
            response.EnsureSuccessStatusCode();
        }
        return response;
    }

    private static async Task<T> ReadResponseJsonAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> jsonType,
        CancellationToken cancellationToken)
    {
        Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        T? parsedResponse = await JsonSerializer.DeserializeAsync(responseStream, jsonType, cancellationToken);
        Debug.Assert(parsedResponse is not null);
        return parsedResponse!;
    }
}

internal sealed class BearerToken
{
    public string AccessToken { get; }
    public string? RefreshToken { get; }

    public BearerToken(
        string accessToken,
        string? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }
}
