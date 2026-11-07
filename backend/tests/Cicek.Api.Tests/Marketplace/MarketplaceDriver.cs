using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cicek.Application.Authorization;
using Cicek.Application.Carts;
using Cicek.Application.Catalog;
using Cicek.Application.Ordering;
using Cicek.Application.Shops;

namespace Cicek.Api.Tests.Marketplace;

/// <summary>Drives the real HTTP API as different actors (customer, shop owner, administrator).</summary>
internal sealed class MarketplaceDriver(ApiTestHost host)
{
    public static readonly Guid RosesCategory = new("0199d6a0-0000-7000-8000-000000000011");
    public static readonly Guid FlowersCategory = new("0199d6a0-0000-7000-8000-000000000001");
    public static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<HttpClient> ClientAsync(params string[] roles)
    {
        var session = await host.SignInAsUserWithRolesAsync(roles);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    public Task<HttpClient> CustomerAsync() => ClientAsync(Roles.Customer);

    public Task<HttpClient> AdminAsync() => ClientAsync(Roles.Administrator);

    public static DateOnly Tomorrow()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(1);
    }

    public static ShopProfileRequest Profile(string name) =>
        new(name, "Taze çiçekler", "shop@example.com", "+90 555 111 22 33", "Bağdat Cad. 1", "Kadıköy", "İstanbul", "34710");

    public static CheckoutRequest Delivery() =>
        new("Ayşe Yılmaz", "+90 555 000 00 00", "Moda Cad. 5", "Kadıköy", "İstanbul", "34710", Tomorrow(), "İyi ki doğdun!");

    public static ProductUpsertRequest Product(string name, decimal price, int stock, long? version = null) =>
        new(RosesCategory, name, "Taze", price, stock, [new ProductImageInput("https://cdn.example.com/roses.jpg", "Güller")], version);

    /// <summary>Owner applies; an administrator approves. Returns the active shop.</summary>
    public async Task<ShopManagementDto> ActiveShopAsync(HttpClient owner, string name)
    {
        var shop = await ReadAsync<ShopManagementDto>(await owner.PostAsJsonAsync("/api/v1/shops", Profile(name), Json, Ct), HttpStatusCode.Created);
        using var admin = await AdminAsync();
        return await ReadAsync<ShopManagementDto>(await admin.PostAsync($"/api/v1/admin/shops/{shop.Id}/approve", null, Ct));
    }

    public static async Task<ShopProductDto> CreateProductAsync(HttpClient owner, Guid shopId, string name, decimal price, int stock) =>
        await ReadAsync<ShopProductDto>(
            await owner.PostAsJsonAsync($"/api/v1/my/shops/{shopId}/products", Product(name, price, stock), Json, Ct), HttpStatusCode.Created);

    public static Task<HttpResponseMessage> SetCartItemAsync(HttpClient customer, Guid productId, int quantity) =>
        customer.PutAsJsonAsync($"/api/v1/cart/items/{productId}", new SetCartItemRequest(quantity), Json, Ct);

    public static Task<HttpResponseMessage> CheckoutAsync(HttpClient customer, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(Delivery(), options: Json) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return customer.SendAsync(request, Ct);
    }

    public static async Task<CheckoutResultDto> CheckoutOkAsync(HttpClient customer, string? key = null) =>
        await ReadAsync<CheckoutResultDto>(await CheckoutAsync(customer, key));

    public async Task<OrderDetailDto> RecordPaymentAsync(string orderNumber)
    {
        using var admin = await AdminAsync();
        return await ReadAsync<OrderDetailDto>(
            await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderNumber}/record-payment", new RecordPaymentRequest("BANK-REF-1"), Json, Ct));
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"Expected {expected} but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
