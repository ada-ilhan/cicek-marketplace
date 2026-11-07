using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Application.Shops;

/// <summary>
/// Store-scoped authorization: every shop-management use case loads the shop through here, so a user can
/// only act on shops they are a member of, with a role that grants the required permission.
/// </summary>
public sealed class ShopAccess(IAppDbContext db)
{
    public static readonly Error NotFound = Error.NotFound("shop.not_found", "Shop not found.");
    public static readonly Error Forbidden = Error.Forbidden("shop.forbidden", "You do not have access to this shop.");

    /// <summary>Loads the shop (tracked, with members) if <paramref name="userId"/> holds <paramref name="permission"/>.</summary>
    public async Task<Result<Shop>> RequireAsync(Guid shopId, Guid userId, ShopPermission permission, CancellationToken cancellationToken)
    {
        var shop = await db.Shops.Include(s => s.Members).SingleOrDefaultAsync(s => s.Id == shopId, cancellationToken);
        if (shop is null)
        {
            return NotFound;
        }

        return shop.HasPermission(userId, permission) ? shop : Forbidden;
    }

    /// <summary>Cheap membership check without loading the shop.</summary>
    public async Task<Result> RequireMembershipAsync(Guid shopId, Guid userId, ShopPermission permission, CancellationToken cancellationToken)
    {
        var membership = await db.ShopMembers.AsNoTracking()
            .Where(m => m.ShopId == shopId && m.UserId == userId)
            .Select(m => new { m.Role })
            .SingleOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return await db.Shops.AnyAsync(s => s.Id == shopId, cancellationToken) ? Forbidden : NotFound;
        }

        return ShopRolePermissions.Allows(membership.Role, permission) ? Result.Success() : Forbidden;
    }
}
