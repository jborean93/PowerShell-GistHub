using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GistHub.GitHub;

internal sealed partial class GitHubClient
{
    public static async Task<Gist> CreateGistAsync(
        CreateGist create,
        BearerToken token,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        JsonSerializerOptions options = GetNullIgnoreOptions();
        HttpRequestMessage request = new(HttpMethod.Post, $"{GitHubApiUrl}gists")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(create, options),
                Encoding.UTF8)
        };

#if NET60_OR_GREATER
        return await SendMessageWithJsonResponseAsync(
            request,
            GitHubJsonContext.Default.Gist,
            token,
            pipeline,
            cancellationToken).ConfigureAwait(false);
#else
        return await SendMessageWithJsonResponseAsync<Gist>(
            request,
            token,
            pipeline,
            cancellationToken).ConfigureAwait(false);
#endif
    }

    public static async Task DeleteGistAsync(
        string gistId,
        BearerToken token,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"{GitHubApiUrl}gists/{gistId}");
        await SendMessageAsync(request, token, false, pipeline, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Gist?> GetGistAsync(
        string gistId,
        bool readAsBase64 = false,
        BearerToken? token = null,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{GitHubApiUrl}gists/{gistId}");

        string contentType = readAsBase64 ? "base64" : "raw";
        request.Headers.Accept.Add(new($"application/vnd.github.{contentType}+json"));

        HttpResponseMessage response = await SendMessageAsync(
            request,
            token,
            true,
            pipeline,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

#if NET60_OR_GREATER
        return await ReadResponseJsonAsync(
            response,
            GitHubJsonContext.Default.Gist,
            cancellationToken).ConfigureAwait(false);
#else
        return await ReadResponseJsonAsync<Gist>(
            response,
            cancellationToken).ConfigureAwait(false);
#endif
    }

    public static async Task<Stream> GetGistFileStreamAsync(
        string gistUrl,
        BearerToken? token = null,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, gistUrl);
        request.Headers.Accept.Add(new("application/octet-stream"));

        HttpResponseMessage response = await SendMessageAsync(
            request,
            token,
            true,
            pipeline,
            cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    }

    public static async IAsyncEnumerable<Gist[]> GetGistsforUserAsync(
        string username,
        DateTime? since = null,
        BearerToken? token = null,
        AsyncPipeline? pipeline = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string gistUri = $"{GitHubApiUrl}users/{username}/gists?per_page=100?page=1";
        if (since is not null)
        {
            gistUri += $"&since={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
        }

        while (true)
        {
            HttpRequestMessage request = new(HttpMethod.Get, gistUri);
            HttpResponseMessage response = await SendMessageAsync(
                request,
                token,
                false,
                pipeline,
                cancellationToken).ConfigureAwait(false);

#if NET60_OR_GREATER
            Gist[] gists = await ReadResponseJsonAsync(
                response,
                GitHubJsonContext.Default.GistArray,
                cancellationToken).ConfigureAwait(false);
#else
            Gist[] gists = await ReadResponseJsonAsync<Gist[]>(
                response,
                cancellationToken).ConfigureAwait(false);
#endif
            yield return gists;

            if (
                !response.Headers.TryGetValues("Link", out IEnumerable<string>? linkHeaders) ||
                !ParseLinkHeader(linkHeaders.FirstOrDefault()).TryGetValue("next", out string? nextLink))
            {
                break;
            }

            gistUri = nextLink;
        }
    }

    public static async Task<Gist> UpdateGistAsync(
        string gistId,
        UpdateGist update,
        BearerToken token,
        bool responseAsBase64Content = false,
        AsyncPipeline? pipeline = null,
        CancellationToken cancellationToken = default)
    {
        JsonSerializerOptions options = GetNullIgnoreOptions();
        HttpRequestMessage request = new(new HttpMethod("PATCH"), $"{GitHubApiUrl}gists/{gistId}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(update, options),
                Encoding.UTF8,
                $"application/vnd.github.raw+json")
        };

        string contentType = responseAsBase64Content ? "base64" : "raw";
        request.Headers.Accept.Add(new($"application/vnd.github.{contentType}+json"));

#if NET60_OR_GREATER
        return await SendMessageWithJsonResponseAsync(
            request,
            GitHubJsonContext.Default.Gist,
            token,
            pipeline,
            cancellationToken).ConfigureAwait(false);
#else
        return await SendMessageWithJsonResponseAsync<Gist>(
            request,
            token,
            pipeline,
            cancellationToken).ConfigureAwait(false);
#endif
    }

    private static JsonSerializerOptions GetNullIgnoreOptions()
    {
        return new()
        {
#if NET60_OR_GREATER
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
#else
#pragma warning disable SYSLIB0020 // net472 uses older lib with only this prop
            IgnoreNullValues = true
#pragma warning restore SYSLIB0020
#endif
        };
    }
}

