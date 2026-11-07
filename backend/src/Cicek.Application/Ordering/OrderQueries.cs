using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Application.Ordering;

public enum OrderViewer
{
    Customer,
    Shop,
    Administrator,
}

/// <summary>Shared loading and mapping for order queries (one round trip per page; no lazy loading).</summary>
public sealed class OrderReader(IAppDbContext db)
{
    public Task<Order?> LoadAsync(string orderNumber, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).Include(o => o.History)
            .SingleOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public static async Task<PagedResult<OrderSummaryDto>> PageAsync(IQueryable<Order> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(o => new OrderSummaryDto(
                o.OrderNumber, o.ShopId, o.ShopName, o.Status, o.TotalAmount, o.CurrencyCode,
                o.Items.Sum(i => i.Quantity), o.DeliveryDate, o.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<OrderSummaryDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public static OrderDetailDto ToDetail(Order o, OrderViewer viewer) => new(
        o.OrderNumber, o.ShopId, o.ShopName, o.Status, o.CurrencyCode,
        o.SubtotalAmount, o.DeliveryFeeAmount, o.DiscountAmount, o.TotalAmount, o.RefundDueAmount,
        new DeliveryDto(o.RecipientName, o.RecipientPhone, o.AddressLine, o.District, o.City, o.PostalCode, o.DeliveryDate, o.GiftMessage),
        [.. o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPriceAmount, i.Quantity, i.LineTotalAmount))],
        [.. o.History.OrderBy(h => h.OccurredAt).ThenBy(h => h.Id).Select(h => new OrderStatusChangeDto(h.FromStatus, h.ToStatus, h.Actor, h.Reason, h.OccurredAt))],
        AvailableActions(o.Status, viewer),
        viewer == OrderViewer.Customer
            ? null
            : new OrderFinancialsDto(o.CommissionRateBps, o.CommissionBaseAmount, o.CommissionAmount, o.ShopEarningsAmount),
        viewer == OrderViewer.Customer ? null : o.PaymentReference,
        o.CancellationReason, o.CreatedAt, o.PaidAt, o.CompletedAt);

    /// <summary>Mirrors the domain rules so clients can show only valid buttons (the domain still enforces them).</summary>
    public static IReadOnlyList<string> AvailableActions(OrderStatus status, OrderViewer viewer) => (viewer, status) switch
    {
        (OrderViewer.Customer, OrderStatus.PendingPayment or OrderStatus.Paid) => ["cancel"],
        (OrderViewer.Shop, OrderStatus.Paid) => ["accept", "reject"],
        (OrderViewer.Shop, OrderStatus.Accepted) => ["start-preparation"],
        (OrderViewer.Shop, OrderStatus.Preparing) => ["mark-ready"],
        (OrderViewer.Shop, OrderStatus.ReadyForDelivery) => ["mark-delivered"],
        (OrderViewer.Administrator, OrderStatus.PendingPayment) => ["record-payment", "cancel"],
        (OrderViewer.Administrator, _) when !OrderStatusTransitions.IsTerminal(status) => ["cancel"],
        _ => [],
    };
}
