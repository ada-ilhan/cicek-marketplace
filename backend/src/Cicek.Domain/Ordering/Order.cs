using Cicek.Domain.Common;
using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Ordering;

/// <summary>Delivery information captured at checkout (validated by the application layer).</summary>
public sealed record DeliveryDetails(
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string District,
    string City,
    string PostalCode,
    DateOnly DeliveryDate,
    string? GiftMessage);

/// <summary>
/// One shop's part of a checkout. Holds immutable snapshots (product names, prices, commission rate and
/// amounts) so later catalog or rate changes never alter a placed order. Status changes go through methods
/// that enforce <see cref="OrderStatusTransitions"/> and the actor rules below, and append to the history.
/// </summary>
public sealed class Order : Entity
{
    public const int ReasonMaxLength = 500;
    public const int PaymentReferenceMaxLength = 100;
    public const int GiftMessageMaxLength = 500;

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _history = [];

    private Order()
    {
    }

    private Order(Guid id)
        : base(id)
    {
    }

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid CheckoutId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ShopId { get; private set; }

    public string ShopName { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public decimal SubtotalAmount { get; private set; }

    public decimal DeliveryFeeAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public int CommissionRateBps { get; private set; }

    public decimal CommissionBaseAmount { get; private set; }

    public decimal CommissionAmount { get; private set; }

    public decimal ShopEarningsAmount { get; private set; }

    /// <summary>Amount owed back to the customer after a paid order was cancelled or rejected (refunds: later stage).</summary>
    public decimal RefundDueAmount { get; private set; }

    public string RecipientName { get; private set; } = string.Empty;

    public string RecipientPhone { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string District { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string? PostalCode { get; private set; }

    public DateOnly DeliveryDate { get; private set; }

    public string? GiftMessage { get; private set; }

    public string? PaymentReference { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusChange> History => _history;

    public Currency Currency => Currency.FromCode(CurrencyCode);

    public Money Total => new(TotalAmount, Currency);

    /// <summary>True when the order ended without delivery, so its items return to stock.</summary>
    public bool ReleasesStock => Status is OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired;

    public static Order Place(
        string orderNumber,
        Guid checkoutId,
        Guid customerId,
        string shopName,
        ShopOrderQuote quote,
        DeliveryDetails delivery,
        DateTimeOffset now)
    {
        if (!Ordering.OrderNumber.IsWellFormed(orderNumber))
        {
            throw new DomainException("order.invalid_number", "Order number is malformed.");
        }

        if (customerId == Guid.Empty || checkoutId == Guid.Empty)
        {
            throw new DomainException("order.missing_reference", "Order requires a customer and a checkout.");
        }

        var order = new Order(Guid.CreateVersion7())
        {
            OrderNumber = orderNumber,
            CheckoutId = checkoutId,
            CustomerId = customerId,
            ShopId = quote.ShopId,
            ShopName = Guard.RequireText(shopName, 100, "order.shop_name_required"),
            Status = OrderStatus.PendingPayment,
            CurrencyCode = quote.Total.Currency.Code,
            SubtotalAmount = quote.Subtotal.Amount,
            DeliveryFeeAmount = quote.DeliveryFee.Amount,
            DiscountAmount = quote.Discount.Amount,
            TotalAmount = quote.Total.Amount,
            CommissionRateBps = quote.Commission.Rate.BasisPoints,
            CommissionBaseAmount = quote.Commission.Base.Amount,
            CommissionAmount = quote.Commission.Commission.Amount,
            ShopEarningsAmount = quote.Commission.ShopEarnings.Amount,
            RecipientName = Guard.RequireText(delivery.RecipientName, 100, "order.recipient_required"),
            RecipientPhone = Guard.RequireText(delivery.RecipientPhone, 32, "order.phone_required"),
            AddressLine = Guard.RequireText(delivery.AddressLine, 200, "order.address_required"),
            District = Guard.RequireText(delivery.District, 100, "order.district_required"),
            City = Guard.RequireText(delivery.City, 100, "order.city_required"),
            PostalCode = Guard.OptionalText(delivery.PostalCode, 16),
            DeliveryDate = delivery.DeliveryDate,
            GiftMessage = Guard.OptionalText(delivery.GiftMessage, GiftMessageMaxLength),
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var line in quote.Lines)
        {
            order._items.Add(new OrderItem(order.Id, line));
        }

        order._history.Add(new OrderStatusChange(order.Id, null, OrderStatus.PendingPayment, OrderActor.Customer, customerId, null, now));
        return order;
    }

    /// <summary>Records a confirmed payment. Today only an administrator can do this (manual payment); a payment provider later.</summary>
    public void MarkPaid(string paymentReference, OrderActor actor, Guid? actorUserId, DateTimeOffset now)
    {
        RequireActor(actor, OrderActor.Administrator, OrderActor.System);
        PaymentReference = Guard.RequireText(paymentReference, PaymentReferenceMaxLength, "order.payment_reference_required");
        PaidAt = now;
        TransitionTo(OrderStatus.Paid, actor, actorUserId, null, now);
    }

    public void Accept(Guid actorUserId, DateTimeOffset now) => ShopAction(OrderStatus.Accepted, actorUserId, null, now);

    public void Reject(string reason, Guid actorUserId, DateTimeOffset now)
    {
        var text = Guard.RequireText(reason, ReasonMaxLength, "order.reason_required");
        EndWithoutDelivery(OrderStatus.Rejected, OrderActor.Shop, actorUserId, text, now);
    }

    public void StartPreparation(Guid actorUserId, DateTimeOffset now) => ShopAction(OrderStatus.Preparing, actorUserId, null, now);

    public void MarkReadyForDelivery(Guid actorUserId, DateTimeOffset now) => ShopAction(OrderStatus.ReadyForDelivery, actorUserId, null, now);

    /// <summary>Until courier integration exists, the shop confirms delivery itself.</summary>
    public void MarkDelivered(Guid actorUserId, DateTimeOffset now)
    {
        ShopAction(OrderStatus.Delivered, actorUserId, null, now);
        CompletedAt = now;
    }

    /// <summary>
    /// Customers may cancel until the shop accepts (PendingPayment or Paid); administrators may cancel any
    /// non-terminal order. Shops reject instead of cancelling.
    /// </summary>
    public void Cancel(string reason, OrderActor actor, Guid? actorUserId, DateTimeOffset now)
    {
        RequireActor(actor, OrderActor.Customer, OrderActor.Administrator);
        if (actor == OrderActor.Customer && Status is not (OrderStatus.PendingPayment or OrderStatus.Paid))
        {
            throw new DomainException("order.cancel_not_allowed", "The shop has already accepted this order; contact support to cancel it.");
        }

        EndWithoutDelivery(OrderStatus.Cancelled, actor, actorUserId, Guard.RequireText(reason, ReasonMaxLength, "order.reason_required"), now);
    }

    /// <summary>Unpaid orders expire so their stock is released.</summary>
    public void Expire(DateTimeOffset now) =>
        EndWithoutDelivery(OrderStatus.Expired, OrderActor.System, null, "Payment not received in time.", now);

    public bool IsOwnedByCustomer(Guid customerId) => CustomerId == customerId;

    private void ShopAction(OrderStatus target, Guid actorUserId, string? reason, DateTimeOffset now) =>
        TransitionTo(target, OrderActor.Shop, actorUserId, reason, now);

    private void EndWithoutDelivery(OrderStatus target, OrderActor actor, Guid? actorUserId, string reason, DateTimeOffset now)
    {
        var wasPaid = OrderStatusTransitions.IsPaidStatus(Status);
        TransitionTo(target, actor, actorUserId, reason, now);
        CancellationReason = reason;
        CompletedAt = now;
        if (wasPaid)
        {
            RefundDueAmount = TotalAmount;
        }
    }

    private void TransitionTo(OrderStatus target, OrderActor actor, Guid? actorUserId, string? reason, DateTimeOffset now)
    {
        if (!OrderStatusTransitions.IsAllowed(Status, target))
        {
            throw new InvalidOrderTransitionException(Status, target);
        }

        _history.Add(new OrderStatusChange(Id, Status, target, actor, actorUserId, reason, now));
        Status = target;
        UpdatedAt = now;
    }

    private static void RequireActor(OrderActor actor, params OrderActor[] allowed)
    {
        if (!allowed.Contains(actor))
        {
            throw new DomainException("order.actor_not_allowed", $"{actor} cannot perform this action.");
        }
    }
}

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : DomainException("order.invalid_transition", $"An order cannot move from {from} to {to}.")
{
    public OrderStatus From { get; } = from;

    public OrderStatus To { get; } = to;
}

/// <summary>Purchase snapshot of one product line.</summary>
public sealed class OrderItem
{
    internal OrderItem(Guid orderId, PricedLine line)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        ProductId = line.ProductId;
        ProductName = line.ProductName;
        UnitPriceAmount = line.UnitPrice.Amount;
        Quantity = line.Quantity;
        LineTotalAmount = line.LineTotal.Amount;
    }

    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPriceAmount { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotalAmount { get; private set; }
}

/// <summary>Append-only status history entry (audit trail of an order's lifecycle).</summary>
public sealed class OrderStatusChange
{
    internal OrderStatusChange(Guid orderId, OrderStatus? from, OrderStatus to, OrderActor actor, Guid? actorUserId, string? reason, DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        FromStatus = from;
        ToStatus = to;
        Actor = actor;
        ActorUserId = actorUserId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private OrderStatusChange()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public OrderActor Actor { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
