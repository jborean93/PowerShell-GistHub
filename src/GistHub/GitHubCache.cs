using System;
using System.Collections.Generic;
using GistHub.GitHub;

namespace GistHub;

internal sealed class UserGistCache
{
    public string UserName { get; }
    public DateTime? LastUpdate { get; set; }
    public Dictionary<string, Gist> Gists { get; } = new();

    public UserGistCache(
        string userName)
    {
        UserName = userName;
    }

    public void UpdateGistCache(Gist gist)
    {
        // Clear the content of each file to save memory.
        // Getting the content is always done on demand so we don't have to
        // save the data in memory from other operations.
        foreach (GistFile file in gist.Files.Values)
        {
            file.ClearContent();
        }
        Gists[gist.Id] = gist;
    }
}
