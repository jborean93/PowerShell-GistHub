using System.Diagnostics;
using System.Management.Automation;

namespace GistHub;

internal sealed class GistGetChildDynamicParameters
{
    /// <summary>
    /// Adds -Credential support on Get-ChildItem.
    /// </summary>
    [Parameter()]
    [Credential()]
    public PSCredential Credential { get; set; } = PSCredential.Empty;
}

internal sealed class GistGetContentDynamicParameters
{
    /// <summary>
    /// Outputs the content as bytes rather than a string.
    /// </summary>
    [Parameter()]
    public SwitchParameter AsByteStream { get; set; }

    /// <summary>
    /// The delimiter to use when splitting the content string. Cannot be used
    /// with -Raw or -AsByteStream. If null or "", the content will be split on
    /// any combination of \r\n.
    /// </summary>
    [Parameter()]
    public string? Delimiter { get; set; }

    /// <summary>
    /// Outputs the content as a single string/byte rather than an array of
    /// values.
    /// </summary>
    [Parameter()]
    public SwitchParameter Raw { get; set; }
}

internal sealed class GistNewItemDynamicParameters
{
    /// <summary>
    /// The description of the gist. This is optional.
    /// </summary>
    [Parameter()]
    public string? Description { get; set; }

    /// <summary>
    /// Creates the gist as a public gist. This only works if the gist has not
    // already been created.
    /// </summary>
    [Parameter()]
    public SwitchParameter Public { get; set; }
}

internal sealed class GistSetContentDynamicParameters
{
    /// <summary>
    /// The delimiter to use when joining the input content array. Cannot be
    /// used with -AsByteStream. If unset/null, the content will be joined with
    /// Environment.NewLine.
    /// </summary>
    [Parameter()]
    public string? Delimiter { get; set; }
}
