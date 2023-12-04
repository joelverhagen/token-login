using Newtonsoft.Json;

namespace NuGet.TokenCredentialProvider;

class GitHubActionsV1TokenInfo : TokenInfo
{
    [JsonConstructor]
    public GitHubActionsV1TokenInfo(string type, string packageSource, string username, string runtimeToken, string tokenUrl)
        : base(type, packageSource)
    {
        Username = username;
        RuntimeToken = runtimeToken;
        TokenUrl = tokenUrl;
    }

    public string Username { get; }
    public string RuntimeToken { get; }
    public string TokenUrl { get; }
}