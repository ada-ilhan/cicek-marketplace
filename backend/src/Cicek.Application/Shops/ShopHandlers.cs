using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Application.Media;
using Cicek.Domain.Auditing;
using Cicek.Domain.Common;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Application.Shops;

internal static class ShopMapping
{
    public static ShopManagementDto ToManagementDto(this Shop shop, Guid? viewerId, long version) => new(
        shop.Id, shop.Name, shop.Slug, shop.Description, shop.ContactEmail, shop.ContactPhone, shop.AddressLine,
        shop.District, shop.City, shop.PostalCode, shop.ImageUrl, shop.Status, shop.StatusReason, shop.CommissionRateBps,
        shop.CreatedAt, shop.ApprovedAt, shop.Members.FirstOrDefault(m => m.UserId == viewerId)?.Role, version);
}

/// <summary>Any signed-in user can apply to open a shop; it stays hidden until an administrator approves it.</summary>
public sealed class ApplyForShopHandler(IAppDbContext db, TimeProvider time, MediaReferenceValidator media)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid userId, ShopProfileRequest request, CancellationToken ct)
    {
        // No shop exists yet, so no uploaded platform image can legitimately belong to it.
        var images = await media.ValidateAsync(null, [request.ImageUrl], ct);
        if (!images.IsSuccess)
        {
            return images.Error;
        }

        var slug = await UniqueSlugAsync(SearchText.Slugify(request.Name, Shop.SlugMaxLength - 4), ct);
        var shop = Shop.Apply(userId, slug, request.ToProfile(), time.GetUtcNow());
        db.Shops.Add(shop);
        await db.SaveChangesAsync(ct);
        return shop.ToManagementDto(userId, db.GetVersion(shop));
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var taken = await db.Shops.AsNoTracking()
            .Where(s => s.Slug == baseSlug || s.Slug.StartsWith(baseSlug + "-"))
            .Select(s => s.Slug)
            .ToListAsync(ct);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate))
            {
                return candidate; // a concurrent duplicate is still caught by the unique index
            }
        }
    }
}

public sealed class GetMyShopsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ShopManagementDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var shops = await db.Shops.Include(s => s.Members)
            .Where(s => s.Members.Any(m => m.UserId == userId))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        return [.. shops.Select(s => s.ToManagementDto(userId, db.GetVersion(s)))];
    }
}

public sealed class GetMyShopHandler(ShopAccess access, IAppDbContext db)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var shop = await access.RequireAsync(shopId, userId, ShopPermission.ViewShop, ct);
        return shop.IsSuccess ? shop.Value.ToManagementDto(userId, db.GetVersion(shop.Value)) : shop.Error;
    }
}

public sealed class UpdateShopProfileHandler(ShopAccess access, IAppDbContext db, TimeProvider time, MediaReferenceValidator media)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid userId, UpdateShopProfileRequest request, CancellationToken ct)
    {
        var shop = await access.RequireAsync(shopId, userId, ShopPermission.ManageProfile, ct);
        if (!shop.IsSuccess)
        {
            return shop.Error;
        }

        var images = await media.ValidateAsync(shopId, [request.Profile.ImageUrl], ct);
        if (!images.IsSuccess)
        {
            return images.Error;
        }

        db.SetExpectedVersion(shop.Value, request.Version);
        shop.Value.UpdateProfile(request.Profile.ToProfile(), time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return shop.Value.ToManagementDto(userId, db.GetVersion(shop.Value));
    }
}

