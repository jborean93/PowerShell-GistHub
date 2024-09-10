using System.Text.Json.Serialization;

namespace GistHub.GitHub;

[JsonSerializable(typeof(CreateGist))]
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(Gist[]))]
[JsonSerializable(typeof(GistFile))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(UpdateGist))]
internal partial class GitHubJsonContext : JsonSerializerContext
{ }
