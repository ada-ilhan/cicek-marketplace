using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Tests.Media;

public sealed class ImageUrlPolicyTests
{
    private static ImageUrlPolicy Policy(string[] allowlist, bool allowAny = false) =>
        new(Options.Create(new MarketplaceOptions { ImageHostAllowlist = allowlist, AllowAnyExternalImageHost = allowAny }));

    [Fact]
    public void An_empty_allowlist_does_not_silently_accept_external_hosts()
    {
        Assert.False(Policy([]).IsAllowed("https://anything.example.com/a.jpg"));
    }

    [Fact]
    public void Platform_uploads_are_always_allowed()
    {
        Assert.True(Policy([]).IsAllowed($"/media/{Guid.NewGuid()}"));
    }

    [Fact]
    public void The_development_switch_accepts_any_https_host()
    {
        var policy = Policy([], allowAny: true);
        Assert.True(policy.IsAllowed("https://anything.example.com/a.jpg"));
        Assert.False(policy.IsAllowed("http://anything.example.com/a.jpg"));
    }

    [Fact]
    public void A_configured_allowlist_wins_over_the_development_switch()
    {
        var policy = Policy(["cdn.example.com"], allowAny: true);
        Assert.True(policy.IsAllowed("https://CDN.example.com/a.jpg"));
        Assert.False(policy.IsAllowed("https://evil.example.com/a.jpg"));
    }
}
