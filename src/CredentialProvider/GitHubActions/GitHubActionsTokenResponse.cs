using Newtonsoft.Json;

namespace NuGet.TokenCredentialProvider;

class GitHubActionsTokenResponse
{
    [JsonConstructor]
    public GitHubActionsTokenResponse(string value)
    {
        Value = value;
    }

    public string Value { get; }
}
