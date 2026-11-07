using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Carts;
using Cicek.Application.Common;
using Cicek.Domain.Ordering;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cicek.Application.Ordering;

public static class CheckoutErrors
{
    public static readonly Error InvalidIdempotencyKey = Error.Validation(
        "checkout.invalid_idempotency_key", "Send an Idempotency-Key header of 8–64 characters [A-Za-z0-9-_].");

    public static readonly Error CartEmpty = Error.Validation("checkout.cart_empty", "The cart is empty.");

    public static readonly Error InvalidDeliveryDate = Error.Validation("checkout.invalid_delivery_date", "Choose a delivery date from today up to the allowed window.");

    public static readonly Error ItemsUnavailable = Error.Conflict(
        "checkout.items_unavailable", "Some items in the cart are no longer available. Review the cart and try again.");

    public static Error PriceChanged(string productName) =>
        Error.Conflict("checkout.price_changed", $"The price of \"{productName}\" changed. Review the cart and try again.");

    public static Error InsufficientStock(string productName) =>
        Error.Conflict("checkout.insufficient_stock", $"\"{productName}\" no longer has enough stock.");
}

/// <summary>
/// Turns the cart into one order per shop.
/// <list type="number">
/// <item>Idempotency: the (customer, Idempotency-Key) pair is unique; repeating a request returns the original result.</item>
/// <item>Prices and totals are computed here from current product data (never from the client).</item>
/// <item>One database transaction: atomic conditional stock reservation for every line, checkout + orders insert,
/// cart cleared. Any failure rolls everything back, including reserved stock.</item>
/// </list>
/// No payment is taken here; orders start as PendingPayment.
/// </summary>
public sealed class PlaceCheckoutHandler(
    IAppDbContext db,
    IProductStock stock,
    CartPricer pricer,
    MarketCalendar calendar,
    IDatabaseErrorClassifier errors,
    TimeProvider time,
    ILogger<PlaceCheckoutHandler> logger)
{
    private const int MaxAttempts = 3;

    public async Task<Result<CheckoutResultDto>> HandleAsync(Guid customerId, string? idempotencyKey, CheckoutRequest request, CancellationToken ct)
    {
        if (!Checkout.IsValidIdempotencyKey(idempotencyKey))
        {
            return CheckoutErrors.InvalidIdempotencyKey;
        }

        var key = idempotencyKey!;
        if (await FindExistingAsync(customerId, key, ct) is { } replay)
        {
            return replay;
        }

        if (!calendar.IsValidDeliveryDate(request.DeliveryDate))
        {
            return CheckoutErrors.InvalidDeliveryDate;
        }

        var lines = await pricer.LoadLinesAsync(customerId, ct);
        if (lines.Count == 0)
        {
            return CheckoutErrors.CartEmpty;
        }

        if (lines.Any(l => l.Issue != CartLineIssue.None))
        {
            return CheckoutErrors.ItemsUnavailable;
        }

        var quote = pricer.Quote(lines)!;
        var shopNames = lines.GroupBy(l => l.ShopId).ToDictionary(g => g.Key, g => g.First().ShopName);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await PlaceAsync(customerId, key, request, lines, quote, shopNames, ct);
            }
            catch (Exception ex) when (errors.Classify(ex)?.Kind == DatabaseFailureKind.UniqueViolation && attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();

                // A concurrent request with the same key won the race: return its result.
                if (await FindExistingAsync(customerId, key, ct) is { } concurrent)
                {
                    return concurrent;
                }

                // Otherwise an order-number collision: retry with new numbers.
                logger.LogWarning("Order number collision during checkout; retrying (attempt {Attempt}).", attempt);
            }
        }
    }

    private async Task<Result<CheckoutResultDto>> PlaceAsync(
        Guid customerId,
        string key,
        CheckoutRequest request,
        IReadOnlyList<CartLineSnapshot> lines,
        CheckoutQuote quote,
        Dictionary<Guid, string> shopNames,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var line in lines)
        {
            var reservation = await stock.TryReserveAsync(line.ProductId, line.Quantity, line.UnitPrice, ct);
            if (reservation != StockReservation.Reserved)
            {
                // Rolls back stock already reserved for earlier lines of this checkout.
                await transaction.RollbackAsync(ct);
                return reservation switch
                {
                    StockReservation.PriceChanged => CheckoutErrors.PriceChanged(line.ProductName),
                    StockReservation.Unavailable => CheckoutErrors.ItemsUnavailable,
                    _ => CheckoutErrors.InsufficientStock(line.ProductName),
                };
            }
        }

        var checkout = Checkout.Start(customerId, key, quote.Total, now);
        db.Checkouts.Add(checkout);

        var delivery = request.ToDeliveryDetails();
        var orders = quote.Orders
            .Select(o => Order.Place(OrderNumber.Create(now), checkout.Id, customerId, shopNames[o.ShopId], o, delivery, now))
            .ToList();
        db.Orders.AddRange(orders);

        var cart = await db.Carts.Include(c => c.Items).SingleAsync(c => c.CustomerId == customerId, ct);
        cart.Clear(now);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Checkout {CheckoutId} placed {OrderCount} order(s).", checkout.Id, orders.Count);
        return new CheckoutResultDto(
            checkout.Id, checkout.TotalAmount, checkout.CurrencyCode,
            [.. orders.Select(o => new OrderSummaryDto(o.OrderNumber, o.ShopId, o.ShopName, o.Status, o.TotalAmount, o.CurrencyCode,
                o.Items.Sum(i => i.Quantity), o.DeliveryDate, o.CreatedAt))],
            Replayed: false);
    }

    private async Task<CheckoutResultDto?> FindExistingAsync(Guid customerId, string key, CancellationToken ct)
    {
        var checkout = await db.Checkouts.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customerId && c.IdempotencyKey == key, ct);
        if (checkout is null)
        {
            return null;
        }

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CheckoutId == checkout.Id)
            .OrderBy(o => o.ShopName)
            .Select(o => new OrderSummaryDto(o.OrderNumber, o.ShopId, o.ShopName, o.Status, o.TotalAmount, o.CurrencyCode,
                o.Items.Sum(i => i.Quantity), o.DeliveryDate, o.CreatedAt))
            .ToListAsync(ct);
        return new CheckoutResultDto(checkout.Id, checkout.TotalAmount, checkout.CurrencyCode, orders, Replayed: true);
    }
}
