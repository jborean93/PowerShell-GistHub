using System.Diagnostics;
using System.Text;

namespace GistHub;

[DebuggerDisplay("{Path}")]
internal sealed class GistPath
{
    public string? UserName { get; }
    public string? GistId { get; }
    public string? FileName { get; }
    public string Path { get; }
    public string NormalizedPath { get; }

    private GistPath(string? userName, string? gistId, string? fileName, string originalPath)
    {
        UserName = userName;
        GistId = gistId?.ToLowerInvariant();
        FileName = fileName;
        Path = originalPath;

        StringBuilder normalizedPath = new();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            normalizedPath.Append(UserName);
            if (!string.IsNullOrWhiteSpace(gistId))
            {
                normalizedPath.Append(System.IO.Path.DirectorySeparatorChar);
                normalizedPath.Append(GistId);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    normalizedPath.Append(System.IO.Path.DirectorySeparatorChar);
                    normalizedPath.Append(FileName);
                }
            }
        }
        NormalizedPath = normalizedPath.ToString();
    }

    public static GistPath Parse(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            // An empty path, this can happen with the 'Gist:/' provider path.
            return new(null, null, null, "");
        }

        string tempPath = path!;

        // We support separatoring the user and id with either a '/' or a '\'.
        // If we don't find either, then we assume the entire path is just
        // the username.
        int sepIndex = tempPath.IndexOfAny(new[] { '/', '\\' });
        if (sepIndex == -1)
        {
            return new(tempPath, null, null, path!);
        }

        string userName = tempPath.Substring(0, sepIndex);
        tempPath = tempPath.Substring(sepIndex + 1);

        // The next value is the gist id, we also support splitting by '/' or
        // '\\'. If not present this is the 'username/gistId'.
        sepIndex = tempPath.IndexOfAny(new[] { '/', '\\' });
        if (sepIndex == -1)
        {
            return new(userName, tempPath, null, path!);
        }

        string gistId = tempPath.Substring(0, sepIndex);
        // The next value is the filename, a gist can have '\' in the filename
        // but should not contain '/' in it. The provider API will automatically
        // convert '\' to '/' so we treat that as '\' if present.
        tempPath = tempPath.Substring(sepIndex + 1).Replace('/', '\\');

        return new(userName, gistId, tempPath, path!);
    }
}
