using System.Net;
using System.Net.Http.Json;
using Cicek.Application.Ordering;
using Cicek.Application.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cicek.Api.Tests.Marketplace.MarketplaceDriver;

namespace Cicek.Api.Tests.Marketplace;

/// <summary>Cross-shop and cross-customer isolation, and platform permissions for marketplace endpoints.</summary>
[Collection(ApiCollection.Name)]
public sealed class MarketplaceAuthorizationTests(ApiTestHost host)
{
    private readonly MarketplaceDriver _driver = new(host);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_shop_owner_cannot_read_or_modify_another_shop()
    {
        host.SkipIfUnavailable();
        using var ownerA = await _driver.CustomerAsync();
        using var ownerB = await _driver.CustomerAsync();
        var shopA = await _driver.ActiveShopAsync(ownerA, $"Victim {Guid.NewGuid():N}");
        var productA = await CreateProductAsync(ownerA, shopA.Id, "Gül", 100m, 5);

        var attempts = new[]
        {
            await ownerB.GetAsync($"/api/v1/my/shops/{shopA.Id}", Ct),
            await ownerB.PutAsJsonAsync($"/api/v1/my/shops/{shopA.Id}", new UpdateShopProfileRequest(Profile("Hijacked"), shopA.Version), Json, Ct),
            await ownerB.PostAsync($"/api/v1/my/shops/{shopA.Id}/close", null, Ct),
            await ownerB.GetAsync($"/api/v1/my/shops/{shopA.Id}/products", Ct),
            await ownerB.PostAsJsonAsync($"/api/v1/my/shops/{shopA.Id}/products", Product("Injected", 1m, 1), Json, Ct),
            await ownerB.PutAsJsonAsync($"/api/v1/my/shops/{shopA.Id}/products/{productA.Id}", Product("Changed", 1m, 1, productA.Version), Json, Ct),
            await ownerB.PostAsync($"/api/v1/my/shops/{shopA.Id}/products/{productA.Id}/deactivate", null, Ct),
            await ownerB.GetAsync($"/api/v1/my/shops/{shopA.Id}/orders", Ct),
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));

        // Nothing changed.
        var product = await ReadAsync<Cicek.Application.Catalog.ShopProductDto>(await ownerA.GetAsync($"/api/v1/my/shops/{shopA.Id}/products/{productA.Id}", Ct));
        Assert.Equal("Gül", product.Name);
        Assert.Equal(productA.Version, product.Version);
    }

    [Fact]
    public async Task A_shop_cannot_act_on_another_shops_order_through_its_own_route()
    {
        host.SkipIfUnavailable();
        using var ownerA = await _driver.CustomerAsync();
        using var ownerB = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        var shopA = await _driver.ActiveShopAsync(ownerA, $"A {Guid.NewGuid():N}");
        var shopB = await _driver.ActiveShopAsync(ownerB, $"B {Guid.NewGuid():N}");
        var productA = await CreateProductAsync(ownerA, shopA.Id, "Gül", 100m, 5);
        await SetCartItemAsync(customer, productA.Id, 1);
        var orderNumber = (await CheckoutOkAsync(customer)).Orders[0].OrderNumber;
        await _driver.RecordPaymentAsync(orderNumber);

        // Shop B passes its own shop id (membership OK) but the order belongs to shop A.
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"/api/v1/my/shops/{shopB.Id}/orders/{orderNumber}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PostAsync($"/api/v1/my/shops/{shopB.Id}/orders/{orderNumber}/accept", null, Ct)).StatusCode);

        var order = await ReadAsync<OrderDetailDto>(await ownerA.GetAsync($"/api/v1/my/shops/{shopA.Id}/orders/{orderNumber}", Ct));
        Assert.Equal(Domain.Ordering.OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task A_customer_cannot_see_or_cancel_another_customers_order()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var alice = await _driver.CustomerAsync();
        using var mallory = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Privacy {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Gül", 100m, 5);
        await SetCartItemAsync(alice, product.Id, 1);
        var orderNumber = (await CheckoutOkAsync(alice)).Orders[0].OrderNumber;

        Assert.Equal(HttpStatusCode.NotFound, (await mallory.GetAsync($"/api/v1/me/orders/{orderNumber}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await mallory.PostAsJsonAsync($"/api/v1/me/orders/{orderNumber}/cancel", new ReasonRequest("x"), Json, Ct)).StatusCode);
        var malloryOrders = await ReadAsync<Cicek.Application.Common.PagedResult<OrderSummaryDto>>(await mallory.GetAsync("/api/v1/me/orders", Ct));
        Assert.Equal(0, malloryOrders.TotalCount);

        var aliceView = await ReadAsync<OrderDetailDto>(await alice.GetAsync($"/api/v1/me/orders/{orderNumber}", Ct));
        Assert.Equal(Domain.Ordering.OrderStatus.PendingPayment, aliceView.Status);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/shops")]
    [InlineData("POST", "/api/v1/admin/shops/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("GET", "/api/v1/admin/orders")]
    [InlineData("POST", "/api/v1/admin/categories")]
    public async Task Customers_and_shop_owners_cannot_use_admin_endpoints(string method, string path)
    {
        host.SkipIfUnavailable();
        using var customer = await _driver.CustomerAsync();

        var response = await customer.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { name = "x" }) }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Support_can_read_orders_but_not_record_payments()
    {
        host.SkipIfUnavailable();
        using var support = await _driver.ClientAsync(Application.Authorization.Roles.Support);

        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync("/api/v1/admin/orders", Ct)).StatusCode);
        var pay = await support.PostAsJsonAsync("/api/v1/admin/orders/CS261013-AAAAAA/record-payment", new RecordPaymentRequest("x"), Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, pay.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/cart")]
    [InlineData("POST", "/api/v1/checkout")]
    [InlineData("GET", "/api/v1/me/orders")]
    [InlineData("POST", "/api/v1/shops")]
    [InlineData("GET", "/api/v1/my/shops")]
    public async Task Anonymous_users_cannot_use_customer_or_seller_endpoints(string method, string path)
    {
        host.SkipIfUnavailable();
        using var anonymous = host.CreateClient();

        var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), path), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_actions_are_audited_and_suspension_hides_the_shop()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var admin = await _driver.AdminAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Suspend {Guid.NewGuid():N}");

        var suspended = await ReadAsync<ShopManagementDto>(await admin.PostAsJsonAsync($"/api/v1/admin/shops/{shop.Id}/suspend", new SuspendShopRequest("Policy review"), Json, Ct));
        Assert.Equal(Domain.Shops.ShopStatus.Suspended, suspended.Status);

        // Owner cannot reopen a suspended shop.
        var reopen = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/open", null, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reopen.StatusCode);

        await using var scope = host.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Cicek.Application.Abstractions.Persistence.IAppDbContext>();
        var entries = await db.AuditEntries.Where(a => a.EntityId == shop.Id.ToString()).Select(a => a.Action).ToListAsync(Ct);
        Assert.Contains("shop.approve", entries);
        Assert.Contains("shop.suspend", entries);
    }
}
