using System.Text.RegularExpressions;

namespace Cicek.Domain.Media;

/// <summary>
/// What a product or shop image may point to: an image stored by the platform (<c>/media/{guid}</c>, uploaded through
/// the upload endpoint) or an absolute HTTPS URL on an allowed external host (host policy lives in the application
/// layer). File paths, other schemes and credentials in URLs are never accepted.
/// </summary>
public static partial class ImageReference
{
    public const int MaxLength = 1000;
    public const string MediaPrefix = "/media/";

    /// <summary>Normalised reference, or null when the value is not an acceptable image reference.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (TryGetMediaId(trimmed, out var id))
        {
            return MediaPrefix + id.ToString("D");
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }

    public static bool TryGetMediaId(string? value, out Guid id)
    {
        id = Guid.Empty;
        var match = value is null ? null : MediaPattern().Match(value);
        return match is { Success: true } && Guid.TryParse(match.Groups["id"].Value, out id);
    }

    [GeneratedRegex("^/media/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$", RegexOptions.CultureInvariant)]
    private static partial Regex MediaPattern();
}
