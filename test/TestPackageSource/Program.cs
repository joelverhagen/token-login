using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var log = new ConcurrentQueue<object>();

app.MapGet("/", (HttpContext context) => log);

app.MapGet("/v3/index.json", (HttpContext context) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    return new
    {
        version = "3.0.0",
        resources = new[]
        {
            new ServiceIndexEntry($"{baseUrl}/v3/publish", "PackagePublish/2.0.0")
        }
    };
}).AddEndpointFilter(LogRequest);

app.MapPut(
    "/v3/publish",
    (HttpContext context) => HandleAuthenticate(context, 201))
    .AddEndpointFilter(LogRequest);

app.MapMethods(
    "/v3/publish/{id}/{version}",
    ["POST", "DELETE"],
    (HttpContext context) => HandleAuthenticate(context, context.Request.Method == "DELETE" ? 204 : 200))
    .AddEndpointFilter(LogRequest);

app.Run();

IResult HandleAuthenticate(HttpContext context, int successStatusCode)
{
    if (!TryParseBasicAuth(context.Request.Headers["Authorization"], out var username, out var password))
    {
        context.Response.Headers.WWWAuthenticate = "Basic";
        return TypedResults.StatusCode(401);
    }

    context.Items.Add("info", new { username, password });

    if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
    {
        return TypedResults.StatusCode(401);
    }

    return TypedResults.StatusCode(successStatusCode);
}

async ValueTask<object?> LogRequest(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var result = await next(context);

    context.HttpContext.Items.TryGetValue("info", out var info);

    log.Enqueue(new
    {
        context.HttpContext.Request.Method,
        context.HttpContext.Request.Path.Value,
        Authorization = context.HttpContext.Request.Headers.Authorization.ToString(),
        (result as IStatusCodeHttpResult)?.StatusCode,
        Info = info,
    });

    return result;
}

static bool TryParseBasicAuth(string? header, out string? username, out string? password)
{
    username = null;
    password = null;

    if (string.IsNullOrWhiteSpace(header))
    {
        return false;
    }

    const string scheme = "Basic ";
    if (!header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var encoded = header.AsSpan().Slice(scheme.Length);
    Span<byte> buffer = stackalloc byte[encoded.Length];
    if (!Convert.TryFromBase64Chars(encoded, buffer, out var byteCount))
    {
        return false;
    }
    buffer = buffer.Slice(0, byteCount);

    Span<char> decodedChar = stackalloc char[encoded.Length];
    if (!Encoding.UTF8.TryGetChars(buffer.Slice(0, byteCount), decodedChar, out var charCount))
    {
        return false;
    }
    decodedChar = decodedChar.Slice(0, charCount);

    var colonIndex = decodedChar.IndexOf(':');
    if (colonIndex < 0)
    {
        return false;
    }

    username = new string(decodedChar.Slice(0, colonIndex));
    password = new string(decodedChar.Slice(colonIndex + 1));
    return true;
}

public record ServiceIndexEntry(
    [property: JsonPropertyName("@id")] string Url,
    [property: JsonPropertyName("@type")] string Type);
