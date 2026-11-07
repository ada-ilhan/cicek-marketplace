using Cicek.Domain.Commissions;
using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Ordering;

/// <summary>A cart line priced from current product data on the server.</summary>
public sealed record PricedLine(Guid ProductId, Guid ShopId, string ProductName, Money UnitPrice, int Quantity)
{
    public Money LineTotal => UnitPrice.Multiply(Quantity);
}

/// <summary>Everything one shop's order will contain and cost. Total = Subtotal + DeliveryFee − Discount.</summary>
public sealed record ShopOrderQuote(
    Guid ShopId,
    IReadOnlyList<PricedLine> Lines,
    Money Subtotal,
    Money DeliveryFee,
    Money Discount,
    Money Total,
    CommissionBreakdown Commission);

/// <summary>Grand total across shops for one checkout (single currency).</summary>
public sealed record CheckoutQuote(IReadOnlyList<ShopOrderQuote> Orders, Money Total);

/// <summary>
/// Server-side pricing rules for a checkout: lines are grouped by shop; each shop order is charged the flat
/// delivery fee; commission applies to the item subtotal net of discounts. All amounts must share one currency.
/// </summary>
public static class OrderPricing
{
    public static CheckoutQuote Quote(
        IReadOnlyCollection<PricedLine> lines,
        Money deliveryFeePerOrder,
        Func<Guid, CommissionRate> commissionRateForShop)
    {
        if (lines.Count == 0)
        {
            throw new DomainException("checkout.empty", "There is nothing to order.");
        }

        var currency = deliveryFeePerOrder.Currency;
        if (lines.Any(l => l.UnitPrice.Currency != currency))
        {
            throw new DomainException("checkout.currency_mismatch", "All prices must be in the marketplace currency.");
        }

        if (lines.Any(l => l.Quantity <= 0))
        {
            throw new DomainException("checkout.invalid_quantity", "Quantities must be positive.");
        }

        var orders = lines
            .GroupBy(l => l.ShopId)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                var shopLines = group.ToList();
                var subtotal = shopLines.Aggregate(Money.Zero(currency), (sum, l) => sum + l.LineTotal);
                var discount = Money.Zero(currency); // promotions are a later module
                var commission = CommissionCalculator.Calculate(subtotal - discount, commissionRateForShop(group.Key));
                var total = subtotal + deliveryFeePerOrder - discount;
                return new ShopOrderQuote(group.Key, shopLines, subtotal, deliveryFeePerOrder, discount, total, commission);
            })
            .ToList();

        return new CheckoutQuote(orders, orders.Aggregate(Money.Zero(currency), (sum, o) => sum + o.Total));
    }
}
