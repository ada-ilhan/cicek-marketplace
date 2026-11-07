using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Catalog;
using Cicek.Domain.Shops;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Conditional single-statement stock updates (ADR-0012). The reservation is
/// <c>UPDATE CAT_PRODUCTS SET STOCK_QUANTITY = STOCK_QUANTITY - :q, ROW_VERSION = ROW_VERSION + 1
/// WHERE ID = :id AND STATUS = 'Active' AND PRICE_AMOUNT = :price AND STOCK_QUANTITY &gt;= :q
/// AND EXISTS (active shop)</c>.
/// Oracle locks the row for the statement; a concurrent request re-evaluates the WHERE clause after the first
/// commits, so stock can never go below zero (also guarded by CK_CAT_PRODUCTS_STOCK). Including the price and the
/// shop status closes the window between quoting the order and reserving stock: a price edit or a closed shop in
/// that window makes the reservation fail instead of selling at a stale price. Runs inside the caller's transaction.
/// </summary>
internal sealed class ProductStock(AppDbContext db) : IProductStock
{
    public async Task<StockReservation> TryReserveAsync(Guid productId, int quantity, decimal expectedUnitPrice, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        var updated = await db.Products
            .Where(p => p.Id == productId
                && p.Status == ProductStatus.Active
                && p.PriceAmount == expectedUnitPrice
                && p.StockQuantity >= quantity
                && db.Shops.Any(s => s.Id == p.ShopId && s.Status == ShopStatus.Active))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity)
                    .SetProperty(p => EF.Property<long>(p, AppDbContext.RowVersionProperty), p => EF.Property<long>(p, AppDbContext.RowVersionProperty) + 1),
                cancellationToken);
        if (updated == 1)
        {
            return StockReservation.Reserved;
        }

        // Nothing was updated: find out why, so the customer gets an accurate message. The diagnosis read is not
        // part of the atomic decision above; it only classifies the failure.
        var state = await (
                from p in db.Products.AsNoTracking()
                join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
                where p.Id == productId
                select new { p.Status, p.PriceAmount, p.StockQuantity, ShopStatus = s.Status })
            .SingleOrDefaultAsync(cancellationToken);

        return state switch
        {
            null => StockReservation.Unavailable,
            { Status: not ProductStatus.Active } or { ShopStatus: not ShopStatus.Active } => StockReservation.Unavailable,
            _ when state.PriceAmount != expectedUnitPrice => StockReservation.PriceChanged,
            _ => StockReservation.InsufficientStock,
        };
    }

    public async Task ReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        await db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                    .SetProperty(p => EF.Property<long>(p, AppDbContext.RowVersionProperty), p => EF.Property<long>(p, AppDbContext.RowVersionProperty) + 1),
                cancellationToken);
    }
}