/// <summary>Owner opens (Inactive → Active) or temporarily closes (Active → Inactive) the shop.</summary>
public sealed class SetShopOpenHandler(ShopAccess access, IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid userId, bool open, CancellationToken ct)
    {
        var shop = await access.RequireAsync(shopId, userId, ShopPermission.ManageProfile, ct);
        if (!shop.IsSuccess)
        {
            return shop.Error;
        }

        if (open)
        {
            shop.Value.Activate(time.GetUtcNow());
        }
        else
        {
            shop.Value.Deactivate(time.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return shop.Value.ToManagementDto(userId, db.GetVersion(shop.Value));
    }
}

public sealed class ListPublicShopsHandler(IAppDbContext db)
{
    public async Task<PagedResult<PublicShopDto>> HandleAsync(string? search, PageRequest page, CancellationToken ct)
    {
        var query = db.Shops.AsNoTracking().Where(s => s.Status == ShopStatus.Active);
        var normalized = SearchText.Normalize(search);
        if (normalized.Length > 0)
        {
            query = query.Where(s => s.NormalizedName.Contains(normalized));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.NormalizedName).ThenBy(s => s.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(s => new PublicShopDto(s.Id, s.Name, s.Slug, s.Description, s.District, s.City, s.ImageUrl))
            .ToListAsync(ct);
        return new PagedResult<PublicShopDto>(items, page.SafePage, page.SafePageSize, total);
    }
}

public sealed class GetPublicShopHandler(IAppDbContext db)
{
    public async Task<Result<PublicShopDto>> HandleAsync(string slug, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking()
            .Where(s => s.Slug == slug && s.Status == ShopStatus.Active)
            .Select(s => new PublicShopDto(s.Id, s.Name, s.Slug, s.Description, s.District, s.City, s.ImageUrl))
            .SingleOrDefaultAsync(ct);
        return shop is null ? ShopAccess.NotFound : shop;
    }
}

// ---- Administration ------------------------------------------------------------------------------

/// <summary>Shop as shown to administrators: management data plus owner contact and catalogue size.</summary>
public sealed record AdminShopDto(ShopManagementDto Shop, string? OwnerEmail, int ActiveProducts);

public sealed class ListShopsForAdminHandler(IAppDbContext db, IUserDirectory users)
{
    public async Task<PagedResult<AdminShopDto>> HandleAsync(ShopStatus? status, string? search, PageRequest page, CancellationToken ct)
    {
        var query = db.Shops.AsNoTracking().Include(s => s.Members).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }

        var normalized = SearchText.Normalize(search);
        if (normalized.Length > 0)
        {
            query = query.Where(x => x.NormalizedName.Contains(normalized));
        }

        var total = await query.CountAsync(ct);
        var shops = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<AdminShopDto>(await ToAdminDtosAsync(db, users, shops, ct), page.SafePage, page.SafePageSize, total);
    }

    internal static async Task<IReadOnlyList<AdminShopDto>> ToAdminDtosAsync(IAppDbContext db, IUserDirectory users, IReadOnlyList<Shop> shops, CancellationToken ct)
    {
        var shopIds = shops.Select(x => x.Id).ToList();
        var productCounts = await db.Products.AsNoTracking()
            .Where(p => shopIds.Contains(p.ShopId) && p.Status == Domain.Catalog.ProductStatus.Active)
            .GroupBy(p => p.ShopId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var ownerIds = shops.Select(x => x.Members.FirstOrDefault(m => m.Role == ShopMemberRole.Owner)?.UserId).OfType<Guid>().Distinct().ToList();
        var emails = await users.GetEmailsAsync(ownerIds, ct);
        return [.. shops.Select(x =>
        {
            var ownerId = x.Members.FirstOrDefault(m => m.Role == ShopMemberRole.Owner)?.UserId;
            return new AdminShopDto(
                x.ToManagementDto(null, 0),
                ownerId is { } id && emails.TryGetValue(id, out var email) ? email : null,
                productCounts.FirstOrDefault(c => c.Key == x.Id)?.Count ?? 0);
        })];
    }
}

public sealed class GetShopForAdminHandler(IAppDbContext db, IUserDirectory users)
{
    public async Task<Result<AdminShopDto>> HandleAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Include(s => s.Members).SingleOrDefaultAsync(s => s.Id == shopId, ct);
        return shop is null ? ShopAccess.NotFound : (await ListShopsForAdminHandler.ToAdminDtosAsync(db, users, [shop], ct))[0];
    }
}

public enum AdminShopAction
{
    Approve,
    Reject,
    Suspend,
    Reinstate,
}

/// <summary>Approve / suspend / reinstate, each recorded in the audit log in the same transaction.</summary>
public sealed class AdminShopActionHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid adminId, AdminShopAction action, string? reason, CancellationToken ct)
    {
        var shop = await db.Shops.Include(s => s.Members).SingleOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null)
        {
            return ShopAccess.NotFound;
        }

        var now = time.GetUtcNow();
        switch (action)
        {
            case AdminShopAction.Approve:
                shop.Approve(now);
                break;
            case AdminShopAction.Reject:
                shop.Reject(reason ?? string.Empty, now);
                break;
            case AdminShopAction.Suspend:
                shop.Suspend(reason ?? string.Empty, now);
                break;
            case AdminShopAction.Reinstate:
                shop.Reinstate(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        db.AuditEntries.Add(new AuditEntry(adminId, $"shop.{action.ToString().ToLowerInvariant()}", "Shop", shop.Id.ToString(), reason, now));
        await db.SaveChangesAsync(ct);
        return shop.ToManagementDto(null, db.GetVersion(shop));
    }
}

public sealed class SetShopCommissionHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid adminId, int? basisPoints, CancellationToken ct)
    {
        var shop = await db.Shops.Include(s => s.Members).SingleOrDefaultAsync(s => s.Id == shopId, ct);
        if (shop is null)
        {
            return ShopAccess.NotFound;
        }

        var now = time.GetUtcNow();
        var previous = shop.CommissionRateBps;
        shop.SetCommissionRate(basisPoints, now);
        db.AuditEntries.Add(new AuditEntry(adminId, "shop.commission_changed", "Shop", shop.Id.ToString(),
            $"{previous?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default"} -> {basisPoints?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default"} bps", now));
        await db.SaveChangesAsync(ct);
        return shop.ToManagementDto(null, db.GetVersion(shop));
    }
}

/// <summary>Owner sends a rejected application back to review after correcting the profile (approval is never bypassed).</summary>
public sealed class ResubmitShopHandler(ShopAccess access, IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ShopManagementDto>> HandleAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var shop = await access.RequireAsync(shopId, userId, ShopPermission.ManageProfile, ct);
        if (!shop.IsSuccess)
        {
            return shop.Error;
        }

        shop.Value.Resubmit(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return shop.Value.ToManagementDto(userId, db.GetVersion(shop.Value));
    }
}
