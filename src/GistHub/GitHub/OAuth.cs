using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GistHub.GitHub;

internal sealed partial class GitHubClient
{
    private const string GitHubClientId = "Iv23liqX6f3ynRFszwKd";
    private const string GitHubDeviceCodeUrl = "https://github.com/login/device/code";
    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GitHubDeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    public static async Task<DeviceCodeResponse> GetOAuthDeviceCodeAsync(
        CancellationToken cancellationToken = default)
    {
        FormUrlEncodedContent content = new(new[]
        {
            new KeyValuePair<string, string>("client_id", GitHubClientId),
            new KeyValuePair<string, string>("scope", "gist")
        });
        HttpRequestMessage request = new(HttpMethod.Post, GitHubDeviceCodeUrl)
        {
            Content = content
        };

#if NET60_OR_GREATER
        return await SendMessageWithJsonResponseAsync(
            request,
            GitHubJsonContext.Default.DeviceCodeResponse,
            null,
            null,
            cancellationToken).ConfigureAwait(false);
#else
        return await SendMessageWithJsonResponseAsync<DeviceCodeResponse>(
            request,
            null,
            null,
            cancellationToken).ConfigureAwait(false);
#endif
    }

    public static async Task<BearerToken> PollForOAuthTokenAsync(
        string deviceCode,
        int interval,
        CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", GitHubClientId),
            new KeyValuePair<string, string>("device_code", deviceCode),
            new KeyValuePair<string, string>("grant_type", GitHubDeviceCodeGrantType)
        });

        while (true)
        {
            HttpRequestMessage message = new(HttpMethod.Post, GitHubTokenUrl)
            {
                Content = content
            };
#if NET60_OR_GREATER
            TokenResponse tokenResponse = await SendMessageWithJsonResponseAsync(
                message,
                GitHubJsonContext.Default.TokenResponse,
                null,
                null,
                cancellationToken).ConfigureAwait(false);
#else
            TokenResponse tokenResponse = await SendMessageWithJsonResponseAsync<TokenResponse>(
                message,
                null,
                null,
                cancellationToken).ConfigureAwait(false);
#endif

            if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return new BearerToken(
                    tokenResponse.AccessToken,
                    tokenResponse.RefreshToken);
            }
            else if (tokenResponse.Error == "authorization_pending")
            {
                // Continue polling after waiting for the interval time
                await Task.Delay(interval * 1000, cancellationToken).ConfigureAwait(false);
            }
            else if (tokenResponse.Error == "slow_down")
            {
                // GitHub suggests slowing down the polling
                interval += 5;
                await Task.Delay(interval * 1000, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                StringBuilder errorMsg = new();
                errorMsg.Append($"Error when polling for OAuth response ({tokenResponse.Error ?? "Unknown"})");
                if (!string.IsNullOrEmpty(tokenResponse.ErrorDescription))
                {
                    errorMsg.Append($": {tokenResponse.ErrorDescription}");
                }
                if (!string.IsNullOrWhiteSpace(tokenResponse.ErrorUri))
                {
                    errorMsg.Append($" {tokenResponse.ErrorUri}");
                }
                throw new AuthenticationException(errorMsg.ToString());
            }
        }
    }
}

internal sealed class DeviceCodeResponse
{
    // https://datatracker.ietf.org/doc/html/rfc8628#section-3.2
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; }

    [JsonPropertyName("user_code")]
    public string UserCode { get; }

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; }

    [JsonPropertyName("verification_uri_complete")]
    public string? VerificationUriComplete { get; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; }

    [JsonPropertyName("interval")]
    public int Interval { get; }

    [JsonIgnore]
    public DateTime CreationTime { get; } = DateTime.Now;

    public DeviceCodeResponse(
        string deviceCode,
        string userCode,
        string verificationUri,
        string? verificationUriComplete,
        int expiresIn,
        int interval)
    {
        DeviceCode = deviceCode;
        UserCode = userCode;
        VerificationUri = verificationUri;
        VerificationUriComplete = verificationUriComplete;
        ExpiresIn = expiresIn;
        Interval = interval;
    }
}

internal sealed class TokenResponse
{
    // Success properties
    // https://datatracker.ietf.org/doc/html/rfc6749#section-5.1
    [JsonPropertyName("access_token")]
    public string AccessToken { get; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; }

    [JsonPropertyName("scope")]
    public string? Scope { get; }

    // Error properties
    // https://datatracker.ietf.org/doc/html/rfc6749#section-5.2
    [JsonPropertyName("error")]
    public string Error { get; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; }

    [JsonIgnore]
    public DateTime CreationTime { get; } = DateTime.Now;

    public TokenResponse(
        string accessToken,
        string tokenType,
        int expiresIn,
        string? refreshToken,
        int refreshTokenExpiresIn,
        string? scope,
        string? error,
        string? errorDescription,
        string? errorUri)
    {
        AccessToken = accessToken;
        TokenType = tokenType;
        ExpiresIn = expiresIn;
        RefreshToken = refreshToken;
        RefreshTokenExpiresIn = refreshTokenExpiresIn;
        Scope = scope;
        Error = error ?? "Unknown";
        ErrorDescription = errorDescription;
        ErrorUri = errorUri;
    }
}
