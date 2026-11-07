using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Cicek.Api.Middleware;

/// <summary>
/// Accepts a well-formed inbound <c>X-Correlation-Id</c> (e.g. from the BFF) or uses the W3C trace id,
/// echoes it on the response and adds it to the logging scope of the request.
/// </summary>
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(inbound)
            ? inbound
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }

    // Only safe characters, bounded length: the value is reflected in headers and logs.
    private static bool IsValid(string value) => value.Length is > 0 and <= 64 && SafePattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePattern();
}
