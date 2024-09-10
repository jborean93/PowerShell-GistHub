using System.IO;
using System.Text;
using GistHub;
using Xunit;

namespace GistHubTests;

public static class GistReaderTests
{
    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public static void ReadStringNoDelimiter(string newline)
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"Hello{newline}World")),
            null,
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);

        reader.Close();
    }

    [Fact]
    public static void ReadStringCrAtEndOfBuffer()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"Hello\nWorld\r")),
            null,
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);

        reader.Close();
    }

    [Fact]
    public static void ReadStringNoDelimiterBufferBoundary()
    {
        string first = new('a', 256);
        string second = new('b', 256);

        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"{first}\r\n{second}")),
            null,
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal(first, actual[0]);
        Assert.Equal(second, actual[1]);
    }

    [Fact]
    public static void ReadCrLnDelimiterAtBuffer()
    {
        string first = new('a', 255);
        string second = new('b', 255);

        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"{first}\r\n{second}")),
            null,
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal(first, actual[0]);
        Assert.Equal(second, actual[1]);
    }

    [Fact]
    public static void ReadStringRaw()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\r\nWorld")),
            null,
            true,
            true);

        var actual = reader.Read(0);
        Assert.Single(actual);
        Assert.Equal("Hello\r\nWorld", actual[0]);
    }

    [Fact]
    public static void ReadStringNotDelimiterMinusCount()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\r\nWorld")),
            null,
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);
    }

    [Fact]
    public static void ReadStringNotDelimiterWithCount()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\r\nWorld\nFoo\rBar")),
            null,
            true,
            false);

        var actual = reader.Read(2);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);

        actual = reader.Read(1);
        Assert.Single(actual);
        Assert.Equal("Foo", actual[0]);

        actual = reader.Read(2);
        Assert.Single(actual);
        Assert.Equal("Bar", actual[0]);

        Assert.Empty(reader.Read(1));
    }

    [Fact]
    public static void ReadStringSingleDelimiter()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\nWorld")),
            "\n",
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);
    }

    [Fact]
    public static void ReadStringSingleDelimiterWithCount()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\nWorld\nFoo\nBar")),
            "\n",
            true,
            false);

        var actual = reader.Read(2);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);

        actual = reader.Read(1);
        Assert.Single(actual);
        Assert.Equal("Foo", actual[0]);

        actual = reader.Read(2);
        Assert.Single(actual);
        Assert.Equal("Bar", actual[0]);

        Assert.Empty(reader.Read(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReadStringSingleDelimiterWithEmptyEntry(bool addEndNewline)
    {
        string endNewline = addEndNewline ? "\n" : "";
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"\nHello\nWorld\n\nFoo\nBar{endNewline}")),
            "\n",
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(6, actual.Count);
        Assert.Equal("", actual[0]);
        Assert.Equal("Hello", actual[1]);
        Assert.Equal("World", actual[2]);
        Assert.Equal("", actual[3]);
        Assert.Equal("Foo", actual[4]);
        Assert.Equal("Bar", actual[5]);
    }

    [Fact]
    public static void ReadStringMultiDelimiter()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\r\nWorld")),
            "\r\n",
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);
    }

    [Fact]
    public static void ReadStringMultiDelimiterWithCount()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes("Hello\r\nWorld\r\nFoo\r\nBar")),
            "\r\n",
            true,
            false);

        var actual = reader.Read(2);
        Assert.Equal(2, actual.Count);
        Assert.Equal("Hello", actual[0]);
        Assert.Equal("World", actual[1]);

        actual = reader.Read(1);
        Assert.Single(actual);
        Assert.Equal("Foo", actual[0]);

        actual = reader.Read(2);
        Assert.Single(actual);
        Assert.Equal("Bar", actual[0]);

        Assert.Empty(reader.Read(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReadStringMultiDelimiterWithEmptyEntry(bool addEndNewline)
    {
        string endNewline = addEndNewline ? "\r\n" : "";
        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"\r\nHello\r\nWorld\r\n\r\nFoo\r\nBar{endNewline}")),
            "\r\n",
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(6, actual.Count);
        Assert.Equal("", actual[0]);
        Assert.Equal("Hello", actual[1]);
        Assert.Equal("World", actual[2]);
        Assert.Equal("", actual[3]);
        Assert.Equal("Foo", actual[4]);
        Assert.Equal("Bar", actual[5]);
    }

    [Fact]
    public static void ReadMultiDelimiterAtBuffer()
    {
        string first = new('a', 255);
        string second = new('b', 255);

        using GistReader reader = new GistReader(
            new MemoryStream(Encoding.UTF8.GetBytes($"{first}\r\n{second}")),
            "\r\n",
            true,
            false);

        var actual = reader.Read(0);
        Assert.Equal(2, actual.Count);
        Assert.Equal(first, actual[0]);
        Assert.Equal(second, actual[1]);
    }

    [Fact]
    public static void ReadByteRaw()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(new byte[] { 1, 2, 3, 4 }),
            null,
            false,
            true);

        var actual = reader.Read(0);
        Assert.Single(actual);
        byte[] dataActual = Assert.IsType<byte[]>(actual[0]);

        Assert.Equal(4, dataActual.Length);
        Assert.Equal(1, dataActual[0]);
        Assert.Equal(2, dataActual[1]);
        Assert.Equal(3, dataActual[2]);
        Assert.Equal(4, dataActual[3]);

        reader.Close();
    }

    [Fact]
    public static void ReadByteAll()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(new byte[] { 1, 2, 3, 4 }),
            null,
            false,
            false);

        var actual = reader.Read(0);
        Assert.Equal(4, actual.Count);
        var dataActual = Assert.IsType<byte>(actual[0]);
        Assert.Equal(1, dataActual);

        dataActual = Assert.IsType<byte>(actual[1]);
        Assert.Equal(2, dataActual);

        dataActual = Assert.IsType<byte>(actual[2]);
        Assert.Equal(3, dataActual);

        dataActual = Assert.IsType<byte>(actual[3]);
        Assert.Equal(4, dataActual);
    }

    [Fact]
    public static void ReadByteSingle()
    {
        using GistReader reader = new GistReader(
            new MemoryStream(new byte[] { 1, 2, 3, 4 }),
            null,
            false,
            false);

        var actual = reader.Read(1);
        Assert.Single(actual);
        byte dataActual = Assert.IsType<byte>(actual[0]);
        Assert.Equal(1, dataActual);

        actual = reader.Read(2);
        Assert.Equal(2, actual.Count);
        dataActual = Assert.IsType<byte>(actual[0]);
        Assert.Equal(2, dataActual);
        dataActual = Assert.IsType<byte>(actual[1]);
        Assert.Equal(3, dataActual);

        actual = reader.Read(2);
        Assert.Single(actual);
        dataActual = Assert.IsType<byte>(actual[0]);
        Assert.Equal(4, dataActual);
    }
}
