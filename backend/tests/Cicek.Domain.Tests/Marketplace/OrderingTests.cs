using Cicek.Domain.Commissions;
using Cicek.Domain.Ordering;
using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;
using static Cicek.Domain.Tests.Marketplace.TestData;

namespace Cicek.Domain.Tests.Marketplace;

public sealed class CommissionTests
{
    [Theory]
    [InlineData("200.00", 1500, "30.00", "170.00")]
    [InlineData("99.99", 1250, "12.50", "87.49")] // 12.49875 → 12.50 (half away from zero)
    [InlineData("10.00", 0, "0.00", "10.00")]
    [InlineData("10.00", 10_000, "10.00", "0.00")]
    [InlineData("0.03", 1500, "0.00", "0.03")] // 0.0045 → 0.00
    public void Commission_is_rounded_and_earnings_take_the_exact_remainder(string baseAmount, int bps, string commission, string earnings)
    {
        var result = CommissionCalculator.Calculate(Tl(Dec(baseAmount)), CommissionRate.FromBasisPoints(bps));

        Assert.Equal(Dec(commission), result.Commission.Amount);
        Assert.Equal(Dec(earnings), result.ShopEarnings.Amount);
        Assert.Equal(result.Base, result.Commission + result.ShopEarnings);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Rate_must_be_within_0_and_100_percent(int bps)
    {
        Assert.Throws<DomainException>(() => CommissionRate.FromBasisPoints(bps));
    }

    [Fact]
    public void Shop_override_wins_over_platform_default()
    {
        var platform = CommissionRate.FromBasisPoints(1500);

        Assert.Equal(1000, CommissionCalculator.ResolveRate(1000, platform).BasisPoints);
        Assert.Equal(1500, CommissionCalculator.ResolveRate(null, platform).BasisPoints);
    }

    [Fact]
    public void Negative_base_is_rejected()
    {
        Assert.Throws<DomainException>(() => CommissionCalculator.Calculate(Tl(-1m), CommissionRate.FromBasisPoints(1)));
    }

    private static decimal Dec(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class OrderPricingTests
{
    [Fact]
    public void Checkout_is_split_per_shop_with_delivery_fee_and_commission_each()
    {
        var shopA = Guid.NewGuid();
        var shopB = Guid.NewGuid();
        var lines = new[]
        {
            new PricedLine(Guid.NewGuid(), shopA, "Güller", Tl(150m), 2),
            new PricedLine(Guid.NewGuid(), shopA, "Kart", Tl(25.50m), 1),
            new PricedLine(Guid.NewGuid(), shopB, "Orkide", Tl(600m), 1),
        };

        var quote = OrderPricing.Quote(lines, Tl(40m), shop => CommissionRate.FromBasisPoints(shop == shopA ? 1000 : 2000));

        var a = quote.Orders.Single(o => o.ShopId == shopA);
        var b = quote.Orders.Single(o => o.ShopId == shopB);
        Assert.Equal(325.50m, a.Subtotal.Amount);
        Assert.Equal(365.50m, a.Total.Amount);
        Assert.Equal(32.55m, a.Commission.Commission.Amount);
        Assert.Equal(292.95m, a.Commission.ShopEarnings.Amount);
        Assert.Equal(640m, b.Total.Amount);
        Assert.Equal(120m, b.Commission.Commission.Amount);
        Assert.Equal(1005.50m, quote.Total.Amount);
    }

    [Fact]
    public void Mixed_currencies_are_rejected()
    {
        var lines = new[] { new PricedLine(Guid.NewGuid(), Guid.NewGuid(), "Rose", new Money(10m, Currency.FromCode("EUR")), 1) };

        var ex = Assert.Throws<DomainException>(() => OrderPricing.Quote(lines, Tl(0m), _ => CommissionRate.FromBasisPoints(0)));
        Assert.Equal("checkout.currency_mismatch", ex.Code);
    }

    [Fact]
    public void Empty_checkout_is_rejected()
    {
        Assert.Throws<DomainException>(() => OrderPricing.Quote([], Tl(0m), _ => CommissionRate.FromBasisPoints(0)));
    }
}

public sealed class OrderLifecycleTests
{
    private static readonly Guid ShopUser = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void Placed_order_snapshots_prices_and_starts_pending_payment()
    {
        var order = PendingOrder(unitPrice: 100m, quantity: 2);

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(200m, order.SubtotalAmount);
        Assert.Equal(230m, order.TotalAmount);
        Assert.Equal(1500, order.CommissionRateBps);
        Assert.Equal(30m, order.CommissionAmount);
        Assert.Equal(170m, order.ShopEarningsAmount);
        var item = Assert.Single(order.Items);
        Assert.Equal("Kırmızı Güller", item.ProductName);
        Assert.Equal(100m, item.UnitPriceAmount);
        Assert.Single(order.History);
    }

    [Fact]
    public void Happy_path_records_every_transition()
    {
        var order = PendingOrder();

        order.MarkPaid("BANK-123", OrderActor.Administrator, Admin, Now);
        order.Accept(ShopUser, Now);
        order.StartPreparation(ShopUser, Now);
        order.MarkReadyForDelivery(ShopUser, Now);
        order.MarkDelivered(ShopUser, Now);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(6, order.History.Count);
        Assert.Equal(
            [OrderStatus.PendingPayment, OrderStatus.Paid, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForDelivery, OrderStatus.Delivered],
            order.History.Select(h => h.ToStatus));
        Assert.False(order.ReleasesStock);
        Assert.Equal(0m, order.RefundDueAmount);
    }

    [Fact]
    public void Shop_cannot_accept_an_unpaid_order()
    {
        var order = PendingOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.Accept(ShopUser, Now));
        Assert.Equal(OrderStatus.PendingPayment, ex.From);
    }

    [Fact]
    public void Steps_cannot_be_skipped_and_terminal_states_are_final()
    {
        var order = PendingOrder();
        order.MarkPaid("REF", OrderActor.Administrator, Admin, Now);

        Assert.Throws<InvalidOrderTransitionException>(() => order.MarkDelivered(ShopUser, Now));

        order.Reject("Out of roses", ShopUser, Now);
        Assert.Throws<InvalidOrderTransitionException>(() => order.Accept(ShopUser, Now));
        Assert.Throws<InvalidOrderTransitionException>(() => order.Cancel("x", OrderActor.Administrator, Admin, Now));
    }

    [Fact]
    public void Customer_can_cancel_before_acceptance_but_not_after()
    {
        var unpaid = PendingOrder();
        unpaid.Cancel("Changed my mind", OrderActor.Customer, unpaid.CustomerId, Now);
        Assert.Equal(OrderStatus.Cancelled, unpaid.Status);
        Assert.True(unpaid.ReleasesStock);
        Assert.Equal(0m, unpaid.RefundDueAmount);

        var accepted = PendingOrder();
        accepted.MarkPaid("REF", OrderActor.Administrator, Admin, Now);
        accepted.Accept(ShopUser, Now);
        var ex = Assert.Throws<DomainException>(() => accepted.Cancel("Too late", OrderActor.Customer, accepted.CustomerId, Now));
        Assert.Equal("order.cancel_not_allowed", ex.Code);
    }

    [Fact]
    public void Cancelling_or_rejecting_a_paid_order_records_the_refund_due()
    {
        var paid = PendingOrder();
        paid.MarkPaid("REF", OrderActor.Administrator, Admin, Now);
        paid.Cancel("Customer request", OrderActor.Customer, paid.CustomerId, Now);
        Assert.Equal(paid.TotalAmount, paid.RefundDueAmount);

        var rejected = PendingOrder();
        rejected.MarkPaid("REF", OrderActor.Administrator, Admin, Now);
        rejected.Reject("Closed", ShopUser, Now);
        Assert.Equal(rejected.TotalAmount, rejected.RefundDueAmount);
        Assert.True(rejected.ReleasesStock);
    }

    [Fact]
    public void Administrator_can_cancel_an_accepted_order()
    {
        var order = PendingOrder();
        order.MarkPaid("REF", OrderActor.Administrator, Admin, Now);
        order.Accept(ShopUser, Now);

        order.Cancel("Fraud", OrderActor.Administrator, Admin, Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Only_administrators_or_the_system_record_payments_and_only_unpaid_orders_expire()
    {
        var order = PendingOrder();
        Assert.Throws<DomainException>(() => order.MarkPaid("REF", OrderActor.Shop, ShopUser, Now));
        Assert.Throws<DomainException>(() => order.MarkPaid("REF", OrderActor.Customer, order.CustomerId, Now));

        order.MarkPaid("REF", OrderActor.Administrator, Admin, Now);
        Assert.Throws<InvalidOrderTransitionException>(() => order.Expire(Now));

        var unpaid = PendingOrder();
        unpaid.Expire(Now);
        Assert.Equal(OrderStatus.Expired, unpaid.Status);
        Assert.Equal(OrderActor.System, unpaid.History[^1].Actor);
    }

    [Fact]
    public void Every_status_has_a_defined_transition_set()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            Assert.NotNull(OrderStatusTransitions.NextStatuses(status));
        }
    }

    [Fact]
    public void Order_numbers_are_well_formed_and_distinct()
    {
        var numbers = Enumerable.Range(0, 1000).Select(_ => OrderNumber.Create(Now)).ToList();

        Assert.All(numbers, n => Assert.True(OrderNumber.IsWellFormed(n), n));
        Assert.StartsWith("CS261013-", numbers[0], StringComparison.Ordinal);
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.False(OrderNumber.IsWellFormed("CS261013-ILOU00"));
    }

    [Theory]
    [InlineData("3f2b6a1e-0c1d-4f5e-9a8b-7c6d5e4f3a2b", true)]
    [InlineData("short", false)]
    [InlineData("has space in it!", false)]
    public void Idempotency_keys_are_validated(string key, bool valid)
    {
        Assert.Equal(valid, Checkout.IsValidIdempotencyKey(key));
    }
}
