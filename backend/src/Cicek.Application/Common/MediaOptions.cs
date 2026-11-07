using System.ComponentModel.DataAnnotations;

namespace Cicek.Application.Common;

/// <summary>Configuration section "Media" (image uploads).</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// "None" (default; uploads are refused) or "Local" (DEVELOPMENT/TEST ONLY: files on the API host's disk).
    /// A production object-storage adapter implements <c>IImageStorage</c> and is selected here later.
    /// </summary>
    [Required]
    public string Provider { get; set; } = "None";

    /// <summary>Directory for the Local provider (created if missing).</summary>
    public string? LocalPath { get; set; }

    [Range(10_000, 20_000_000)]
    public int MaxBytes { get; set; } = 5_000_000;

    /// <summary>Upper bound of stored images per shop (limits abuse and storage growth).</summary>
    [Range(1, 10_000)]
    public int MaxImagesPerShop { get; set; } = 300;
}
