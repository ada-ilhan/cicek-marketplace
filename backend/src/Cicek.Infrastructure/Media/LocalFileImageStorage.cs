using Cicek.Application.Abstractions.Media;
using Cicek.Application.Common;
using Microsoft.Extensions.Options;

namespace Cicek.Infrastructure.Media;

/// <summary>
/// DEVELOPMENT / TEST ONLY image storage on the API host's local disk. Keys are platform-generated
/// (<c>{guid:N}.{jpg|png|webp}</c>) and re-validated here, so no caller-supplied path can ever leave the storage directory.
/// Production should use an object-storage adapter behind <see cref="IImageStorage"/> (see ADR-0014).
/// </summary>
internal sealed class LocalFileImageStorage : IImageStorage
{
    private readonly string _root;

    public LocalFileImageStorage(IOptions<MediaOptions> options)
    {
        var path = options.Value.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Media:LocalPath is required when Media:Provider is 'Local'.");
        }

        _root = Path.GetFullPath(path);
        Directory.CreateDirectory(_root);
    }

    public bool IsConfigured => true;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var target = Resolve(key);
        await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var target = Resolve(key);
        Stream? stream = File.Exists(target)
            ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var target = Resolve(key);
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (!IsSafeKey(key))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        return full;
    }

    private static bool IsSafeKey(string key)
    {
        var dot = key.IndexOf('.', StringComparison.Ordinal);
        if (dot != 32 || key.Length > 40 || !Guid.TryParseExact(key[..dot], "N", out _))
        {
            return false;
        }

        return key[(dot + 1)..] is "jpg" or "png" or "webp";
    }
}

/// <summary>Used when no provider is configured: uploads are refused with 503 rather than writing anywhere.</summary>
internal sealed class UnconfiguredImageStorage : IImageStorage
{
    public bool IsConfigured => false;

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Image storage is not configured.");

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
}
