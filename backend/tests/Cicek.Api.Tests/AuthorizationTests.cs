using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cicek.Application.Auth;
using Cicek.Application.Authorization;
using Cicek.Application.SystemStatus;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cicek.Api.Tests;

/// <summary>Role/permission matrix and token handling through the real HTTP pipeline (Oracle-backed).</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(ApiTestHost host)
{
    private const string StatusPath = "/api/v1/admin/system/status";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient ClientWithToken(string token)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData(StatusPath)]
    public async Task Anonymous_requests_to_protected_endpoints_get_401_problem_details(string path)
    {
        host.SkipIfUnavailable();
        using var client = host.CreateClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(Roles.FloristOwner, HttpStatusCode.Forbidden)]
    [InlineData(Roles.FloristStaff, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Finance, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Support, HttpStatusCode.OK)]
    [InlineData(Roles.Administrator, HttpStatusCode.OK)]
    public async Task System_status_requires_the_status_permission(string role, HttpStatusCode expected)
    {
        host.SkipIfUnavailable();
        var session = await host.SignInAsUserWithRolesAsync(role);
        using var client = ClientWithToken(session.AccessToken);

        var response = await client.GetAsync(StatusPath, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_sees_database_status_without_connection_details()
    {
        host.SkipIfUnavailable();
        var session = await host.SignInAsUserWithRolesAsync(Roles.Administrator);
        using var client = ClientWithToken(session.AccessToken);

        var body = await client.GetStringAsync(StatusPath, Ct);
        var status = JsonSerializer.Deserialize<SystemStatusDto>(body, JsonSerializerOptions.Web)!;

        Assert.True(status.Database.IsReachable);
        Assert.Empty(status.Database.PendingMigrations);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_returns_roles_and_derived_permissions()
    {
        host.SkipIfUnavailable();
        var session = await host.SignInAsUserWithRolesAsync(Roles.Support);
        using var client = ClientWithToken(session.AccessToken);

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/v1/me", Ct);

        Assert.Equal([Roles.Support], me!.Roles);
        Assert.Equal([Permissions.AdminOverviewRead, Permissions.OrdersRead, Permissions.SystemStatusRead, Permissions.UsersRead], me.Permissions);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        host.SkipIfUnavailable();
        var forged = CreateToken("a-completely-different-signing-key-0123456789abcdef", DateTime.UtcNow.AddMinutes(5), Roles.Administrator);
        using var client = ClientWithToken(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(StatusPath, Ct)).StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        host.SkipIfUnavailable();
        var expired = CreateToken(ApiTestHost.SigningKey, DateTime.UtcNow.AddMinutes(-5), Roles.Administrator);
        using var client = ClientWithToken(expired);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(StatusPath, Ct)).StatusCode);
    }

    [Fact]
    public async Task Registration_login_refresh_reuse_and_logout_flow()
    {
        host.SkipIfUnavailable();
        using var client = host.CreateClient();
        var email = $"flow.{Guid.NewGuid():N}@example.com";

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCustomerCommand(email, ApiTestHost.Password), Ct);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal("no-store", registered.Headers.CacheControl?.ToString());
        var session = (await registered.Content.ReadFromJsonAsync<AuthSessionDto>(Ct))!;
        Assert.Equal([Roles.Customer], session.User.Roles);

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCustomerCommand(email, ApiTestHost.Password), Ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokensCommand(session.RefreshToken), Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AuthSessionDto>(Ct))!;

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutCommand(rotated.RefreshToken), Ct);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokensCommand(rotated.RefreshToken), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_field_errors()
    {
        host.SkipIfUnavailable();
        using var client = host.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCustomerCommand("nope", "short"), Ct);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Health_endpoints_report_live_and_ready()
    {
        host.SkipIfUnavailable();
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", Ct)).StatusCode);
    }

    private static string CreateToken(string key, DateTime expires, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "cicek-api",
            Audience = "cicek-clients",
            Subject = new ClaimsIdentity(claims),
            NotBefore = expires.AddMinutes(-20),
            IssuedAt = expires.AddMinutes(-20),
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        });
    }
}
