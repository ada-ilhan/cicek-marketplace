using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using Cicek.Api.Auth;
using Cicek.Api.Configuration;
using Cicek.Api.Endpoints;
using Cicek.Api.ErrorHandling;
using Cicek.Api.Middleware;
using Cicek.Api.OpenApi;
using Cicek.Api.RateLimiting;
using Cicek.Application;
using Cicek.Application.Auth;
using Cicek.Application.Common;
using Cicek.Infrastructure;
using Cicek.SharedKernel.Monetary;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation (Microsoft.Extensions.ApiDescription.Server) starts the host without secrets.
// Supply non-functional placeholders so options validation passes, and skip the database startup check.
var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (isOpenApiGeneration)
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Database:Password"] = "openapi-generation-placeholder",
        ["Auth:Jwt:SigningKey"] = "openapi-generation-placeholder-not-a-real-signing-key",
    });
}

// ---- Logging: structured; JSON outside development. Scopes carry the correlation id. ----------------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.IncludeScopes = true;
    });
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "O";
    });
}

// Validation messages must not depend on the host OS culture. Clients localize by field name and error
// code; server-side message localization (Accept-Language) is planned with the i18n work.
ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");

// ---- Layers ------------------------------------------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddOptions<RefreshTokenOptions>()
    .BindConfiguration(RefreshTokenOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(o => o.FamilyLifetime >= o.TokenLifetime, "FamilyLifetime must be >= TokenLifetime.")
    .ValidateOnStart();
builder.Services.AddOptions<MarketplaceOptions>()
    .BindConfiguration(MarketplaceOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(o => Currency.TryFromCode(o.Currency, out _), "Marketplace:Currency is not a supported currency.")
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Marketplace:TimeZone is not a known time zone.")
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MarketplaceOptions>, MarketplaceSafetyValidator>();
builder.Services.AddSingleton<IValidateOptions<MediaOptions>, MediaSafetyValidator>();
builder.Services.AddPersistence();

// Enums travel as their names ("PendingPayment"), which is what the typed frontend client expects.
// Numbers must be JSON numbers (the web default also accepts quoted numbers), which keeps the OpenAPI types exact.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddHealthChecks();
if (!isOpenApiGeneration)
{
    builder.Services.AddPersistenceDiagnostics();
}
builder.Services.AddApiAuthentication();

// ---- HTTP infrastructure -------------------------------------------------------------------------------
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = null;
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
    if (context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
    {
        context.ProblemDetails.Extensions["correlationId"] = correlationId;
    }
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Çiçek Marketplace API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

// Only the BFF talks to this API from browsers' point of view, so CORS is closed unless origins are configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", CorrelationIdMiddleware.HeaderName)
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
    }
}));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    // TLS terminates at the ingress/proxy; HSTS tells browsers to stay on HTTPS.
    app.UseHsts();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Çiçek API v1"));
}

// Liveness: the process responds. Readiness: dependencies (Oracle) reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(Cicek.Infrastructure.DependencyInjection.ReadinessTag) })
    .DisableRateLimiting();

var v1 = app.MapGroup("/api/v1");
v1.MapAuthEndpoints();
v1.MapMeEndpoints();
v1.MapAdminSystemEndpoints();
v1.MapCatalogEndpoints();
v1.MapShopEndpoints();
v1.MapMediaEndpoints();
v1.MapOrderEndpoints();
v1.MapAdminEndpoints();

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
