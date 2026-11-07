using Cicek.Application.Common;
using Cicek.Domain.Media;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Catalog;

/// <summary>
/// Which image references are acceptable. Platform-uploaded images (<c>/media/{guid}</c>) are always syntactically
/// acceptable (ownership is checked separately). External HTTPS URLs need an explicit host allowlist: with an empty
/// allowlist they are rejected, unless <c>Marketplace:AllowAnyExternalImageHost</c> is set (development/test only).
/// </summary>
public sealed class ImageUrlPolicy(IOptions<MarketplaceOptions> options)
{
    public bool IsAllowed(string? url)
    {
        var normalized = ImageReference.Normalize(url);
        if (normalized is null)
        {
            return false;
        }

        if (ImageReference.TryGetMediaId(normalized, out _))
        {
            return true;
        }

        var host = new Uri(normalized).Host;
        var allowlist = options.Value.ImageHostAllowlist;
        return allowlist.Length > 0
            ? allowlist.Contains(host, StringComparer.OrdinalIgnoreCase)
            : options.Value.AllowAnyExternalImageHost;
    }
}
