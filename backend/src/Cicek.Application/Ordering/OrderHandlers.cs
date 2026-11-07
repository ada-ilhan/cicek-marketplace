using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Application.Shops;
using Cicek.Domain.Auditing;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Ordering;

public static class OrderErrors
{
    /// <summary>Also returned for orders that exist but belong to someone else, so order numbers cannot be probed.</summary>
    public static readonly Error NotFound = Error.NotFound("order.not_found", "Order not found.");
}

/// <summary>
/// Applies a status transition and persists it in one transaction. When the order ends without delivery,
/// its items are returned to stock in the same transaction. A concurrent change to the same order fails
/// the optimistic concurrency check and rolls back (including the stock release).
/// </summary>
public sealed class OrderTransitionRunner(IAppDbContext db, IProductStock stock)
{
    public async Task RunAsync(Order order, Action<Order> transition, AuditEntry? audit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        transition(order);
        if (order.ReleasesStock)
        {
            foreach (var item in order.Items)
            {
                await stock.ReleaseAsync(item.ProductId, item.Quantity, ct);
            }
        }

        if (audit is not null)
        {
            db.AuditEntries.Add(audit);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

// ---- Customer ----------------------------------------------------------------------------------------

public sealed class ListCustomerOrdersHandler(IAppDbContext db)
{
    public Task<PagedResult<OrderSummaryDto>> HandleAsync(Guid customerId, OrderStatus? status, PageRequest page, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        return OrderReader.PageAsync(query, page, ct);
    }
}

public sealed class GetCustomerOrderHandler(OrderReader reader)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(Guid customerId, string orderNumber, CancellationToken ct)
    {
        var order = await reader.LoadAsync(orderNumber, ct);
        return order is null || !order.IsOwnedByCustomer(customerId)
            ? OrderErrors.NotFound
            : OrderReader.ToDetail(order, OrderViewer.Customer);
    }
}

public sealed class CancelCustomerOrderHandler(OrderReader reader, OrderTransitionRunner runner, TimeProvider time)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(Guid customerId, string orderNumber, string reason, CancellationToken ct)
    {
        var order = await reader.LoadAsync(orderNumber, ct);
        if (order is null || !order.IsOwnedByCustomer(customerId))
        {
            return OrderErrors.NotFound;
        }

        await runner.RunAsync(order, o => o.Cancel(reason, OrderActor.Customer, customerId, time.GetUtcNow()), null, ct);
        return OrderReader.ToDetail(order, OrderViewer.Customer);
    }
}

// ---- Shop (store-scoped) -----------------------------------------------------------------------------

public enum ShopOrderAction
{
    Accept,
    Reject,
    StartPreparation,
    MarkReady,
    MarkDelivered,
}

public sealed class ListShopOrdersHandler(ShopAccess access, IAppDbContext db)
{
    public async Task<Result<PagedResult<OrderSummaryDto>>> HandleAsync(Guid shopId, Guid userId, OrderStatus? status, PageRequest page, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageOrders, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var query = db.Orders.AsNoTracking().Where(o => o.ShopId == shopId);
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        return await OrderReader.PageAsync(query, page, ct);
    }
}

public sealed class GetShopOrderHandler(ShopAccess access, OrderReader reader)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(Guid shopId, Guid userId, string orderNumber, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageOrders, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var order = await reader.LoadAsync(orderNumber, ct);
        return order is null || order.ShopId != shopId ? OrderErrors.NotFound : OrderReader.ToDetail(order, OrderViewer.Shop);
    }
}

