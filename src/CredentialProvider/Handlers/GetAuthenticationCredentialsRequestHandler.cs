using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol.Plugins;
using static NuGet.TokenCredentialProvider.Constants;

namespace NuGet.TokenCredentialProvider;

class GetAuthenticationCredentialsRequestHandler : RequestHandlerBase<GetAuthenticationCredentialsRequest, GetAuthenticationCredentialsResponse>
{
    private readonly IReadOnlyList<ICredentialProvider> _providers;

    public GetAuthenticationCredentialsRequestHandler(PluginLogger logger) : base(logger)
    {
        _providers = new List<ICredentialProvider>
        {
            new GitHubActionsCredentialProvider(logger),
        };
    }

    public override async Task<GetAuthenticationCredentialsResponse> HandleRequestAsync(GetAuthenticationCredentialsRequest request, CancellationToken cancellationToken)
    {
        _logger.Log(LogLevel.Debug, "ENV: " + _logger.SerializeForLogging(Environment.GetEnvironmentVariables()));

        var result = await GetCredentialAsync(request);
        switch (result.Type)
        {
            case CredentialProviderResultType.NotSupported:
                return new GetAuthenticationCredentialsResponse(
                    username: null,
                    password: null,
                    message: null,
                    authenticationTypes: Array.Empty<string>(),
                    MessageResponseCode.NotFound);
            case CredentialProviderResultType.Error:
                return new GetAuthenticationCredentialsResponse(
                    username: null,
                    password: null,
                    message: result.ErrorMessage,
                    authenticationTypes: Array.Empty<string>(),
                    MessageResponseCode.Error);
            case CredentialProviderResultType.JWT:
                return new GetAuthenticationCredentialsResponse(
                    username: "PasswordIsToken",
                    password: result.Token,
                    message: null,
                    authenticationTypes: new List<string> { "Basic" },
                    MessageResponseCode.Success);
            default:
                throw new NotImplementedException($"Unsupported result type: {result.Type}.");
        }
    }

    private async Task<CredentialProviderResult> GetCredentialAsync(GetAuthenticationCredentialsRequest request)
    {
        _logger.Log(LogLevel.Debug, $"Beginning authentication credential request for package source '{request.Uri.AbsoluteUri}'.");

        var tokenInfoJson = Environment.GetEnvironmentVariable(TokenInfoEnv);
        if (string.IsNullOrWhiteSpace(tokenInfoJson))
        {
            _logger.Log(LogLevel.Warning, $"The {TokenInfoEnv} environment variable is not set. The NuGet TokenCredentialProvider will not be used.");
            return CredentialProviderResult.NotSupported();
        }

        foreach (var provider in _providers)
        {
            foreach (var value in provider.GetValuesToRedact(tokenInfoJson))
            {
                _logger.AddRedactedValue(value);
            }
        }

        _logger.Log(LogLevel.Debug, $"Found {TokenInfoEnv} environment variable: {tokenInfoJson}");

        TokenInfo? tokenInfo;
        try
        {
            tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(tokenInfoJson);
            if (tokenInfo is null)
            {
                return CredentialProviderResult.Error($"The {TokenInfoEnv} environment variable is null.");
            }
        }
        catch (JsonException ex)
        {
            return CredentialProviderResult.Error($"The {TokenInfoEnv} environment variable could not be deserialized. " + ex.Message);
        }

        if (tokenInfo.PackageSource != request.Uri.AbsoluteUri)
        {
            _logger.Log(
                LogLevel.Warning,
                $"The package source '{tokenInfo.PackageSource}' in {TokenInfoEnv} does not match " +
                $"'{request.Uri.AbsoluteUri}' in the credential request.");
            return CredentialProviderResult.NotSupported();
        }

        _logger.Log(LogLevel.Debug, $"Found a matching package source in {TokenInfoEnv}.");

        foreach (var provider in _providers)
        {
            var providerName = provider.GetType().Name;
            _logger.Log(LogLevel.Debug, $"Starting {providerName}.");
            var result = await provider.GetResponseOrNullAsync(tokenInfo.Type, tokenInfoJson);
            switch (result.Type)
            {
                case CredentialProviderResultType.NotSupported:
                    _logger.Log(LogLevel.Debug, $"The '{tokenInfo.Type}' type in the {TokenInfoEnv} is not supported by {providerName}.");
                    break;
                case CredentialProviderResultType.Error:
                    _logger.Log(LogLevel.Error, result.ErrorMessage!);
                    return result;
                case CredentialProviderResultType.JWT:
                    _logger.AddRedactedValue(result.Token!);
                    _logger.LogJwtClaims(label: "credential", result.Token!);
                    _logger.Log(LogLevel.Minimal, result.SuccessMessage!);
                    return result;
                default:
                    return result;
            }
        }

        _logger.Log(LogLevel.Warning, $"The '{tokenInfo.Type}' type in the {TokenInfoEnv} is not recognized type.");
        return CredentialProviderResult.NotSupported();
    }
}
