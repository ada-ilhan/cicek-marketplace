using System.Net;
using System.Text.Json;
using Cicek.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cicek.Api.Tests;

/// <summary>
/// Problem Details, safe error responses, correlation ids and security headers. Independent of Oracle:
/// database failures are simulated through a fake <see cref="IDatabaseErrorClassifier"/>.
/// </summary>
public sealed class ErrorHandlingTests : IClassFixture<ErrorHandlingTests.Factory>
{
    private const string SecretDetail = "ORA-12541 Data Source=prod-db:1521/SECRET;Password=hunter2";
    private readonly Factory _factory;

    public ErrorHandlingTests(Factory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("plain", HttpStatusCode.InternalServerError, "server.error")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "service.unavailable")]
    [InlineData("contention", HttpStatusCode.ServiceUnavailable, "service.busy")]
    [InlineData("duplicate", HttpStatusCode.Conflict, "conflict.duplicate")]
    [InlineData("rule", HttpStatusCode.UnprocessableEntity, "schema_access.grantee_not_found")]
    public async Task Exceptions_become_safe_problem_details(string kind, HttpStatusCode status, string code)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/__test/throw/{kind}", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(body);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("ORA-", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at Cicek", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unavailable_database_advertises_retry_after()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/unavailable", Ct);

        Assert.Equal("5", response.Headers.RetryAfter?.ToString());
    }

    [Fact]
    public async Task Valid_correlation_id_is_echoed_and_invalid_one_is_replaced()
    {
        using var client = _factory.CreateClient();

        using var valid = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        valid.Headers.Add("X-Correlation-Id", "bff-req-42");
        var validResponse = await client.SendAsync(valid, Ct);

        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        invalid.Headers.Add("X-Correlation-Id", "<script>alert(1)</script>");
        var invalidResponse = await client.SendAsync(invalid, Ct);

        Assert.Equal("bff-req-42", validResponse.Headers.GetValues("X-Correlation-Id").Single());
        Assert.NotEqual("<script>alert(1)</script>", invalidResponse.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Security_headers_are_set()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live", Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>API host with a non-functional database and a throwing test endpoint appended to the pipeline.</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:DataSource", "localhost:1599/NONE");
            builder.UseSetting("Database:UserId", "NOBODY");
            builder.UseSetting("Database:Password", "unused");
            builder.UseSetting("Auth:Jwt:SigningKey", ApiTestHost.SigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDatabaseErrorClassifier>();
                services.AddSingleton<IDatabaseErrorClassifier, FakeClassifier>();
                services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>();
            });
        }
    }

    private sealed class SimulatedDatabaseException(string kind) : Exception(SecretDetail)
    {
        public string Kind { get; } = kind;
    }

    private sealed class FakeClassifier : IDatabaseErrorClassifier
    {
        public DatabaseFailure? Classify(Exception exception) => exception is SimulatedDatabaseException simulated
            ? simulated.Kind switch
            {
                "unavailable" => new DatabaseFailure(DatabaseFailureKind.Unavailable, 12541),
                "contention" => new DatabaseFailure(DatabaseFailureKind.TransientContention, 60),
                "duplicate" => new DatabaseFailure(DatabaseFailureKind.UniqueViolation, 1),
                "rule" => new DatabaseFailure(DatabaseFailureKind.ApplicationRule, 20002, "schema_access.grantee_not_found"),
                _ => null,
            }
            : null;
    }

    private sealed class ThrowingEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((HttpContext context, RequestDelegate _) =>
            {
                const string prefix = "/__test/throw/";
                if (context.Request.Path.Value?.StartsWith(prefix, StringComparison.Ordinal) != true)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }

                var kind = context.Request.Path.Value[prefix.Length..];
                throw kind == "plain" ? new InvalidOperationException(SecretDetail) : new SimulatedDatabaseException(kind);
            });
        };
    }
}
