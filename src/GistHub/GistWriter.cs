using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;
using GistHub.GitHub;

namespace GistHub;

internal sealed class GistWriter : IContentWriter
{
    private readonly BearerToken _bearerToken;
    private readonly string _delimiter;
    private readonly GistPath _gistPath;
    private readonly MemoryStream _stream;
    private readonly StreamWriter _writer;

    public GistWriter(
        BearerToken bearerToken,
        string? delimiter,
        GistPath gistPath,
        Stream stream)
    {
        _bearerToken = bearerToken;
        _delimiter = delimiter ?? Environment.NewLine;
        _gistPath = gistPath;

        // We need an expandable memory stream to write to.
        _stream = new MemoryStream();
        stream.CopyTo(_stream);
        _stream.Seek(0, SeekOrigin.Begin);

        _writer = new StreamWriter(_stream)
        {
            AutoFlush = true,
        };
    }

    public IList Write(IList content)
    {
        foreach (object item in content)
        {
            _writer.Write("{0}{1}", LanguagePrimitives.ConvertTo<string>(item), _delimiter);
        }

        return content;
    }

    public void Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public void Close()
    {
        string content = Encoding.UTF8.GetString(_stream.ToArray());
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                $"Cannot set a gist '{_gistPath.Path}' to an empty or whitespace only string.");
        }

        GitHubClient.UpdateGistAsync(
            _gistPath.GistId!,
            new UpdateGist(
                description: null,
                files: new Dictionary<string, UpdateGistFile?>
                {
                    {
                        _gistPath.FileName!,
                        new UpdateGistFile(fileName: null, content: content)
                    }
                }),
            token: _bearerToken).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _writer.Dispose();
    }
}
