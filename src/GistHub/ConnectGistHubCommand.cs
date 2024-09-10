using GistHub.GitHub;
using System;
using System.Management.Automation;
using System.Net;
using System.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace GistHub;

[Cmdlet(VerbsCommunications.Connect, "GistHub", DefaultParameterSetName = "AccessToken")]
public sealed class ConnectGistHubCommand : PSCmdlet
{
    private CancellationTokenSource? _cancellationTokenSource;

    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = "AccessToken")]
    [StringAsSecureStringTransformer]
    public SecureString AccessToken { get; set; } = new();

    [Parameter(Mandatory = true, ParameterSetName = "OAuthDeviceCode")]
    public SwitchParameter OAuthDeviceCode { get; set; }

    protected override void EndProcessing()
    {
        BearerToken? token = OAuthDeviceCode
            ? ConnectWithOAuthDeviceCode()
            : new BearerToken(new NetworkCredential(string.Empty, AccessToken).Password, null);
        if (token is not null)
        {
            RunspaceStorage.GetFromTLS().OAuthToken = token;
        }
    }

    private BearerToken? ConnectWithOAuthDeviceCode()
    {
        if (Host.UI is null)
        {
            InvalidOperationException exc = new("Cannot display GitHub OAuth device code without $host.UI.");
            ErrorRecord err = new(
                exc,
                "NoHostForOAuthDeviceCodeFlow",
                ErrorCategory.InvalidOperation,
                null);
            ThrowTerminatingError(err);
            return null;
        }

        using (_cancellationTokenSource = new CancellationTokenSource())
        {
            try
            {
                return Task.Run(
                    () => GetToken(_cancellationTokenSource.Token),
                    _cancellationTokenSource.Token).GetAwaiter().GetResult();
            }
            catch (AuthenticationException ex)
            {
                ErrorRecord err = new(
                    ex,
                    "AuthenticationFailed",
                    ErrorCategory.AuthenticationError,
                    null);
                WriteError(err);
                return null;
            }
        }
    }

    private async Task<BearerToken> GetToken(CancellationToken cancellationToken)
    {
        var deviceCode = await GitHubClient.GetOAuthDeviceCodeAsync(
            cancellationToken).ConfigureAwait(false);

        Host.UI.WriteLine($"Please go to {deviceCode.VerificationUri} and enter the code: {deviceCode.UserCode}");

        return await GitHubClient.PollForOAuthTokenAsync(
            deviceCode.DeviceCode,
            deviceCode.Interval,
            cancellationToken).ConfigureAwait(false);
    }

    protected override void StopProcessing()
    {
        _cancellationTokenSource?.Cancel();
    }
}