public sealed class ShopOrderActionHandler(ShopAccess access, OrderReader reader, OrderTransitionRunner runner, TimeProvider time)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(
        Guid shopId, Guid userId, string orderNumber, ShopOrderAction action, string? reason, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageOrders, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var order = await reader.LoadAsync(orderNumber, ct);
        if (order is null || order.ShopId != shopId)
        {
            return OrderErrors.NotFound;
        }

        var now = time.GetUtcNow();
        Action<Order> transition = action switch
        {
            ShopOrderAction.Accept => o => o.Accept(userId, now),
            ShopOrderAction.Reject => o => o.Reject(reason ?? string.Empty, userId, now),
            ShopOrderAction.StartPreparation => o => o.StartPreparation(userId, now),
            ShopOrderAction.MarkReady => o => o.MarkReadyForDelivery(userId, now),
            ShopOrderAction.MarkDelivered => o => o.MarkDelivered(userId, now),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        await runner.RunAsync(order, transition, null, ct);
        return OrderReader.ToDetail(order, OrderViewer.Shop);
    }
}

// ---- Administration ----------------------------------------------------------------------------------

public sealed class ListOrdersForAdminHandler(IAppDbContext db)
{
    public Task<PagedResult<OrderSummaryDto>> HandleAsync(OrderStatus? status, Guid? shopId, PageRequest page, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        if (shopId is { } id)
        {
            query = query.Where(o => o.ShopId == id);
        }

        return OrderReader.PageAsync(query, page, ct);
    }
}

public sealed class GetOrderForAdminHandler(OrderReader reader)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(string orderNumber, CancellationToken ct)
    {
        var order = await reader.LoadAsync(orderNumber, ct);
        return order is null ? OrderErrors.NotFound : OrderReader.ToDetail(order, OrderViewer.Administrator);
    }
}

/// <summary>Manual payment confirmation (e.g. bank transfer) by an administrator — audited. Not a payment-provider integration.</summary>
public sealed class RecordManualPaymentHandler(OrderReader reader, OrderTransitionRunner runner, TimeProvider time)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(Guid adminId, string orderNumber, string reference, CancellationToken ct)
    {
        var order = await reader.LoadAsync(orderNumber, ct);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var now = time.GetUtcNow();
        var audit = new AuditEntry(adminId, "order.payment_recorded", "Order", order.OrderNumber, $"reference={reference}; amount={order.TotalAmount} {order.CurrencyCode}", now);
        await runner.RunAsync(order, o => o.MarkPaid(reference, OrderActor.Administrator, adminId, now), audit, ct);
        return OrderReader.ToDetail(order, OrderViewer.Administrator);
    }
}

public sealed class AdminCancelOrderHandler(OrderReader reader, OrderTransitionRunner runner, TimeProvider time)
{
    public async Task<Result<OrderDetailDto>> HandleAsync(Guid adminId, string orderNumber, string reason, CancellationToken ct)
    {
        var order = await reader.LoadAsync(orderNumber, ct);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var now = time.GetUtcNow();
        var audit = new AuditEntry(adminId, "order.cancelled_by_admin", "Order", order.OrderNumber, reason, now);
        await runner.RunAsync(order, o => o.Cancel(reason, OrderActor.Administrator, adminId, now), audit, ct);
        return OrderReader.ToDetail(order, OrderViewer.Administrator);
    }
}

/// <summary>
/// Expires unpaid orders older than <see cref="MarketplaceOptions.PaymentTimeout"/> and releases their stock.
/// Each order is its own transaction; an order changed concurrently (e.g. just paid) is skipped.
/// </summary>
public sealed class ExpireUnpaidOrdersHandler(
    IAppDbContext db,
    OrderTransitionRunner runner,
    TimeProvider time,
    IOptions<MarketplaceOptions> options,
    ILogger<ExpireUnpaidOrdersHandler> logger)
{
    public async Task<int> HandleAsync(int batchSize, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cutoff = now - options.Value.PaymentTimeout;
        var ids = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt < cutoff)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in ids)
        {
            var order = await db.Orders.Include(o => o.Items).Include(o => o.History).SingleAsync(o => o.Id == id, ct);
            try
            {
                await runner.RunAsync(order, o => o.Expire(now), null, ct);
                expired++;
            }
            catch (Exception ex) when (ex is ConcurrencyConflictException or InvalidOrderTransitionException)
            {
                logger.LogInformation("Order {OrderNumber} changed while expiring; skipped.", order.OrderNumber);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return expired;
    }
}
