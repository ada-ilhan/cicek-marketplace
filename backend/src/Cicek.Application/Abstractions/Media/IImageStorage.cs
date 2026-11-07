namespace Cicek.Application.Abstractions.Media;

/// <summary>
/// Binary image storage port. Oracle keeps only metadata; the bytes live behind this abstraction so a production
/// object-storage provider can be chosen later (S3-compatible, Azure Blob, …) without touching use cases.
/// Keys are platform-generated identifiers, never user-supplied file names.
/// </summary>
public interface IImageStorage
{
    /// <summary>False when no storage provider is configured; uploads are then refused (fail-safe).</summary>
    bool IsConfigured { get; }

    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens the stored bytes, or null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
