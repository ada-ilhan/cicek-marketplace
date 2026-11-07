using System.Net.Http.Json;
using Cicek.Application.Auth;
using Cicek.Infrastructure.Identity;
using Cicek.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cicek.Api.Tests;

/// <summary>
/// The real API (WebApplicationFactory) connected to the Oracle TEST schema as the least-privilege runtime
/// user, after the production deployment sequence has run against a clean schema.
/// </summary>
public sealed class ApiTestHost : IAsyncLifetime
{
    public const string SigningKey = "test-signing-key-for-integration-tests-only-0123456789";
    public const string Password = "correct-horse-battery";

    private readonly OracleTestDatabase _database = new();

    public string MediaDirectory { get; } = Path.Combine(Path.GetTempPath(), "cicek-tests-media-" + Guid.NewGuid().ToString("N"));

    public WebApplicationFactory<Program>? Factory { get; private set; }

    public void SkipIfUnavailable() => _database.SkipIfUnavailable();

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        if (_database.Settings is null)
        {
            return;
        }

        var settings = _database.Settings.AppConfiguration();
        settings["Auth:Jwt:SigningKey"] = SigningKey;
        settings["RateLimiting:AuthPermitsPerMinute"] = "10000";
        settings["RateLimiting:GlobalPermitsPerMinute"] = "100000";
        settings["Marketplace:Currency"] = "AZN"; // the market defaults (explicit multi-currency behaviour is covered by Money/Oracle tests)
        settings["Marketplace:TimeZone"] = "Asia/Baku";
        settings["Marketplace:AllowAnyExternalImageHost"] = "true";
        settings["Media:Provider"] = "Local";
        settings["Media:LocalPath"] = MediaDirectory;
        settings["Marketplace:DeliveryFeePerOrder"] = "30";
        settings["Marketplace:DefaultCommissionRateBps"] = "1500";
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _database.DisposeAsync();
        try
        {
            if (Directory.Exists(MediaDirectory))
            {
                Directory.Delete(MediaDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort cleanup of the temporary image folder
        }
    }

    public HttpClient CreateClient() => Factory!.CreateClient();

    /// <summary>Creates a user with the given roles directly in the store and signs in through the API.</summary>
    public async Task<AuthSessionDto> SignInAsUserWithRolesAsync(params string[] roles)
    {
        var email = $"user.{Guid.NewGuid():N}@example.com";
        await using (var scope = Factory!.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, CreatedAt = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            if (roles.Length > 0)
            {
                Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
            }
        }

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, Password), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthSessionDto>(TestContext.Current.CancellationToken))!;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiTestHost>
{
    public const string Name = "Api+Oracle";
}
