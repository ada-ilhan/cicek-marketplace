using System.Collections.Frozen;

namespace Cicek.Domain.Ordering;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Accepted,
    Preparing,
    ReadyForDelivery,
    Delivered,
    Cancelled,
    Rejected,
    Expired,
}

/// <summary>Who performed a transition; used both for authorization rules and the status history.</summary>
public enum OrderActor
{
    Customer,
    Shop,
    Administrator,
    System,
}

/// <summary>
/// The complete set of permitted status transitions (documented in docs/ARCHITECTURE.md §4.2 / PROJECT_STATUS).
/// <code>
/// PendingPayment → Paid | Cancelled | Expired
/// Paid           → Accepted | Rejected | Cancelled
/// Accepted       → Preparing | Cancelled
/// Preparing      → ReadyForDelivery | Cancelled
/// ReadyForDelivery → Delivered | Cancelled
/// Delivered, Cancelled, Rejected, Expired: terminal
/// </code>
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly FrozenDictionary<OrderStatus, FrozenSet<OrderStatus>> Allowed =
        new Dictionary<OrderStatus, FrozenSet<OrderStatus>>
        {
            [OrderStatus.PendingPayment] = Set(OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.Expired),
            [OrderStatus.Paid] = Set(OrderStatus.Accepted, OrderStatus.Rejected, OrderStatus.Cancelled),
            [OrderStatus.Accepted] = Set(OrderStatus.Preparing, OrderStatus.Cancelled),
            [OrderStatus.Preparing] = Set(OrderStatus.ReadyForDelivery, OrderStatus.Cancelled),
            [OrderStatus.ReadyForDelivery] = Set(OrderStatus.Delivered, OrderStatus.Cancelled),
            [OrderStatus.Delivered] = Set(),
            [OrderStatus.Cancelled] = Set(),
            [OrderStatus.Rejected] = Set(),
            [OrderStatus.Expired] = Set(),
        }.ToFrozenDictionary();

    public static bool IsAllowed(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static bool IsTerminal(OrderStatus status) => Allowed[status].Count == 0;

    public static IReadOnlySet<OrderStatus> NextStatuses(OrderStatus from) => Allowed[from];

    /// <summary>Statuses in which the customer has already been charged (refund due if the order ends without delivery).</summary>
    public static bool IsPaidStatus(OrderStatus status) =>
        status is OrderStatus.Paid or OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForDelivery or OrderStatus.Delivered;

    private static FrozenSet<OrderStatus> Set(params OrderStatus[] statuses) => statuses.ToFrozenSet();
}
