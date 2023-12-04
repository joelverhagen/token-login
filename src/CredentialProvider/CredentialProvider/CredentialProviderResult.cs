namespace NuGet.TokenCredentialProvider;

class CredentialProviderResult
{
    private CredentialProviderResult()
    {
    }

    public CredentialProviderResultType Type { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? SuccessMessage { get; private init; }
    public string? Token { get; private init; }

    public static CredentialProviderResult NotSupported()
    {
        return new CredentialProviderResult
        {
            Type = CredentialProviderResultType.NotSupported,
        };
    }

    public static CredentialProviderResult Error(string errorMessage)
    {
        return new CredentialProviderResult
        {
            Type = CredentialProviderResultType.Error,
            ErrorMessage = errorMessage,
        };
    }

    public static CredentialProviderResult JWT(string token, string successMessage)
    {
        return new CredentialProviderResult
        {
            Type = CredentialProviderResultType.JWT,
            Token = token,
            SuccessMessage = successMessage,
        };
    }
}
