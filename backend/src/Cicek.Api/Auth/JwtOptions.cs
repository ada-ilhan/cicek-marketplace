using System.ComponentModel.DataAnnotations;

namespace Cicek.Api.Auth;

/// <summary>Configuration section "Auth:Jwt". <see cref="SigningKey"/> must come from a secret store.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key; at least 32 characters (256 bits) of high-entropy secret.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Short-lived by design: role changes and lockouts take effect within this window.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
}
