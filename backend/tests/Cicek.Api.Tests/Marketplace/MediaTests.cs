using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cicek.Application.Catalog;
using Cicek.Application.Media;
using Cicek.Application.Shops;
using static Cicek.Api.Tests.Marketplace.MarketplaceDriver;

namespace Cicek.Api.Tests.Marketplace;

/// <summary>Image upload: content-based validation, ownership, storage outside Oracle and public delivery.</summary>
[Collection(ApiCollection.Name)]
public sealed class MediaTests(ApiTestHost host)
{
    private readonly MarketplaceDriver _driver = new(host);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private static MultipartFormDataContent File(byte[] bytes, string fileName, string mime)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return new MultipartFormDataContent { { part, "file", fileName } };
    }

    private async Task<(HttpClient Owner, ShopManagementDto Shop)> OwnerWithShopAsync()
    {
        var owner = await _driver.CustomerAsync();
        var shop = await _driver.ActiveShopAsync(owner, $"Media {Guid.NewGuid():N}");
        return (owner, shop);
    }

    [Fact]
    public async Task A_member_uploads_a_png_and_anyone_can_fetch_it_with_cache_headers()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;

        var response = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(PngBytes, "kendi-adim.png", "image/png"), Ct);
        var uploaded = await ReadAsync<UploadedImageDto>(response, HttpStatusCode.Created);

        Assert.Equal("image/png", uploaded.ContentType);
        Assert.Equal($"/media/{uploaded.Id}", uploaded.Url);

        using var anonymous = host.CreateClient();
        var fetched = await anonymous.GetAsync($"/api/v1/media/{uploaded.Id}", Ct);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/png", fetched.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await fetched.Content.ReadAsByteArrayAsync(Ct));
        Assert.Contains("immutable", fetched.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", fetched.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task The_file_type_comes_from_the_bytes_not_from_the_name_or_declared_mime()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;

        // A JPEG named and declared as PNG is stored as what it really is.
        var jpeg = await ReadAsync<UploadedImageDto>(
            await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(JpegBytes, "x.png", "image/png"), Ct), HttpStatusCode.Created);
        Assert.Equal("image/jpeg", jpeg.ContentType);

        // Script/HTML/SVG content is rejected whatever the extension or MIME claims.
        foreach (var (bytes, name, mime) in new[]
                 {
                     ("<script>alert(1)</script>"u8.ToArray(), "evil.png", "image/png"),
                     ("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(), "logo.svg", "image/svg+xml"),
                     ("GIF89a......"u8.ToArray(), "anim.gif", "image/gif"),
                     ("MZ executable"u8.ToArray(), "a.jpg", "image/jpeg"),
                 })
        {
            var rejected = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(bytes, name, mime), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal("media.unsupported_type", await ProblemCodeAsync(rejected));
        }
    }

    [Fact]
    public async Task Empty_and_oversized_files_are_rejected()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;

        var empty = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File([], "empty.png", "image/png"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var big = new byte[5_000_001];
        PngBytes.CopyTo(big, 0);
        var tooLarge = await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(big, "big.png", "image/png"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Equal("media.too_large", await ProblemCodeAsync(tooLarge));
    }

    [Fact]
    public async Task Non_members_and_anonymous_callers_cannot_upload()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;
        using var stranger = await _driver.CustomerAsync();
        using var anonymous = host.CreateClient();

        var asStranger = await stranger.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(PngBytes, "a.png", "image/png"), Ct);
        var asAnonymous = await anonymous.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(PngBytes, "a.png", "image/png"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asAnonymous.StatusCode);
    }

    [Fact]
    public async Task A_shop_cannot_use_another_shops_uploaded_image()
    {
        host.SkipIfUnavailable();
        var (ownerA, shopA) = await OwnerWithShopAsync();
        var (ownerB, shopB) = await OwnerWithShopAsync();
        using var _a = ownerA;
        using var _b = ownerB;

        var imageOfA = await ReadAsync<UploadedImageDto>(
            await ownerA.PostAsync($"/api/v1/my/shops/{shopA.Id}/images", File(PngBytes, "a.png", "image/png"), Ct), HttpStatusCode.Created);

        // B tries to attach A's image to a product and to the shop profile.
        var product = new ProductUpsertRequest(RosesCategory, "Gül", null, 10m, 3, [new ProductImageInput(imageOfA.Url, "x")], null);
        var onProduct = await ownerB.PostAsJsonAsync($"/api/v1/my/shops/{shopB.Id}/products", product, Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, onProduct.StatusCode);
        Assert.Equal("media.invalid_reference", await ProblemCodeAsync(onProduct));

        var profile = Profile("Yeni Ad") with { ImageUrl = imageOfA.Url };
        var onShop = await ownerB.PutAsJsonAsync($"/api/v1/my/shops/{shopB.Id}", new UpdateShopProfileRequest(profile, shopB.Version), Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, onShop.StatusCode);

        // A itself may use it, and a non-existent platform image is refused.
        var own = new ProductUpsertRequest(RosesCategory, "Gül", null, 10m, 3, [new ProductImageInput(imageOfA.Url, "x")], null);
        var created = await ReadAsync<ShopProductDto>(
            await ownerA.PostAsJsonAsync($"/api/v1/my/shops/{shopA.Id}/products", own, Json, Ct), HttpStatusCode.Created);
        Assert.Equal(imageOfA.Url, created.Images[0].Url);

        var ghost = new ProductUpsertRequest(RosesCategory, "Gül", null, 10m, 3, [new ProductImageInput($"/media/{Guid.NewGuid()}", null)], null);
        var missing = await ownerA.PostAsJsonAsync($"/api/v1/my/shops/{shopA.Id}/products", ghost, Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task A_shop_image_is_saved_on_the_profile_and_shown_publicly()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;
        var image = await ReadAsync<UploadedImageDto>(
            await owner.PostAsync($"/api/v1/my/shops/{shop.Id}/images", File(PngBytes, "logo.png", "image/png"), Ct), HttpStatusCode.Created);

        var updated = await ReadAsync<ShopManagementDto>(await owner.PutAsJsonAsync(
            $"/api/v1/my/shops/{shop.Id}", new UpdateShopProfileRequest(Profile(shop.Name) with { ImageUrl = image.Url }, shop.Version), Json, Ct));
        Assert.Equal(image.Url, updated.ImageUrl);

        using var anonymous = host.CreateClient();
        var publicShop = await ReadAsync<PublicShopDto>(await anonymous.GetAsync($"/api/v1/shops/{shop.Slug}", Ct));
        Assert.Equal(image.Url, publicShop.ImageUrl);
    }

    [Theory]
    [InlineData("/media/not-a-guid")]
    [InlineData("http://cdn.example.com/a.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//cdn.example.com/a.jpg")]
    [InlineData("https://user:pw@cdn.example.com/a.jpg")]
    public async Task Malformed_or_unsafe_image_references_are_rejected(string url)
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;
        var product = new ProductUpsertRequest(RosesCategory, "Gül", null, 10m, 3, [new ProductImageInput(url, null)], null);

        var response = await owner.PostAsJsonAsync($"/api/v1/my/shops/{shop.Id}/products", product, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_media_is_404()
    {
        host.SkipIfUnavailable();
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/media/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_shop_product_list_can_be_searched_by_name()
    {
        host.SkipIfUnavailable();
        var (owner, shop) = await OwnerWithShopAsync();
        using var _ = owner;
        await CreateProductAsync(owner, shop.Id, "Qırmızı Qızılgül", 50m, 5);
        await CreateProductAsync(owner, shop.Id, "Ağ Orxideya", 90m, 5);

        var found = await ReadAsync<Cicek.Application.Common.PagedResult<ShopProductDto>>(
            await owner.GetAsync($"/api/v1/my/shops/{shop.Id}/products?q=orxideya", Ct));

        Assert.Single(found.Items);
        Assert.Equal("Ağ Orxideya", found.Items[0].Name);
    }
}
