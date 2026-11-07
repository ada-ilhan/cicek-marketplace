using Cicek.Api.Configuration;
using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cicek.Api.Tests;

/// <summary>
/// Misconfiguration must stop the API at startup instead of running with development conveniences or without a signing
/// key. None of these tests need Oracle: the host is expected to fail before it serves anything.
/// </summary>
public sealed class ProductionSafetyTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("production", true, false)]
    [InlineData("Development", true, true)]
    [InlineData("Testing", true, true)]
    [InlineData("Production", false, true)]
    public void Arbitrary_image_hosts_are_only_acceptable_in_development_and_test(string environment, bool allowAny, bool valid)
    {
        var result = new MarketplaceSafetyValidator(new FakeEnvironment(environment))
            .Validate(null, new MarketplaceOptions { AllowAnyExternalImageHost = allowAny });

        Assert.Equal(valid, result.Succeeded);
    }

    [Theory]
    [InlineData("Production", "Local", false)]
    [InlineData("Production", "None", true)]
    [InlineData("Development", "Local", true)]
    [InlineData("Testing", "Local", true)]
    public void The_local_disk_image_store_is_only_acceptable_in_development_and_test(string environment, string provider, bool valid)
    {
        var result = new MediaSafetyValidator(new FakeEnvironment(environment)).Validate(null, new MediaOptions { Provider = provider });

        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public void Production_with_the_development_switch_fails_at_startup_with_a_clear_message()
    {
        var ex = Assert.ThrowsAny<Exception>(() => StartProduction(("Marketplace:AllowAnyExternalImageHost", "true")));

        Assert.Contains("AllowAnyExternalImageHost", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Production_with_local_image_storage_fails_at_startup()
    {
        var ex = Assert.ThrowsAny<Exception>(() => StartProduction(("Media:Provider", "Local"), ("Media:LocalPath", "/tmp/x")));

        Assert.Contains("Media:Provider", ex.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void A_missing_or_weak_jwt_signing_key_fails_at_startup_without_echoing_the_value(string key)
    {
        var ex = Assert.ThrowsAny<Exception>(() => StartProduction(("Auth:Jwt:SigningKey", key)));

        var text = ex.ToString();
        Assert.Contains("SigningKey", text, StringComparison.Ordinal);
        if (key.Length > 0)
        {
            Assert.DoesNotContain(key, text.Replace("SigningKey", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.jpg", "cdn.example.com", true)]
    [InlineData("https://CDN.EXAMPLE.COM/a.jpg", "cdn.example.com", true)]
    [InlineData("https://evil.example.com/a.jpg", "cdn.example.com", false)]
    [InlineData("https://cdn.example.com.evil.net/a.jpg", "cdn.example.com", false)]
    [InlineData("http://cdn.example.com/a.jpg", "cdn.example.com", false)]
    [InlineData("//cdn.example.com/a.jpg", "cdn.example.com", false)]
    [InlineData("https://user:pw@cdn.example.com/a.jpg", "cdn.example.com", false)]
    [InlineData("javascript:alert(1)", "cdn.example.com", false)]
    [InlineData("data:image/png;base64,AAAA", "cdn.example.com", false)]
    [InlineData("not a url", "cdn.example.com", false)]
    [InlineData("", "cdn.example.com", false)]
    public void Only_https_images_on_explicitly_allowed_hosts_are_accepted(string url, string host, bool expected)
    {
        var policy = new ImageUrlPolicy(Options.Create(new MarketplaceOptions { ImageHostAllowlist = [host] }));

        Assert.Equal(expected, policy.IsAllowed(url));
    }

    [Fact]
    public void An_empty_allowlist_rejects_every_external_host_by_default()
    {
        var policy = new ImageUrlPolicy(Options.Create(new MarketplaceOptions()));

        Assert.False(policy.IsAllowed("https://anything.example.com/a.jpg"));
    }

    private static void StartProduction(params (string Key, string Value)[] overrides)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // Valid baseline so the setting under test is the only reason to fail.
            builder.UseSetting("Auth:Jwt:SigningKey", new string('k', 40));
            builder.UseSetting("Database:Password", "x");
            foreach (var (key, value) in overrides)
            {
                builder.UseSetting(key, value);
            }
        });

        _ = factory.Server; // starts the host; ValidateOnStart runs before any request is served
    }
}
