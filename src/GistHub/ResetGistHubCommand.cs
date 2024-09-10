using System.Management.Automation;

namespace GistHub;

[Cmdlet(VerbsCommon.Reset, "GistHub", SupportsShouldProcess = true)]
public sealed class ResetGistHubCommand : PSCmdlet
{
    protected override void EndProcessing()
    {
        RunspaceStorage storage = RunspaceStorage.GetFromTLS();
        if (ShouldProcess("GistHub cache", "reset"))
        {
            storage.GistCache.Clear();
            storage.OAuthToken = null;
        }
    }
}
