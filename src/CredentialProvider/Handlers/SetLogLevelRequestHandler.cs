using NuGet.Protocol.Plugins;

namespace NuGet.TokenCredentialProvider;

class SetLogLevelRequestHandler : RequestHandlerBase<SetLogLevelRequest, SetLogLevelResponse>
{
    public SetLogLevelRequestHandler(PluginLogger logger) : base(logger)
    {
    }

    public override Task<SetLogLevelResponse> HandleRequestAsync(SetLogLevelRequest request, CancellationToken cancellationToken)
    {
        _logger.LogLevel = request.LogLevel;
        return Task.FromResult(new SetLogLevelResponse(MessageResponseCode.Success));
    }
}
