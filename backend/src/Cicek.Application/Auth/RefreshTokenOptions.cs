using System.ComponentModel.DataAnnotations;

namespace Cicek.Application.Auth;

/// <summary>Configuration section "Auth:RefreshTokens".</summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "Auth:RefreshTokens";

    /// <summary>Lifetime of a single refresh token before it must be rotated.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "90.00:00:00")]
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Absolute lifetime of a login (rotation family); the user must log in again afterwards.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "365.00:00:00")]
    public TimeSpan FamilyLifetime { get; set; } = TimeSpan.FromDays(30);
}
