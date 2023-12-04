
namespace NuGet.TokenCredentialProvider
{
    internal interface ICredentialProvider
    {
        IEnumerable<string> GetValuesToRedact(string tokenInfoJson);
        Task<CredentialProviderResult> GetResponseOrNullAsync(string type, string tokenInfoJson);
    }
}