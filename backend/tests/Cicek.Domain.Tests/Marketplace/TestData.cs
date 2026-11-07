using Cicek.Domain.Catalog;
using Cicek.Domain.Commissions;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Tests.Marketplace;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    public static readonly Currency Try = Currency.FromCode("TRY");

    public static Money Tl(decimal amount) => new(amount, Try);

    public static ShopProfile Profile(string name = "Gül Bahçesi") =>
        new(name, "Taze çiçekler", "shop@example.com", "+905551112233", "Bağdat Cad. 1", "Kadıköy", "İstanbul", "34710");

    public static Shop ActiveShop(Guid owner)
    {
        var shop = Shop.Apply(owner, "gul-bahcesi", Profile(), Now);
        shop.Approve(Now);
        return shop;
    }

    public static Category Category(bool active = true)
    {
        var category = Catalog.Category.Create("Güller", "guller", null, 1);
        if (!active)
        {
            category.Update("Güller", 1, isActive: false);
        }

        return category;
    }

    public static DeliveryDetails Delivery() =>
        new("Ayşe Yılmaz", "+905550000000", "Moda Cad. 5", "Kadıköy", "İstanbul", "34710", new DateOnly(2026, 10, 14), "İyi ki doğdun!");

    public static Order PendingOrder(Guid? customer = null, decimal unitPrice = 100m, int quantity = 2)
    {
        var lines = new[] { new PricedLine(Guid.NewGuid(), Guid.NewGuid(), "Kırmızı Güller", Tl(unitPrice), quantity) };
        var quote = OrderPricing.Quote(lines, Tl(30m), _ => CommissionRate.FromBasisPoints(1500)).Orders[0];
        return Order.Place(OrderNumber.Create(Now), Guid.NewGuid(), customer ?? Guid.NewGuid(), "Gül Bahçesi", quote, Delivery(), Now);
    }
}
