using Cicek.Domain.Ordering;
using FluentValidation;

namespace Cicek.Application.Ordering;

public sealed record CheckoutRequest(
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string District,
    string City,
    string? PostalCode,
    DateOnly DeliveryDate,
    string? GiftMessage)
{
    public DeliveryDetails ToDeliveryDetails() =>
        new(RecipientName, RecipientPhone, AddressLine, District, City, PostalCode ?? string.Empty, DeliveryDate, GiftMessage);
}

public sealed record ReasonRequest(string Reason);

public sealed record RecordPaymentRequest(string Reference);

public sealed record OrderSummaryDto(
    string OrderNumber,
    Guid ShopId,
    string ShopName,
    OrderStatus Status,
    decimal Total,
    string Currency,
    int ItemCount,
    DateOnly DeliveryDate,
    DateTimeOffset CreatedAt);

/// <summary>Result of a checkout. <see cref="Replayed"/> is true when an earlier submission with the same idempotency key was returned.</summary>
public sealed record CheckoutResultDto(Guid CheckoutId, decimal Total, string Currency, IReadOnlyList<OrderSummaryDto> Orders, bool Replayed);

public sealed record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderStatusChangeDto(OrderStatus? From, OrderStatus To, OrderActor Actor, string? Reason, DateTimeOffset OccurredAt);

public sealed record DeliveryDto(
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string District,
    string City,
    string? PostalCode,
    DateOnly DeliveryDate,
    string? GiftMessage);

/// <summary>Commission split; shown to the shop and administrators, never to customers.</summary>
public sealed record OrderFinancialsDto(int CommissionRateBps, decimal CommissionBase, decimal Commission, decimal ShopEarnings);

public sealed record OrderDetailDto(
    string OrderNumber,
    Guid ShopId,
    string ShopName,
    OrderStatus Status,
    string Currency,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    decimal RefundDue,
    DeliveryDto Delivery,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusChangeDto> History,
    /// <summary>Actions the viewing party may perform now (e.g. "cancel", "accept").</summary>
    IReadOnlyList<string> AvailableActions,
    OrderFinancialsDto? Financials,
    string? PaymentReference,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? CompletedAt);

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RecipientPhone).NotEmpty().MaximumLength(32).Matches(@"^\+?[0-9 ()-]{7,}$");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(200);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(16);
        RuleFor(x => x.DeliveryDate).NotEmpty();
        RuleFor(x => x.GiftMessage).MaximumLength(Order.GiftMessageMaxLength);
    }
}

public sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(Order.ReasonMaxLength);
}

public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator() => RuleFor(x => x.Reference).NotEmpty().MaximumLength(Order.PaymentReferenceMaxLength);
}
