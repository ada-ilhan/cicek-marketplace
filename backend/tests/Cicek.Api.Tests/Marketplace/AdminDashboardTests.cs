using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Admin;
using Cicek.Application.Authorization;
using Cicek.Application.Common;
using Cicek.Application.Shops;
using Cicek.Domain.Shops;
using static Cicek.Api.Tests.Marketplace.MarketplaceDriver;

namespace Cicek.Api.Tests.Marketplace;

/// <summary>Administrator dashboard endpoints, shop rejection workflow and the permissions guarding them.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminDashboardTests(ApiTestHost host)
{
    private readonly MarketplaceDriver _driver = new(host);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rejection_hides_the_shop_shows_the_reason_and_allows_a_corrected_resubmission()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var admin = await _driver.AdminAsync();
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", Profile($"Rejected {Guid.NewGuid():N}"), Json, Ct), HttpStatusCode.Created);

        var rejected = await ReadAsync<ShopManagementDto>(await admin.PostAsJsonAsync($"/api/v1/admin/shops/{shop.Id}/reject", new SuspendShopRequest("Business license missing"), Json, Ct));
        Assert.Equal(ShopStatus.Rejected, rejected.Status);

        var ownerView = await ReadAsync<ShopManagementDto>(await owner.GetAsync($"/api/v1/my/shops/{shop.Id}", Ct));
        Assert.Equal("Business license missing", ownerView.StatusReason);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/shops/{shop.Slug}", Ct)).StatusCode);

        // Approval cannot skip resubmission.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync($"/api/v1/admin/shops/{shop.Id}/approve", null, Ct)).StatusCode);

        var corrected = await ReadAsync<ShopManagementDto>(await owner.PutAsJsonAsync($"/api/v1/my/shops/{shop.Id}",
            new UpdateShopProfileRequest(Profile("Corrected Shop") with { Description = "Now with license" }, ownerView.Version), Json, Ct));
        var resubmitted = await ReadAsync<ShopManagementDto>(await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/resubmit", null, Ct));
        Assert.Equal(ShopStatus.PendingApproval, resubmitted.Status);
        Assert.Null(resubmitted.StatusReason);

        var approved = await ReadAsync<ShopManagementDto>(await admin.PostAsync($"/api/v1/admin/shops/{shop.Id}/approve", null, Ct));
        Assert.Equal(ShopStatus.Active, approved.Status);
        Assert.Equal("Corrected Shop", corrected.Name);
    }

    [Fact]
    public async Task Only_the_owner_can_resubmit_and_only_a_rejected_shop()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var stranger = await _driver.CustomerAsync();
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", Profile($"Resub {Guid.NewGuid():N}"), Json, Ct), HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/api/v1/my/shops/{shop.Id}/resubmit", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/resubmit", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_shop_list_shows_owner_email_status_filter_and_search()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var admin = await _driver.AdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", Profile($"Adminlist {marker}"), Json, Ct), HttpStatusCode.Created);

        var list = await ReadAsync<PagedResult<AdminShopDto>>(await admin.GetAsync($"/api/v1/admin/shops?status=PendingApproval&q={marker}", Ct));
        var row = Assert.Single(list.Items);
        Assert.Equal(shop.Id, row.Shop.Id);
        Assert.Contains("@example.com", row.OwnerEmail, StringComparison.Ordinal);

        var detail = await ReadAsync<AdminShopDto>(await admin.GetAsync($"/api/v1/admin/shops/{shop.Id}", Ct));
        Assert.Equal(row.OwnerEmail, detail.OwnerEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/shops/{Guid.NewGuid()}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Overview_reports_real_counts_and_is_permission_protected()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        using var customer = await _driver.CustomerAsync();
        using var admin = await _driver.AdminAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Overview {Guid.NewGuid():N}");
        var product = await CreateProductAsync(owner, shop.Id, "Gül", 100m, 5);
        await SetCartItemAsync(customer, product.Id, 1);
        await CheckoutOkAsync(customer);

        var overview = await ReadAsync<AdminOverviewDto>(await admin.GetAsync("/api/v1/admin/overview", Ct));

        Assert.Equal("AZN", overview.Currency);
        Assert.True(overview.ShopsByStatus.Single(s => s.Status == "Active").Count >= 1);
        Assert.True(overview.OrdersByStatus.Single(s => s.Status == "PendingPayment").Count >= 1);
        Assert.True(overview.OrdersLast7Days >= 1);
        Assert.True(overview.ActiveProducts >= 1);
        Assert.True(overview.Users >= 3);
        Assert.True(overview.OrderValue >= 130m);
        Assert.Equal(Enum.GetValues<ShopStatus>().Length, overview.ShopsByStatus.Count);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/overview", Ct)).StatusCode);
        using var finance = await _driver.ClientAsync(Roles.Finance);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/v1/admin/overview", Ct)).StatusCode);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/overview", Ct)).StatusCode);
    }

    [Fact]
    public async Task User_list_exposes_only_safe_fields_and_supports_search_and_role_filter()
    {
        host.SkipIfUnavailable();
        using var admin = await _driver.AdminAsync();
        using var support = await _driver.ClientAsync(Roles.Support);
        using var customer = await _driver.CustomerAsync();

        var response = await admin.GetAsync("/api/v1/admin/users?role=Administrator&pageSize=5", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var forbidden in new[] { "passwordHash", "securityStamp", "phoneNumber", "token" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }

        var admins = JsonSerializer.Deserialize<PagedResult<UserListItemDto>>(body, Json)!;
        Assert.NotEmpty(admins.Items);
        Assert.All(admins.Items, u => Assert.Contains(Roles.Administrator, u.Roles));

        var search = await ReadAsync<PagedResult<UserListItemDto>>(await admin.GetAsync("/api/v1/admin/users?q=user.&role=Customer", Ct));
        Assert.All(search.Items, u => Assert.Contains("user.", u.Email, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync("/api/v1/admin/users", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/users", Ct)).StatusCode);
    }

    [Fact]
    public async Task Shop_owners_cannot_reach_admin_shop_endpoints()
    {
        host.SkipIfUnavailable();
        using var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Guard {Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/v1/admin/shops/{shop.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.PostAsJsonAsync($"/api/v1/admin/shops/{shop.Id}/reject", new SuspendShopRequest("self"), Json, Ct)).StatusCode);
    }
}
