using Cicek.Domain.Identity;

namespace Cicek.Application.Abstractions.Identity;

/// <summary>
/// Persistence port for refresh sessions. A dedicated store (instead of generic DbContext access)
/// is justified because rotation relies on optimistic concurrency semantics that the implementation
/// must translate into <see cref="Persistence.ConcurrencyConflictException"/>.
/// </summary>
public interface IRefreshSessionStore
{
    Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshSession>> ListUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    void Add(RefreshSession session);

    /// <summary>Commits pending changes in one transaction.</summary>
    /// <exception cref="Persistence.ConcurrencyConflictException">A tracked session was modified concurrently.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
