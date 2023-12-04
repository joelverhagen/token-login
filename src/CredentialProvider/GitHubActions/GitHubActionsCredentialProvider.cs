using System.Diagnostics;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using NuGet.Common;
using static NuGet.TokenCredentialProvider.Constants;

namespace NuGet.TokenCredentialProvider;

class GitHubActionsCredentialProvider : ICredentialProvider
{
    private readonly PluginLogger _logger;

    public GitHubActionsCredentialProvider(PluginLogger logger)
    {
        _logger = logger;
    }

    public IEnumerable<string> GetValuesToRedact(string tokenInfoJson)
    {
        GitHubActionsV1TokenInfo? info = null;
        try
        {
            info = JsonConvert.DeserializeObject<GitHubActionsV1TokenInfo>(tokenInfoJson)!;
        }
        catch
        {
            // ignore
        }

        if (info != null)
        {
            yield return info.RuntimeToken;
        }
    }

    /// <summary>
    /// Implements the protocol defined by GitHub Actions:
    /// https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-cloud-providers#using-custom-actions
    /// </summary>
    public async Task<CredentialProviderResult> GetResponseOrNullAsync(string type, string tokenInfoJson)
    {
        if (type != "GitHubActionsV1")
        {
            return CredentialProviderResult.NotSupported();
        }

        GitHubActionsV1TokenInfo? tokenInfo;
        try
        {
            tokenInfo = JsonConvert.DeserializeObject<GitHubActionsV1TokenInfo>(tokenInfoJson)!;
        }
        catch (JsonException ex)
        {
            return CredentialProviderResult.Error($"Failed to read GitHub Actions {TokenInfoEnv}. " + ex.Message);
        }

        if (!Uri.TryCreate(tokenInfo.TokenUrl, UriKind.Absolute, out var tokenUrl)
            || tokenUrl.Scheme != "https")
        {
            return CredentialProviderResult.Error($"The token URL '{tokenInfo.TokenUrl}' in {TokenInfoEnv} is not a valid HTTPS URL.");
        }

        _logger.LogJwtClaims(label: "GitHub Actions runtime token", tokenInfo.RuntimeToken!);

        _logger.Log(LogLevel.Debug, $"Using username '{tokenInfo.Username}' from {TokenInfoEnv}.");

        // This audience value must be a format that the package source expects.
        // Most importantly, the package source must be able to extract the username from this value so it can
        // match the token claims to the user that has trusted the specific token pattern. 
        var audience = $"{tokenInfo.Username}@{new Uri(tokenInfo.PackageSource).Host}";
        
        var audienceQuery = $"audience={Uri.EscapeDataString(audience)}";
        var tokenUrlBuilder = new UriBuilder(tokenUrl);
        if (string.IsNullOrEmpty(tokenUrlBuilder.Query))
        {
            tokenUrlBuilder.Query = audienceQuery;
        }
        else
        {
            tokenUrlBuilder.Query += "&" + audienceQuery;
        }

        GitHubActionsTokenResponse? tokenResponse;
        try
        {
            _logger.Log(LogLevel.Debug, $"Fetching a token from the token URL provided in {TokenInfoEnv} with audience '{audience}'.");
            var sw = Stopwatch.StartNew();
            using var httpClient = new HttpClient();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, tokenUrlBuilder.Uri);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenInfo.RuntimeToken);
            requestMessage.Headers.TryAddWithoutValidation("Accept", "application/json; api-version=2.0");
            using var responseMessage = await httpClient.SendAsync(requestMessage);
            _logger.Log(LogLevel.Debug, $"Token URL returned HTTP {(int)responseMessage.StatusCode} after {sw.ElapsedMilliseconds}ms.");
            responseMessage.EnsureSuccessStatusCode();
            var tokenResponseJson = await responseMessage.Content.ReadAsStringAsync();
            tokenResponse = JsonConvert.DeserializeObject<GitHubActionsTokenResponse>(tokenResponseJson);
            if (string.IsNullOrEmpty(tokenResponse?.Value))
            {
                return CredentialProviderResult.Error("No token value was found in the token URL response.");
            }
        }
        catch (Exception ex)
        {
            return CredentialProviderResult.Error($"Failed to fetch token from '{tokenInfo.TokenUrl}'. " + ex.Message);
        }

        return CredentialProviderResult.JWT(tokenResponse.Value, $"Successfully fetched a GitHub Actions token for {audience}.");
    }
}
