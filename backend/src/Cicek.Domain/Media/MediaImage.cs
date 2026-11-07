using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Media;

/// <summary>
/// Metadata of an uploaded image. The bytes live in object/file storage (never in Oracle); <see cref="StorageKey"/> is a
/// generated identifier, never derived from the uploaded file name.
/// </summary>
public sealed class MediaImage : Entity
{
    public const int StorageKeyMaxLength = 100;

    private MediaImage(Guid id, Guid shopId, string storageKey, ImageType type, long sizeBytes, Guid uploadedBy, DateTimeOffset now)
        : base(id)
    {
        ShopId = shopId;
        StorageKey = storageKey;
        ContentType = ImageSignature.ContentType(type);
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedBy;
        CreatedAt = now;
    }

    private MediaImage()
    {
    }

    public Guid ShopId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Public URL path of the image (served by the platform, same origin as the storefront).</summary>
    public string Url => ImageReference.MediaPrefix + Id.ToString("D");

    public static MediaImage Create(Guid shopId, ImageType type, long sizeBytes, Guid uploadedBy, DateTimeOffset now)
    {
        if (shopId == Guid.Empty || uploadedBy == Guid.Empty)
        {
            throw new DomainException("media.owner_required", "An image belongs to a shop and an uploader.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("media.empty", "The image is empty.");
        }

        var id = Guid.CreateVersion7();
        return new MediaImage(id, shopId, $"{id:N}.{ImageSignature.Extension(type)}", type, sizeBytes, uploadedBy, now);
    }
}
