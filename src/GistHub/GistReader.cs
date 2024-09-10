using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Provider;
using System.Text;

namespace GistHub;

/// <summary>
/// The GistReader class is a content reader for reading the content of a
/// Gist file from a stream.
/// </summary>
internal sealed class GistReader : IContentReader
{
    private readonly Stream _stream;
    private readonly string? _delimiter;
    private readonly bool _raw;
    private readonly StreamReader? _textReader;

    public GistReader(Stream stream, string? delimiter, bool isText, bool raw)
    {
        _stream = stream;
        _delimiter = delimiter;
        _raw = raw;

        if (isText)
        {
            _textReader = new(stream, Encoding.UTF8, false);
        }
    }

    public IList Read(long readCount)
    {
        if (_stream.Position >= _stream.Length)
        {
            return Array.Empty<object>();
        }

        return _textReader is null ? ReadBytes(readCount) : ReadText(readCount, _textReader);
    }

    private IList ReadBytes(long readCount)
    {
        long position = _stream.Position;
        long length = _stream.Length;
        long remaining = length - position;

        byte[] contentData;
        IList returnContent;
        if (_raw)
        {
            // If -Raw was set, we return the rest of the data as a single byte
            // array. This is wrapped in an array itself so it's returned as a
            // single output elemtent.
            contentData = new byte[remaining];
            returnContent = new[] { contentData };
        }
        else
        {
            // If readCount is 0 or less we read the entire content, otherwise
            // we read the minimum of readCount or the remaining content.
            int toRead = (int)(readCount < 1
                ? remaining
                : Math.Min(readCount, remaining));
            contentData = new byte[toRead];
            returnContent = contentData;
        }

        int read = 0;
        while (read < contentData.Length)
        {
            read += _stream.Read(contentData, read, contentData.Length - read);
        }

        return returnContent;
    }

    private IList ReadText(long readCount, StreamReader reader)
    {
        if (_raw)
        {
            // If -Raw was set, we return the rest of the data as a single
            // string.
            return new[] { reader.ReadToEnd() };
        }

#if NET472
        // .NET Framework StreamReader cannot read into a Span<char> directly,
        // this uses a temp array buffer that is rented from the poolinstead.
        char[] rawBuffer = ArrayPool<char>.Shared.Rent(256);
        try
        {
            Span<char> buffer = rawBuffer.AsSpan(0, 256);
            return ReadTextLinesWithDelimiter(reader, readCount, _delimiter?.ToCharArray(), buffer, rawBuffer);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rawBuffer);
        }
#else
        Span<char> buffer = stackalloc char[256];
        return ReadTextLinesWithDelimiter(reader, readCount, _delimiter?.ToCharArray(), buffer);
#endif
    }

    private List<string> ReadTextLinesWithDelimiter(
        StreamReader reader,
        long readCount,
        char[]? delimiter,
        Span<char> buffer
#if NET472
        , char[] rawBuffer
#endif
        )
    {
        // Set the capacity to the readCount if it's greater than 0, otherwise
        // use 1024 as a sane default.
        int capacity = readCount > 0 ? (int)readCount : 1024;
        List<string> lines = new(capacity);
        StringBuilder lineBuffer = new(buffer.Length);
        int readerRemaining = (int)_stream.Length;

        while (readCount < 1 || lines.Count < readCount)
        {
#if NET472
            int read = reader.Read(rawBuffer, 0, buffer.Length);
#else
            int read = reader.Read(buffer);
#endif
            if (read == 0)
            {
                break;
            }
            readerRemaining -= read;

            ReadOnlySpan<char> contentSpan;
            if (lineBuffer.Length > 0)
            {
                // If we have a partial line from the previous read we need to
                // include it in the search in case the delimiter is split at
                // the buffer boundary.
                lineBuffer.Append(buffer.Slice(0, read).ToString());
                contentSpan = lineBuffer.ToString().AsSpan();
                lineBuffer.Clear();
            }
            else
            {
                contentSpan = buffer.Slice(0, read);
            }

            while (contentSpan.Length > 0)
            {
                if (readCount > 0 && lines.Count == readCount)
                {
                    Seek(-contentSpan.Length, SeekOrigin.Current);
                    break;
                }

                int delimiterIndex = -1;
                int delimiterLength;
                if (delimiter is null || delimiter.Length == 0)
                {
                    // This is complicated we need to find \r, \n, or \r\n.
                    // Checking for \r is comlicated due to the buffer
                    // boundaries and making sure \n doesn't come after.
                    delimiterLength = 0;

                    int crIndex = contentSpan.IndexOf('\r');
                    int nlIndex = contentSpan.IndexOf('\n');
                    if (nlIndex != -1 && (crIndex == -1 || nlIndex < crIndex))
                    {
                        // Found \n first.
                        delimiterIndex = nlIndex;
                        delimiterLength = 1;
                    }
                    else if (crIndex != -1)
                    {
                        if (nlIndex == crIndex + 1)
                        {
                            // Found \r\n
                            delimiterIndex = crIndex;
                            delimiterLength = 2;
                        }
                        else if (crIndex != contentSpan.Length - 1 ||
                            readerRemaining == 0)
                        {
                            // Found \r without \n after.
                            delimiterIndex = crIndex;
                            delimiterLength = 1;
                        }
                        else
                        {
                            // Found \r at the end of the content but
                            // need to check the next block for \n.
                            delimiterIndex = -1;
                        }
                    }
                }
                else
                {
                    delimiterIndex = contentSpan.IndexOf(delimiter);
                    delimiterLength = delimiter.Length;
                }

                if (delimiterIndex == -1)
                {
                    lineBuffer.Append(contentSpan.ToString());
                    break;
                }
                else if (delimiterIndex == 0)
                {
                    lines.Add(string.Empty);
                    contentSpan = contentSpan.Slice(delimiterLength);
                    continue;
                }

                lines.Add(contentSpan.Slice(0, delimiterIndex).ToString());
                contentSpan = contentSpan.Slice(delimiterIndex + delimiterLength);
            }
        }

        if (lineBuffer.Length > 0)
        {
            lines.Add(lineBuffer.ToString());
        }

        return lines;
    }

    public void Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public void Close()
    { }

    public void Dispose()
    {
        _textReader?.Dispose();
        _stream.Dispose();
    }
}
