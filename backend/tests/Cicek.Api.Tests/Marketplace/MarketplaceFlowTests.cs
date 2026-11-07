using System.Net;
using System.Net.Http.Json;
using Cicek.Application.Carts;
using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Cicek.Application.Ordering;
using Cicek.Application.Shops;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using static Cicek.Api.Tests.Marketplace.MarketplaceDriver;

namespace Cicek.Api.Tests.Marketplace;

/// <summary>End-to-end business behaviour through HTTP against Oracle (as the least-privilege runtime user).</summary>
[Collection(ApiCollection.Name)]
public sealed class MarketplaceFlowTests(ApiTestHost host)
{
    private readonly MarketplaceDriver _driver = new(host);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Full_order_lifecycle_with_server_side_totals_and_commission_snapshot()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Lifecycle {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Kırmızı Güller", 249.90m, 10);

        var cart = await ReadAsync<CartDto>(await SetCartItemAsync(customer, product.Id, 2));
        Assert.Equal(499.80m + 30m, cart.Total); // 2 × 249.90 + delivery fee 30
        Assert.True(cart.CanCheckout);

        var checkout = await CheckoutOkAsync(customer);
        var orderNumber = Assert.Single(checkout.Orders).OrderNumber;
        Assert.Equal(529.80m, checkout.Total);
        Assert.Equal(OrderStatus.PendingPayment, checkout.Orders[0].Status);

        // Cart is emptied and stock reserved.
        Assert.Empty((await customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Json, Ct))!.Shops);
        var afterCheckout = await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct));
        Assert.Equal(8, afterCheckout.StockQuantity);

        // The shop cannot accept before payment.
        var early = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/orders/{orderNumber}/accept", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("order.invalid_transition", await ProblemCodeAsync(early));

        await _driver.RecordPaymentAsync(orderNumber);
        foreach (var action in new[] { "accept", "start-preparation", "mark-ready", "mark-delivered" })
        {
            var response = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/orders/{orderNumber}/{action}", null, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var shopView = await ReadAsync<OrderDetailDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/orders/{orderNumber}", Ct));
        Assert.Equal(OrderStatus.Delivered, shopView.Status);
        Assert.Equal(6, shopView.History.Count);
        Assert.Equal(1500, shopView.Financials!.CommissionRateBps);
        Assert.Equal(499.80m, shopView.Financials.CommissionBase); // delivery fee excluded
        Assert.Equal(74.97m, shopView.Financials.Commission);
        Assert.Equal(424.83m, shopView.Financials.ShopEarnings);

        var customerView = await ReadAsync<OrderDetailDto>(await customer.GetAsync($"/api/v1/me/orders/{orderNumber}", Ct));
        Assert.Null(customerView.Financials); // commission is never shown to customers
        Assert.Equal("Kırmızı Güller", Assert.Single(customerView.Items).ProductName);
        Assert.Empty(customerView.AvailableActions);
    }

    [Fact]
    public async Task Order_keeps_its_price_snapshot_when_the_product_changes_later()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Snapshot {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Orkide", 500m, 5);
        await SetCartItemAsync(customer, product.Id, 1);
        var orderNumber = (await CheckoutOkAsync(customer)).Orders[0].OrderNumber;

        var current = await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct));
        await ReadAsync<ShopProductDto>(await owner.PutAsJsonAsync(
            $"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Product("Beyaz Orkide", 750m, 4, current.Version), Json, Ct));

        var order = await ReadAsync<OrderDetailDto>(await customer.GetAsync($"/api/v1/me/orders/{orderNumber}", Ct));
        var item = Assert.Single(order.Items);
        Assert.Equal(500m, item.UnitPrice);
        Assert.Equal("Orkide", item.ProductName);
    }

    [Fact]
    public async Task Cart_is_priced_from_current_data_and_flags_unavailable_items()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Pricing {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Lale", 100m, 5);
        await SetCartItemAsync(customer, product.Id, 3);

        // Price change by the shop is reflected; the client never sends prices.
        var current = await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct));
        await owner.PutAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Product("Lale", 120m, 5, current.Version), Json, Ct);
        var cart = (await customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Json, Ct))!;
        Assert.Equal(360m, cart.Shops[0].Subtotal);

        // Deactivated product → flagged, excluded from totals, checkout refused.
        await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}/deactivate", null, Ct);
        cart = (await customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Json, Ct))!;
        Assert.Equal(CartLineIssue.ProductUnavailable, cart.Shops[0].Lines[0].Issue);
        Assert.False(cart.CanCheckout);
        Assert.Equal(0m, cart.Total);
        var checkout = await CheckoutAsync(customer);
        Assert.Equal(HttpStatusCode.Conflict, checkout.StatusCode);
        Assert.Equal("checkout.items_unavailable", await ProblemCodeAsync(checkout));
    }

    [Fact]
    public async Task Adding_more_than_stock_or_an_unavailable_product_is_rejected()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Stock {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Papatya", 50m, 2);

        var tooMany = await SetCartItemAsync(customer, product.Id, 3);
        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        Assert.Equal("cart.insufficient_stock", await ProblemCodeAsync(tooMany));

        var invalidQuantity = await SetCartItemAsync(customer, product.Id, 0);
        Assert.Equal(HttpStatusCode.BadRequest, invalidQuantity.StatusCode);

        var unknown = await SetCartItemAsync(customer, Guid.NewGuid(), 1);
        Assert.Equal("cart.product_unavailable", await ProblemCodeAsync(unknown));
    }

    [Fact]
    public async Task Multi_shop_cart_is_split_into_one_order_per_shop()
    {
        host.SkipIfUnavailable();
        using var ownerA = await _driver.CustomerAsync();
        using var ownerB = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shopA = await _driver.ActiveShopAsync(ownerA, $"Split A {Guid.NewGuid():N}");
        var shopB = await _driver.ActiveShopAsync(ownerB, $"Split B {Guid.NewGuid():N}");
        var productA = await CreateProductAsync(ownerA, shopA.Id, "Gül", 100m, 5);
        var productB = await CreateProductAsync(ownerB, shopB.Id, "Çikolata", 80m, 5);
        await SetCartItemAsync(customer, productA.Id, 1);
        await SetCartItemAsync(customer, productB.Id, 2);

        var checkout = await CheckoutOkAsync(customer);

        Assert.Equal(2, checkout.Orders.Count);
        Assert.Equal(100m + 30m + 160m + 30m, checkout.Total); // delivery fee per shop order
        Assert.Contains(checkout.Orders, o => o.ShopId == shopA.Id && o.Total == 130m);
        Assert.Contains(checkout.Orders, o => o.ShopId == shopB.Id && o.Total == 190m);

        // Each shop sees only its own order.
        var aOrders = await ReadAsync<PagedResult<OrderSummaryDto>>(await ownerA.GetAsync($"/api/v1/my/shops/{shopA.Id}/orders", Ct));
        Assert.All(aOrders.Items, o => Assert.Equal(shopA.Id, o.ShopId));
    }

    [Fact]
    public async Task Repeated_checkout_with_the_same_idempotency_key_returns_the_original_result()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Idempotent {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Sümbül", 60m, 5);
        await SetCartItemAsync(customer, product.Id, 1);
        var key = Guid.NewGuid().ToString();

        var first = await CheckoutOkAsync(customer, key);
        var second = await CheckoutOkAsync(customer, key);

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.CheckoutId, second.CheckoutId);
        Assert.Equal(first.Orders[0].OrderNumber, second.Orders[0].OrderNumber);
        var mine = await ReadAsync<PagedResult<OrderSummaryDto>>(await customer.GetAsync("/api/v1/me/orders", Ct));
        Assert.Equal(1, mine.TotalCount);

        var missingKey = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(Delivery(), options: Json) };
        var noKey = await customer.SendAsync(missingKey, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("checkout.invalid_idempotency_key", await ProblemCodeAsync(noKey));
    }

    [Fact]
    public async Task Concurrent_checkouts_never_oversell()
    {
        host.SkipIfUnavailable();
        const int stock = 2;
        const int buyers = 6;
        using var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Rush {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Sevgililer Günü Buketi", 300m, stock);

        var customers = new List<HttpClient>();
        for (var i = 0; i < buyers; i++)
        {
            var c = await _driver.CustomerAsync();
            Assert.Equal(HttpStatusCode.OK, (await SetCartItemAsync(c, product.Id, 1)).StatusCode);
            customers.Add(c);
        }

        var responses = await Task.WhenAll(customers.Select(c => CheckoutAsync(c)));

        Assert.Equal(stock, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(buyers - stock, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var after = await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct));
        Assert.Equal(0, after.StockQuantity);
        customers.ForEach(c => c.Dispose());
    }

    [Fact]
    public async Task Cancellation_releases_stock_and_paid_cancellation_records_refund_due()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Cancel {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Karanfil", 40m, 3);

        await SetCartItemAsync(customer, product.Id, 2);
        var unpaid = (await CheckoutOkAsync(customer)).Orders[0].OrderNumber;
        var cancelled = await ReadAsync<OrderDetailDto>(await customer.PostAsJsonAsync($"/api/v1/me/orders/{unpaid}/cancel", new ReasonRequest("Yanlış ürün"), Json, Ct));
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(0m, cancelled.RefundDue);
        Assert.Equal(3, (await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct))).StockQuantity);

        await SetCartItemAsync(customer, product.Id, 1);
        var paid = (await CheckoutOkAsync(customer)).Orders[0].OrderNumber;
        await _driver.RecordPaymentAsync(paid);
        var rejected = await ReadAsync<OrderDetailDto>(await owner.PostAsJsonAsync($"/api/v1/my/shops/{shop.Id}/orders/{paid}/reject", new ReasonRequest("Stok bitti"), Json, Ct));
        Assert.Equal(OrderStatus.Rejected, rejected.Status);
        Assert.Equal(rejected.Total, rejected.RefundDue);
        Assert.Equal(3, (await ReadAsync<ShopProductDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Ct))).StockQuantity);

        // Terminal: no further transitions.
        var again = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/orders/{paid}/accept", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Concurrent_product_edits_are_detected()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Versions {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Gerbera", 30m, 3);

        var first = await owner.PutAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Product("Gerbera", 35m, 3, product.Version), Json, Ct);
        var stale = await owner.PutAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products/{product.Id}", Product("Gerbera", 40m, 3, product.Version), Json, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("conflict.concurrent_update", await ProblemCodeAsync(stale));
    }

    [Fact]
    public async Task Public_catalog_shows_only_active_products_of_active_shops_with_search_filter_and_paging()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var shop = await _driver.ActiveShopAsync(owner, $"Katalog {marker}");
        await CreateProductAsync(owner, shop.Id, $"Pembe Güller {marker}", 300m, 2);
        await CreateProductAsync(owner, shop.Id, $"Beyaz Güller {marker}", 150m, 0);
        var hidden = await CreateProductAsync(owner, shop.Id, $"Sarı Güller {marker}", 200m, 4);
        await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/products/{hidden.Id}/deactivate", null, Ct);

        using var anonymous = host.CreateClient();
        var search = await ReadAsync<PagedResult<ProductCardDto>>(await anonymous.GetAsync(
            $"/api/v1/products?q=GULLER%20{marker}&categoryId={FlowersCategory}&sort=PriceAscending&pageSize=1", Ct));

        Assert.Equal(2, search.TotalCount); // inactive excluded; Turkish-insensitive search; parent category includes children
        Assert.Single(search.Items);
        Assert.Equal(150m, search.Items[0].Price);
        Assert.Equal("AZN", search.Items[0].Currency);

        var inStock = await ReadAsync<PagedResult<ProductCardDto>>(await anonymous.GetAsync($"/api/v1/products?q={marker}&inStock=true", Ct));
        Assert.Equal(1, inStock.TotalCount);
        Assert.Equal("https://cdn.example.com/roses.jpg", inStock.Items[0].ImageUrl);

        var detail = await anonymous.GetAsync($"/api/v1/products/{hidden.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Closing the shop hides everything.
        await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/close", null, Ct);
        var closed = await ReadAsync<PagedResult<ProductCardDto>>(await anonymous.GetAsync($"/api/v1/products?q={marker}", Ct));
        Assert.Equal(0, closed.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/shops/{shop.Slug}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Pending_shops_are_not_public_and_cannot_sell()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", Profile($"Pending {Guid.NewGuid():N}"), Json, Ct), HttpStatusCode.Created);
        Assert.Equal(ShopStatus.PendingApproval, shop.Status);
        var product = await CreateProductAsync(owner, shop.Id, "Erken Ürün", 10m, 5);

        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/shops/{shop.Slug}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/products/{product.Id}", Ct)).StatusCode);
        Assert.Equal("cart.product_unavailable", await ProblemCodeAsync(await SetCartItemAsync(customer, product.Id, 1)));
    }

    [Theory]
    [InlineData(0, 5, "https://cdn.example.com/a.jpg", "price")]
    [InlineData(10.123, 5, "https://cdn.example.com/a.jpg", "price")]
    [InlineData(10, -1, "https://cdn.example.com/a.jpg", "stockQuantity")]
    [InlineData(10, 5, "http://cdn.example.com/a.jpg", "images[0].Url")]
    [InlineData(10, 5, "file:///etc/passwd", "images[0].Url")]
    public async Task Invalid_product_input_returns_field_errors(double price, int stock, string imageUrl, string field)
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Validation {Guid.NewGuid():N}");
        var request = new ProductUpsertRequest(RosesCategory, "Gül", null, (decimal)price, stock, [new ProductImageInput(imageUrl, null)], null);

        var response = await owner.PostAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products", request, Json, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Optional_text_can_be_left_empty_even_though_Oracle_stores_empty_strings_as_null()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var profile = Profile($"Minimal {Guid.NewGuid():N}") with { Description = "", PostalCode = null };
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", profile, Json, Ct), HttpStatusCode.Created);
        Assert.Null(shop.Description);
        using (var admin = await _driver.AdminAsync())
        {
            await admin.PostAsync($"/api/v1/admin/shops/{shop.Id}/approve", null, Ct);
        }

        var request = new ProductUpsertRequest(RosesCategory, "Sade Gül", "", 10m, 3, [new ProductImageInput("https://cdn.example.com/a.jpg", "")], null);
        var product = await ReadAsync<ShopProductDto>(await owner.PostAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products", request, Json, Ct), HttpStatusCode.Created);
        Assert.Null(product.Description);
        Assert.Null(product.Images[0].AltText);

        await SetCartItemAsync(customer, product.Id, 1);
        var checkout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout")
        {
            Content = JsonContent.Create(Delivery() with { PostalCode = "", GiftMessage = "" }, options: Json),
        };
        checkout.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var result = await ReadAsync<CheckoutResultDto>(await customer.SendAsync(checkout, Ct));
        var order = await ReadAsync<OrderDetailDto>(await customer.GetAsync($"/api/v1/me/orders/{result.Orders[0].OrderNumber}", Ct));
        Assert.Null(order.Delivery.PostalCode);
        Assert.Null(order.Delivery.GiftMessage);
    }

    [Fact]
    public async Task Unknown_category_is_rejected()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Category {Guid.NewGuid():N}");
        var request = Product("Gül", 10m, 1) with { CategoryId = Guid.NewGuid() };

        var response = await owner.PostAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products", request, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("category.not_found", await ProblemCodeAsync(response));
    }
}
