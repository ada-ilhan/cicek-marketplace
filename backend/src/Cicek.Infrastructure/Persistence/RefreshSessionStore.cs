using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Persistence;

internal sealed class RefreshSessionStore(AppDbContext db) : IRefreshSessionStore
{
    public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshSession>> ListUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        // Re-read from the database even if entities are tracked, so a retry after a concurrency
        // conflict sees the current row versions.
        foreach (var tracked in db.ChangeTracker.Entries<RefreshSession>().Where(e => e.Entity.FamilyId == familyId).ToList())
        {
            await tracked.ReloadAsync(cancellationToken);
        }

        return await db.RefreshSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshSession session) => db.RefreshSessions.Add(session);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Discard the failed unit of work so the context can be reused within this request.
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("A refresh session was modified concurrently.", ex);
        }
    }
}
