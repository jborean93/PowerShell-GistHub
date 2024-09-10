using System;

namespace GistHub.Shared;

/// <summary>
/// The GistInfo class represents a single Gist on GitHub. This is the output
/// of Get-Item/Get-ChildItem when outputting a gist elements.
/// </summary>
public sealed class GistInfo
{
    /// <summary>
    /// The unique identifier of the gist.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The path of the gist.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The description of the gist.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The public URL of the gist.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The git URL of the gist.
    /// </summary>
    public string GitUrl { get; }

    /// <summary>
    /// The files in the gist.
    /// </summary>
    public GistFile[] Files { get; }

    /// <summary>
    /// The date and time the gist was created as a UTC DateTime.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// The date and time the gist was last updated as a UTC DateTime.
    /// </summary>
    public DateTime UpdatedAt { get; }

    /// <summary>
    /// The owner/username of the gist.
    /// </summary>
    public string Owner { get; }

    /// <summary>
    /// Whether the gist is secret or not.
    /// </summary>
    public bool IsSecret { get; }

    internal string ProviderPath { get; }

    internal GistInfo(
        string id,
        string path,
        string? description,
        string url,
        string gitUrl,
        GistFile[] files,
        DateTime createdAt,
        DateTime updatedAt,
        string owner,
        bool isSecret,
        string providerPath)
    {
        Id = id;
        Path = path;
        Description = description;
        Url = url;
        GitUrl = gitUrl;
        Files = files;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Owner = owner;
        IsSecret = isSecret;
        ProviderPath = providerPath;
    }

    public override string ToString() => Path;
}

/// <summary>
/// The GistFile class represents a single file in a Gist on GistHub. This is
/// the output of Get-Item/Get-ChildItem when outputting a file in a gist.
/// </summary>
public sealed class GistFile
{
    /// <summary>
    /// The name of the file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The language of the file as reported by GitHub.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// The raw URL of the file.
    /// </summary>
    public string RawUrl { get; }

    /// <summary>
    /// The length of the file in bytes.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The GistInfo object that this file belongs to.
    /// </summary>
    public GistInfo Gist { get; }

    internal string ProviderPath => $"{Gist.ProviderPath}/{Name}";

    internal GistFile(
        string name,
        string path,
        string language,
        string rawUrl,
        int length,
        GistInfo gist)
    {
        Name = name;
        Path = path;
        Language = language;
        RawUrl = rawUrl;
        Length = length;
        Gist = gist;
    }

    public override string ToString() => Path;
}
