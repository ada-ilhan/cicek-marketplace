using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Common;
using Cicek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Identity;

/// <summary>Read-only identity directory for the administrator panel (no password hashes, tokens or phone numbers leave this class).</summary>
internal sealed class UserDirectory(AppDbContext db, TimeProvider time) : IUserDirectory
{
    public async Task<PagedResult<UserListItemDto>> ListAsync(string? emailSearch, string? role, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(emailSearch))
        {
            var normalized = emailSearch.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail != null && u.NormalizedEmail.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleName = role.Trim();
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == roleName)));
        }

        var total = await query.CountAsync(cancellationToken);
        var now = time.GetUtcNow();
        var users = await query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(u => new { u.Id, u.Email, u.CreatedAt, u.LockoutEnabled, u.LockoutEnd })
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();
        var roles = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ids.Contains(ur.UserId)
                select new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new UserListItemDto(
            u.Id,
            u.Email ?? string.Empty,
            [.. roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).Order(StringComparer.Ordinal)],
            u.LockoutEnabled && u.LockoutEnd is { } end && end > now,
            u.CreatedAt)).ToList();
        return new PagedResult<UserListItemDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.Email != null)
                .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => db.Users.AsNoTracking().CountAsync(cancellationToken);
}
