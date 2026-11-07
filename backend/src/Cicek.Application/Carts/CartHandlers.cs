using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Commissions;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Carts;

public static class CartErrors
{
    public static readonly Error ProductUnavailable = Error.Conflict("cart.product_unavailable", "This product is not available for purchase.");
    public static readonly Error InsufficientStock = Error.Conflict("cart.insufficient_stock", "Not enough stock for the requested quantity.");
    public static readonly Error ItemNotFound = Error.NotFound("cart.item_not_found", "The product is not in the cart.");
}

/// <summary>A cart line joined with current product and shop data (one query for the whole cart).</summary>
public sealed record CartLineSnapshot(
    Guid ProductId,
    int Quantity,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    string Currency,
    int Stock,
    ProductStatus ProductStatus,
    Guid ShopId,
    string ShopName,
    ShopStatus ShopStatus,
    int? ShopCommissionRateBps)
{
    public CartLineIssue Issue =>
        ShopStatus != ShopStatus.Active ? CartLineIssue.ShopClosed
        : ProductStatus != ProductStatus.Active ? CartLineIssue.ProductUnavailable
        : Stock < Quantity ? CartLineIssue.InsufficientStock
        : CartLineIssue.None;
}

/// <summary>
/// Prices a cart from current database values. Prices and totals are never accepted from the client; the
/// same pricing (domain <see cref="OrderPricing"/>) is used by the cart view and by checkout.
/// </summary>
public sealed class CartPricer(IAppDbContext db, IOptions<MarketplaceOptions> options)
{
    public async Task<IReadOnlyList<CartLineSnapshot>> LoadLinesAsync(Guid customerId, CancellationToken ct) =>
        await (
                from c in db.Carts.AsNoTracking()
                from i in c.Items
                join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
                join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
                where c.CustomerId == customerId
                orderby i.AddedAt
                select new CartLineSnapshot(
                    p.Id, i.Quantity, p.Name,
                    p.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).FirstOrDefault(),
                    p.PriceAmount, p.CurrencyCode, p.StockQuantity, p.Status,
                    s.Id, s.Name, s.Status, s.CommissionRateBps))
            .ToListAsync(ct);

    public CheckoutQuote? Quote(IEnumerable<CartLineSnapshot> purchasable)
    {
        var market = options.Value;
        var lines = purchasable
            .Select(l => new PricedLine(l.ProductId, l.ShopId, l.ProductName, new(l.UnitPrice, SharedKernel.Monetary.Currency.FromCode(l.Currency)), l.Quantity))
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        var rates = purchasable.GroupBy(l => l.ShopId).ToDictionary(g => g.Key, g => g.First().ShopCommissionRateBps);
        return OrderPricing.Quote(lines, market.DeliveryFee, shopId => CommissionCalculator.ResolveRate(rates[shopId], market.DefaultCommissionRate));
    }

    public CartDto ToDto(IReadOnlyList<CartLineSnapshot> lines)
    {
        var currency = options.Value.MarketCurrency.Code;
        var quote = Quote(lines.Where(l => l.Issue == CartLineIssue.None).ToList());
        var groups = lines
            .GroupBy(l => (l.ShopId, l.ShopName))
            .Select(g =>
            {
                var shopQuote = quote?.Orders.FirstOrDefault(o => o.ShopId == g.Key.ShopId);
                return new CartShopGroupDto(
                    g.Key.ShopId,
                    g.Key.ShopName,
                    [.. g.Select(l => new CartLineDto(
                        l.ProductId, l.ProductName, l.ImageUrl, l.UnitPrice, l.Quantity, l.UnitPrice * l.Quantity,
                        l.ProductStatus == ProductStatus.Active && l.ShopStatus == ShopStatus.Active ? Math.Min(l.Stock, Cart.MaxQuantityPerItem) : 0,
                        l.Issue))],
                    shopQuote?.Subtotal.Amount ?? 0m,
                    shopQuote?.DeliveryFee.Amount ?? 0m,
                    shopQuote?.Total.Amount ?? 0m);
            })
            .ToList();

        return new CartDto(
            groups,
            currency,
            quote?.Total.Amount ?? 0m,
            lines.Sum(l => l.Quantity),
            lines.Count > 0 && lines.All(l => l.Issue == CartLineIssue.None));
    }
}

public sealed class GetCartHandler(CartPricer pricer)
{
    public async Task<CartDto> HandleAsync(Guid customerId, CancellationToken ct) =>
        pricer.ToDto(await pricer.LoadLinesAsync(customerId, ct));
}

/// <summary>Adds a product or sets its quantity, after checking it is purchasable in that quantity.</summary>
public sealed class SetCartItemHandler(IAppDbContext db, CartPricer pricer, TimeProvider time, IDatabaseErrorClassifier errors)
{
    public async Task<Result<CartDto>> HandleAsync(Guid customerId, Guid productId, int quantity, CancellationToken ct)
    {
        var product = await (
                from p in db.Products.AsNoTracking()
                join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
                where p.Id == productId
                select new { p.Status, p.StockQuantity, ShopStatus = s.Status })
            .SingleOrDefaultAsync(ct);

        if (product is null || product.Status != ProductStatus.Active || product.ShopStatus != ShopStatus.Active)
        {
            return CartErrors.ProductUnavailable;
        }

        if (product.StockQuantity < quantity)
        {
            return CartErrors.InsufficientStock;
        }

        try
        {
            await SaveAsync(customerId, productId, quantity, ct);
        }
        catch (Exception ex) when (errors.Classify(ex)?.Kind == DatabaseFailureKind.UniqueViolation)
        {
            // Two first-time adds raced to create the cart; the other request created it. Retry once.
            db.ChangeTracker.Clear();
            await SaveAsync(customerId, productId, quantity, ct);
        }

        return pricer.ToDto(await pricer.LoadLinesAsync(customerId, ct));
    }

    private async Task SaveAsync(Guid customerId, Guid productId, int quantity, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cart = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (cart is null)
        {
            cart = Cart.Create(customerId, now);
            db.Carts.Add(cart);
        }

        cart.SetQuantity(productId, quantity, now);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RemoveCartItemHandler(IAppDbContext db, CartPricer pricer, TimeProvider time)
{
    public async Task<Result<CartDto>> HandleAsync(Guid customerId, Guid productId, CancellationToken ct)
    {
        var cart = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (cart is null || !cart.Remove(productId, time.GetUtcNow()))
        {
            return CartErrors.ItemNotFound;
        }

        await db.SaveChangesAsync(ct);
        return pricer.ToDto(await pricer.LoadLinesAsync(customerId, ct));
    }
}

public sealed class ClearCartHandler(IAppDbContext db, TimeProvider time)
{
    public async Task HandleAsync(Guid customerId, CancellationToken ct)
    {
        var cart = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (cart is not null && cart.Items.Count > 0)
        {
            cart.Clear(time.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
    }
}
