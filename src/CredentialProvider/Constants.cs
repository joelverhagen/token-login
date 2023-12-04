namespace NuGet.TokenCredentialProvider;

static class Constants
{
    /// <summary>
    /// This environment variable must be provided to the NuGet client process. When NuGet starts the credential
    /// provider, it will use all of its own environment variables including this one.
    /// </summary>
    public const string TokenInfoEnv = "NUGET_TOKEN_INFO";

    /// <summary>
    /// This is for debugging purposes only. It allows secret or private values to be written to logs. Note that in
    /// most CI systems known secret values are scrubbed from logs which will happen whether or not this environment
    /// variable is set.
    /// </summary>
    public const string NoRedactEnv = "NUGET_TOKEN_DANGEROUS_NO_REDACT";

    /// <summary>
    /// This is for debugging purposes only. If set, this should point to a file path that will be used for file-based
    /// logging.
    /// </summary>
    public const string LogFileEnv = "NUGET_TOKEN_LOG_FILE";
}
