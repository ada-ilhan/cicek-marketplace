using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Domain.Catalog;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Admin;

public sealed record StatusCountDto(string Status, int Count);

/// <summary>Marketplace figures for the administrator dashboard. Amounts are in <see cref="Currency"/> and only include orders in that currency.</summary>
public sealed record AdminOverviewDto(
    string Currency,
    IReadOnlyList<StatusCountDto> ShopsByStatus,
    int ActiveProducts,
    IReadOnlyList<StatusCountDto> OrdersByStatus,
    int OrdersLast7Days,
    int Users,
    /// <summary>Value of orders that are not cancelled, rejected or expired.</summary>
    decimal OrderValue,
    /// <summary>Platform commission on delivered orders.</summary>
    decimal CommissionOnDelivered,
    /// <summary>Total refunds owed to customers (refunds are executed manually until a payment provider exists).</summary>
    decimal RefundsDue);

public sealed class GetAdminOverviewHandler(IAppDbContext db, IUserDirectory users, TimeProvider time, IOptions<MarketplaceOptions> options)
{
    private static readonly OrderStatus[] NotCounted = [OrderStatus.Cancelled, OrderStatus.Rejected, OrderStatus.Expired];

    public async Task<AdminOverviewDto> HandleAsync(CancellationToken ct)
    {
        var shopCounts = await db.Shops.AsNoTracking().GroupBy(s => s.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var orderCounts = await db.Orders.AsNoTracking().GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var since = time.GetUtcNow().AddDays(-7);
        var currency = options.Value.Currency; // money is only summed in the marketplace currency; currencies are never mixed

        // Every status is always present (zero when empty) so dashboards need no special cases.
        var shops = Enum.GetValues<ShopStatus>().Select(s => new StatusCountDto(s.ToString(), shopCounts.FirstOrDefault(c => c.Key == s)?.Count ?? 0)).ToList();
        var orders = Enum.GetValues<OrderStatus>().Select(s => new StatusCountDto(s.ToString(), orderCounts.FirstOrDefault(c => c.Key == s)?.Count ?? 0)).ToList();

        return new AdminOverviewDto(
            options.Value.Currency,
            shops,
            await db.Products.AsNoTracking().CountAsync(p => p.Status == ProductStatus.Active, ct),
            orders,
            await db.Orders.AsNoTracking().CountAsync(o => o.CreatedAt >= since, ct),
            await users.CountAsync(ct),
            await db.Orders.AsNoTracking().Where(o => o.CurrencyCode == currency && !NotCounted.Contains(o.Status)).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m,
            await db.Orders.AsNoTracking().Where(o => o.CurrencyCode == currency && o.Status == OrderStatus.Delivered).SumAsync(o => (decimal?)o.CommissionAmount, ct) ?? 0m,
            await db.Orders.AsNoTracking().Where(o => o.CurrencyCode == currency).SumAsync(o => (decimal?)o.RefundDueAmount, ct) ?? 0m);
    }
}

public sealed class ListUsersHandler(IUserDirectory users)
{
    public Task<PagedResult<UserListItemDto>> HandleAsync(string? emailSearch, string? role, PageRequest page, CancellationToken ct) =>
        users.ListAsync(emailSearch, role, page, ct);
}
