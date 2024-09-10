using GistHub.GitHub;
using Xunit;

namespace GistHubTest;

public static class GitHubLinkTests
{
    [Fact]
    public static void TestParseLinkHeaderFirst()
    {
        const string link = "<https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=2>; rel=\"next\", <https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=4>; rel=\"last\"";
        var actual = GitHubClient.ParseLinkHeader(link);

        Assert.Equal(2, actual.Count);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=2", actual["next"]);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=4", actual["last"]);
    }

    [Fact]
    public static void TestParseLinkHeaderMiddle()
    {
        const string link = "<https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1>; rel=\"prev\", <https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=3>; rel=\"next\", <https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=4>; rel=\"last\", <https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1>; rel=\"first\"";
        var actual = GitHubClient.ParseLinkHeader(link);

        Assert.Equal(4, actual.Count);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1", actual["prev"]);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=3", actual["next"]);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=4", actual["last"]);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1", actual["first"]);
    }

    [Fact]
    public static void TestParseLinkHeaderEnd()
    {
        const string link = "<https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=3>; rel=\"prev\", <https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1>; rel=\"first\"";
        var actual = GitHubClient.ParseLinkHeader(link);

        Assert.Equal(2, actual.Count);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=3", actual["prev"]);
        Assert.Equal("https://api.github.com/user/8462645/gists?per_page=100%3Fpage%3D1&page=1", actual["first"]);
    }

    [Fact]
    public static void TestParseLinkInvalidEntries()
    {
        const string link = "<https://foo.com>; rel=\"prev\"; test=\"bar\",<https://bar.com>;rel=\"first\" , fake";
        var actual = GitHubClient.ParseLinkHeader(link);

        Assert.Single(actual);
        Assert.Equal("https://bar.com", actual["first"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public static void TestParseLinkWhitespace(string? link)
    {
        var actual = GitHubClient.ParseLinkHeader(link);
        Assert.Empty(actual);
    }
}
