using GistHub.GitHub;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace GistHubTests;

public static class JsonContextTests
{
    [Fact]
    public static void SerializeCreateGistNoDescription()
    {
        const string expected = "{\"files\":{\"file1.txt\":{\"content\":\"Content\"}},\"public\":true}";

        var createGist = new CreateGist(
            description: null,
            files: new Dictionary<string, CreateGistFile>()
            {
                { "file1.txt", new CreateGistFile(content: "Content") }
            },
            @public: true
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(createGist, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void SerializeCreateGistDescription()
    {
        const string expected = "{\"description\":\"Description\",\"files\":{\"file1.txt\":{\"content\":\"Content\"}},\"public\":true}";

        var createGist = new CreateGist(
            description: "Description",
            files: new Dictionary<string, CreateGistFile>()
            {
                { "file1.txt", new CreateGistFile(content: "Content") }
            },
            @public: true
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(createGist, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void SerializeUpdateGistFileContent()
    {
        const string expected = "{\"files\":{\"file1.txt\":{\"content\":\"Updated content\"}}}";

        var updateGist = new UpdateGist(
            description: null,
            files: new Dictionary<string, UpdateGistFile?>()
            {
                {
                    "file1.txt",
                    new UpdateGistFile(fileName: null, content: "Updated content")
                }

            }
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(updateGist, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void SerializeUpdateGistFileName()
    {
        const string expected = "{\"files\":{\"file1.txt\":{\"filename\":\"new-name.txt\"}}}";

        var updateGist = new UpdateGist(
            description: null,
            files: new Dictionary<string, UpdateGistFile?>()
            {
                {
                    "file1.txt",
                    new UpdateGistFile(fileName: "new-name.txt", content: null)
                }

            }
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(updateGist, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void SerializeUpdateGistDeleteFile()
    {
        const string expected = "{\"files\":{\"file1.txt\":null}}";

        var updateGist = new UpdateGist(
            description: null,
            files: new Dictionary<string, UpdateGistFile?>()
            {
                { "file1.txt", null }
            }
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(updateGist, options);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void SerializeUpdateGistDescription()
    {
        const string expected = "{\"description\":\"Updated description\"}";

        var updateGist = new UpdateGist(
            description: "Updated description",
            files: null
        );

        JsonSerializerOptions options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var actual = JsonSerializer.Serialize(updateGist, options);

        Assert.Equal(expected, actual);
    }
}
