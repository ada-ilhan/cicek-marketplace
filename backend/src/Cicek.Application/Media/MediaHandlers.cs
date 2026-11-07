using Cicek.Application.Abstractions.Media;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Application.Shops;
using Cicek.Domain.Media;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Media;

public sealed record UploadedImageDto(Guid Id, string Url, string ContentType, long SizeBytes);

public sealed record MediaContent(Stream Content, string ContentType, long SizeBytes, string ETag);

public static class MediaErrors
{
    public static readonly Error NotConfigured = Error.Unavailable("media.storage_not_configured", "Image uploads are not available right now.");
    public static readonly Error Empty = Error.Validation("media.empty", "The file is empty.");
    public static readonly Error UnsupportedType = Error.Validation("media.unsupported_type", "Only JPEG, PNG and WebP images are accepted.");
    public static Error TooLarge(int maxBytes) => Error.Validation("media.too_large", $"The image must be at most {maxBytes / 1_000_000.0:0.#} MB.");
    public static readonly Error LimitReached = Error.Conflict("media.limit_reached", "This shop has reached its image limit.");
    public static readonly Error NotFound = Error.NotFound("media.not_found", "Image not found.");
    public static readonly Error InvalidReference = Error.Validation("media.invalid_reference", "An image does not exist or belongs to another shop.");
}

/// <summary>
/// Stores an uploaded image for a shop the caller may manage. The file type is decided from the file's own bytes (never
/// from its name or the client's MIME type), the size is bounded, the storage key is generated, and the file is stored
/// outside Oracle. Metadata and bytes are written together; if the metadata cannot be saved the bytes are removed.
/// </summary>
public sealed class UploadShopImageHandler(
    ShopAccess access,
    IAppDbContext db,
    IImageStorage storage,
    IOptions<MediaOptions> options,
    TimeProvider time)
{
    public async Task<Result<UploadedImageDto>> HandleAsync(Guid shopId, Guid userId, Stream file, CancellationToken ct)
    {
        if (!storage.IsConfigured)
        {
            return MediaErrors.NotConfigured;
        }

        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageCatalog, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var max = options.Value.MaxBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await file.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > max)
            {
                return MediaErrors.TooLarge(max);
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            return MediaErrors.Empty;
        }

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (ImageSignature.Detect(bytes) is not { } type)
        {
            return MediaErrors.UnsupportedType;
        }

        if (await db.MediaImages.CountAsync(m => m.ShopId == shopId, ct) >= options.Value.MaxImagesPerShop)
        {
            return MediaErrors.LimitReached;
        }

        var image = MediaImage.Create(shopId, type, buffer.Length, userId, time.GetUtcNow());
        buffer.Position = 0;
        await storage.SaveAsync(image.StorageKey, buffer, image.ContentType, ct);
        try
        {
            db.MediaImages.Add(image);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(image.StorageKey, CancellationToken.None);
            throw;
        }

        return new UploadedImageDto(image.Id, image.Url, image.ContentType, image.SizeBytes);
    }
}

/// <summary>Serves a stored image to anyone (public, immutable). Unknown ids and missing files are 404.</summary>
public sealed class GetMediaHandler(IAppDbContext db, IImageStorage storage)
{
    public async Task<Result<MediaContent>> HandleAsync(Guid id, CancellationToken ct)
    {
        var meta = await db.MediaImages.AsNoTracking().Where(m => m.Id == id)
            .Select(m => new { m.StorageKey, m.ContentType, m.SizeBytes })
            .SingleOrDefaultAsync(ct);
        if (meta is null || !storage.IsConfigured)
        {
            return MediaErrors.NotFound;
        }

        var stream = await storage.OpenReadAsync(meta.StorageKey, ct);
        return stream is null
            ? MediaErrors.NotFound
            : new MediaContent(stream, meta.ContentType, meta.SizeBytes, $"\"{id:N}\"");
    }
}

/// <summary>
/// Ensures every platform image referenced by a product or shop exists and belongs to <em>that</em> shop, so one shop can
/// never attach (or swap in) another shop's images. External URLs are checked by <c>ImageUrlPolicy</c>.
/// </summary>
public sealed class MediaReferenceValidator(IAppDbContext db)
{
    /// <param name="shopId">The owning shop, or null when none exists yet (platform images cannot be referenced then).</param>
    public async Task<Result> ValidateAsync(Guid? shopId, IEnumerable<string?> references, CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var reference in references)
        {
            if (ImageReference.TryGetMediaId(reference, out var id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            return Result.Success();
        }

        if (shopId is null)
        {
            return MediaErrors.InvalidReference;
        }

        var distinct = ids.Distinct().ToList();
        var owned = await db.MediaImages.AsNoTracking().CountAsync(m => distinct.Contains(m.Id) && m.ShopId == shopId, ct);
        return owned == distinct.Count ? Result.Success() : MediaErrors.InvalidReference;
    }
}
