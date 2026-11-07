using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Cicek.Api.RateLimiting;

/// <summary>
/// Per-client-IP limits. The client IP is taken from X-Forwarded-For only when the immediate peer is a
/// trusted proxy (the BFF), see <c>ReverseProxy:KnownProxies</c>. Account-level brute force is additionally
/// limited by Identity lockout.
/// </summary>
internal static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Upload = "upload";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermitsPerMinute = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
        var uploadPermitsPerMinute = configuration.GetValue("RateLimiting:UploadPermitsPerMinute", 30);
        var globalPermitsPerMinute = configuration.GetValue("RateLimiting:GlobalPermitsPerMinute", 600);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests",
                        detail: "Please wait before trying again.",
                        extensions: new Dictionary<string, object?> { ["code"] = "rate_limited" })
                    .ExecuteAsync(context.HttpContext);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.AddPolicy(Auth, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.AddPolicy(Upload, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = uploadPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    private static string ClientKey(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
