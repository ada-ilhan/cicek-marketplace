using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Common;
using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;
using static Cicek.Domain.Tests.Marketplace.TestData;

namespace Cicek.Domain.Tests.Marketplace;

public sealed class CatalogAndCartTests
{
    [Fact]
    public void Product_is_purchasable_only_when_active_in_stock_and_shop_open()
    {
        var product = Product.Create(Guid.NewGuid(), Category(), "Kırmızı Güller", "", Tl(450m), 3, Now);

        Assert.True(product.IsPurchasable(shopAcceptsOrders: true));
        Assert.False(product.IsPurchasable(shopAcceptsOrders: false));

        product.SetStock(0, Now);
        Assert.False(product.IsPurchasable(true));

        product.SetStock(5, Now);
        product.Deactivate(Now);
        Assert.False(product.IsPurchasable(true));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.005")]
    [InlineData("1000000.01")]
    public void Invalid_prices_are_rejected(string price)
    {
        var amount = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var ex = Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), Category(), "Güller", "", Tl(amount), 1, Now));
        Assert.Equal("product.invalid_price", ex.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Product.MaxStock + 1)]
    public void Invalid_stock_is_rejected(int stock)
    {
        Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), Category(), "Güller", "", Tl(10m), stock, Now));
    }

    [Fact]
    public void New_products_cannot_use_inactive_categories()
    {
        var ex = Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), Category(active: false), "Güller", "", Tl(10m), 1, Now));
        Assert.Equal("product.category_inactive", ex.Code);
    }

    [Fact]
    public void Images_must_be_https_and_are_limited()
    {
        var product = Product.Create(Guid.NewGuid(), Category(), "Güller", "", Tl(10m), 1, Now);

        product.ReplaceImages([("https://cdn.example.com/a.jpg", "Bouquet")], Now);
        Assert.Single(product.Images);

        Assert.Throws<DomainException>(() => product.ReplaceImages([("http://cdn.example.com/a.jpg", null)], Now));
        Assert.Throws<DomainException>(() => product.ReplaceImages([("file:///C:/secret.jpg", null)], Now));
        Assert.Throws<DomainException>(() => product.ReplaceImages([("../../etc/passwd", null)], Now));
        Assert.Throws<DomainException>(() =>
            product.ReplaceImages(Enumerable.Repeat(("https://cdn.example.com/a.jpg", (string?)null), 9).ToList(), Now));
    }

    [Fact]
    public void Cart_sets_quantities_and_enforces_limits()
    {
        var cart = Cart.Create(Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();

        cart.SetQuantity(productId, 2, Now);
        cart.SetQuantity(productId, 5, Now);

        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
        Assert.Throws<DomainException>(() => cart.SetQuantity(productId, 0, Now));
        Assert.Throws<DomainException>(() => cart.SetQuantity(productId, Cart.MaxQuantityPerItem + 1, Now));
        Assert.True(cart.Remove(productId, Now));
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void Cart_limits_distinct_products()
    {
        var cart = Cart.Create(Guid.NewGuid(), Now);
        for (var i = 0; i < Cart.MaxDistinctItems; i++)
        {
            cart.SetQuantity(Guid.NewGuid(), 1, Now);
        }

        Assert.Throws<DomainException>(() => cart.SetQuantity(Guid.NewGuid(), 1, Now));
    }

    [Theory]
    [InlineData("Çiçek Sepeti", "cicek sepeti")]
    [InlineData("IŞIK  ılık İzmir", "isik ilik izmir")]
    [InlineData("Orkideé", "orkidee")]
    public void Search_normalisation_folds_turkish_letters(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Theory]
    [InlineData("Gül Bahçesi & Co.", "gul-bahcesi-co")]
    [InlineData("  Ğüzel  Çiçekler  ", "guzel-cicekler")]
    public void Slugs_are_url_safe(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Slugify(input));
    }

    [Fact]
    public void Money_in_different_currencies_is_never_combined()
    {
        Assert.Throws<InvalidOperationException>(() => Tl(1m) + new Money(1m, Currency.FromCode("EUR")));
    }
}