// https://docs.github.com/en/rest/gists/gists?apiVersion=2022-11-28#get-a-gist
// https://docs.github.com/en/rest/gists/gists?apiVersion=2022-11-28#list-gists-for-a-user
internal sealed class Gist
{
    [JsonPropertyName("url")]
    public string Url { get; }

    [JsonPropertyName("forks_url")]
    public string ForksUrl { get; }

    [JsonPropertyName("commits_url")]
    public string CommitsUrl { get; }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; }

    [JsonPropertyName("git_pull_url")]
    public string GitPullUrl { get; }

    [JsonPropertyName("git_push_url")]
    public string GitPushUrl { get; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; }

    [JsonPropertyName("files")]
    public Dictionary<string, GistFile> Files { get; }

    [JsonPropertyName("public")]
    public bool Public { get; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; }

    [JsonPropertyName("description")]
    public string? Description { get; }

    [JsonPropertyName("comments")]
    public int Comments { get; }

    // [JsonPropertyName("user")]

    [JsonPropertyName("comments_url")]
    public string CommentsUrl { get; }

    [JsonPropertyName("owner")]
    public GistOwner Owner { get; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; }

    public Gist(
        string url,
        string forksUrl,
        string commitsUrl,
        string id,
        string nodeId,
        string gitPullUrl,
        string gitPushUrl,
        string htmlUrl,
        Dictionary<string, GistFile> files,
        bool @public,
        DateTime createdAt,
        DateTime updatedAt,
        string? description,
        int comments,
        string commentsUrl,
        GistOwner owner,
        bool truncated)
    {
        Url = url;
        ForksUrl = forksUrl;
        CommitsUrl = commitsUrl;
        Id = id;
        NodeId = nodeId;
        GitPullUrl = gitPullUrl;
        GitPushUrl = gitPushUrl;
        HtmlUrl = htmlUrl;
        Files = files;
        Public = @public;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Description = description;
        Comments = comments;
        CommentsUrl = commentsUrl;
        Owner = owner;
        Truncated = truncated;
    }
}

internal sealed class GistFile
{
    [JsonPropertyName("filename")]
    public string FileName { get; }

    [JsonPropertyName("type")]
    public string Type { get; }

    [JsonPropertyName("language")]
    public string Language { get; }

    [JsonPropertyName("raw_url")]
    public string RawUrl { get; }

    [JsonPropertyName("size")]
    public int Size { get; }

    // These are only present with gists/$GIST_ID
    [JsonPropertyName("truncated")]
    public bool Truncated { get; }

    [JsonPropertyName("content")]
    public string? Content { get; private set; }

    public GistFile(
        string fileName,
        string type,
        string language,
        string rawUrl,
        int size,
        bool truncated = false,
        string? content = null)
    {
        FileName = fileName;
        Type = type;
        Language = language;
        RawUrl = rawUrl;
        Size = size;
        Truncated = truncated;
        Content = content;
    }

    internal void ClearContent()
        => Content = null;
}

internal sealed class GistOwner
{
    [JsonPropertyName("login")]
    public string Login { get; }

    [JsonPropertyName("id")]
    public int Id { get; }

    public GistOwner(
        string login,
        int id)
    {
        Login = login;
        Id = id;
    }
}

internal sealed class CreateGist
{
    [JsonPropertyName("description")]
    public string? Description { get; }

    [JsonPropertyName("files")]
    public Dictionary<string, CreateGistFile> Files { get; }

    [JsonPropertyName("public")]
    public bool Public { get; }

    public CreateGist(
        string? description,
        Dictionary<string, CreateGistFile> files,
        bool @public)
    {
        Description = description;
        Files = files;
        Public = @public;
    }
}

internal sealed class CreateGistFile
{
    [JsonPropertyName("content")]
    public string Content { get; }

    public CreateGistFile(string content)
    {
        Content = content;
    }
}

internal sealed class UpdateGist
{
    [JsonPropertyName("description")]
    public string? Description { get; }

    [JsonPropertyName("files")]
    public Dictionary<string, UpdateGistFile?>? Files { get; }

    public UpdateGist(
        string? description,
        Dictionary<string, UpdateGistFile?>? files)
    {
        Description = description;
        Files = files;
    }
}

internal sealed class UpdateGistFile
{
    [JsonPropertyName("filename")]
    public string? FileName { get; }

    [JsonPropertyName("content")]
    public string? Content { get; }

    public UpdateGistFile(
        string? fileName,
        string? content)
    {
        FileName = fileName;
        Content = content;
    }
}
