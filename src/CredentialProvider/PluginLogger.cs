using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol.Plugins;
using static NuGet.TokenCredentialProvider.Constants;

namespace NuGet.TokenCredentialProvider;

class PluginLogger : IDisposable
{
    private static readonly int Pid = Process.GetCurrentProcess().Id;

    private readonly Channel<(LogLevel Level, string Message)> _messages;
    private readonly CancellationTokenSource _stopCts;

    /// <summary>
    /// The set of values to redact. The key is the redacted value. The value is whether or not the value is JSON.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _redacted;
    private readonly string? _logFilePath;
    private readonly bool _shouldRedact;
    private readonly Lazy<Task> _lazyFlush;

    public PluginLogger()
    {
        _messages = Channel.CreateUnbounded<(LogLevel Level, string Message)>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
        });
        _stopCts = new();
        _redacted = new();
        _lazyFlush = new Lazy<Task>(PumpAsync);

        if (!bool.TryParse(Environment.GetEnvironmentVariable(NoRedactEnv), out _shouldRedact))
        {
            _shouldRedact = true;
        }

        var logFilePath = Environment.GetEnvironmentVariable(LogFileEnv)?.Trim();
        if (!string.IsNullOrEmpty(logFilePath))
        {
            var fullPath = Path.GetFullPath(logFilePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _logFilePath = fullPath;
            if (LogToFile(string.Empty, out var exception))
            {
                Log(LogLevel.Minimal, $"{LogFileEnv} is enabled. Logs will be written to {fullPath}.");
            }
            else
            {
                _logFilePath = null;
                Log(LogLevel.Minimal, $"Using {LogFileEnv} to write to {logFilePath} ({fullPath}) failed. {exception?.Message}");
            }
        }

        if (!_shouldRedact)
        {
            Log(LogLevel.Minimal, $"{NoRedactEnv} is enabled. Sensitive values will not be redacted from logs.");
        }
    }
    
    private bool LogToFile(string message, out Exception? exception)
    {
        exception = null;
        if (_logFilePath is null)
        {
            return true;
        }

        for (var i = 0; i < 5; i++)
        {
            if (i > 0)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(10));
            }

            try
            {
                File.AppendAllText(_logFilePath, message);
                return true;
            }
            catch (Exception ex)
            {
                // best effort
                exception = ex;
            }
        }

        return false;
    }

    public IPlugin? Plugin { get; set; }
    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    public void AddRedactedValue(string value)
    {
        if (_redacted.TryAdd(value, false))
        {
            // System.Text.Json encodes some characters differently
            _redacted.GetOrAdd(System.Text.Json.JsonSerializer.Serialize(value), true);
            _redacted.GetOrAdd(JsonConvert.SerializeObject(value), true);
            _redacted.GetOrAdd(SerializeForLoggingInternal(value), true);
        }
    }

    public void LogJwtClaims(string label, string token)
    {
        try
        {
            var pieces = token.Split('.', 3);
            var header = Base64UrlEncoder.Decode(pieces[0]);
            Log(LogLevel.Debug, $"Header in the {label} JWT: {header}");
            var payload = Base64UrlEncoder.Decode(pieces[1]);
            Log(LogLevel.Debug, $"Payload in the {label} JWT: {payload}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Information, $"Could not parse the {label} JWT. It may be invalid. " + ex.Message);
        }
    }

    public string SerializeForLogging<T>(T value)
    {
        return Redact(SerializeForLoggingInternal(value));
    }

    private static string SerializeForLoggingInternal<T>(T value)
    {
        using var stringWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(stringWriter))
        {
            JsonSerializationUtilities.Serialize(jsonWriter, value);
        }

        return stringWriter.ToString();
    }

    public void Dispose()
    {
        try
        {
            _stopCts.Cancel();
        }
        catch
        {
            // ignore
        }

        _stopCts.Dispose();
    }

    public void Log(LogLevel level, string message)
    {
        Log(level, message, onlyFile: false);
    }

    public void Log(LogLevel level, string message, bool onlyFile)
    {
        var levelPrefix = level switch
        {
            LogLevel.Debug => "DBUG",
            LogLevel.Verbose => "VERB",
            LogLevel.Information => "INFO",
            LogLevel.Minimal => "MIN ",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERR ",
            _ => "????",
        };

        message = Redact(message);

        if (_logFilePath is not null)
        {
            var fileMessage = $"[{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fff} {Pid} {levelPrefix}] {message}{Environment.NewLine}";
            LogToFile(fileMessage, out _);
        }

        if (!onlyFile)
        {
            var hostMessage = $"    [token-login] {message}";
            _messages.Writer.TryWrite((level, hostMessage));
        }
    }

    private string Redact(string message)
    {
        if (_shouldRedact)
        {
            foreach (var (value, isJson) in _redacted)
            {
                message = message.Replace(
                    value,
                    isJson ? "\"REDACTED\"" : "REDACTED",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        return message;
    }

    public void Start()
    {
        var _ = _lazyFlush.Value;
    }

    public async Task PauseForEmptyAsync(TimeSpan delay)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
        while (_lazyFlush.IsValueCreated && sw.Elapsed < delay && _messages.Reader.TryPeek(out _));
    }

    public async Task StopAsync(TimeSpan delay)
    {
        if (!_lazyFlush.IsValueCreated)
        {
            return;
        }

        _messages.Writer.TryComplete();
        _stopCts.CancelAfter(delay);
        try
        {
            await _lazyFlush.Value;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    private async Task PumpAsync()
    {
        await foreach (var (level, message) in _messages.Reader.ReadAllAsync(_stopCts.Token))
        {
            if (level < LogLevel)
            {
                continue;
            }

            var plugin = Plugin;
            if (plugin is null)
            {
                continue;
            }

            try
            {
                await plugin.Connection.SendRequestAndReceiveResponseAsync<LogRequest, LogResponse>(
                    MessageMethod.Log,
                    new LogRequest(level, message),
                    _stopCts.Token);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to write log to plugin host. {ex}", onlyFile: true);
            }
        }
    }
}
