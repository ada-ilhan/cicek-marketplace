using System.Security.Claims;
using Cicek.Api.Auth;
using Cicek.Application.Authorization;
using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Cicek.Application.Shops;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.Endpoints;

/// <summary>Public catalogue (anonymous, read-only) and category administration.</summary>
internal static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var publicApi = api.MapGroup("").WithTags("Catalog").AllowAnonymous();

        publicApi.MapGet("/categories", async (ListCategoriesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(includeInactive: false, ct)))
            .WithName("ListCategories").WithSummary("Active categories (two levels; children reference ParentId).")
            .Produces<IReadOnlyList<CategoryDto>>();

        publicApi.MapGet("/shops", async ([FromQuery(Name = "q")] string? search, [AsParameters] PagingParameters paging,
                    ListPublicShopsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(search, paging.ToPageRequest(), ct)))
            .WithName("ListShops").WithSummary("Active shops, optionally filtered by name.")
            .Produces<PagedResult<PublicShopDto>>();

        publicApi.MapGet("/shops/{slug}", async (string slug, GetPublicShopHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(slug, ct)).ToHttp())
            .WithName("GetShop").WithSummary("An active shop by slug.")
            .Produces<PublicShopDto>().ProducesProblem(StatusCodes.Status404NotFound);

        publicApi.MapGet("/products", async ([AsParameters] ProductSearchParameters query, [AsParameters] PagingParameters paging,
                    SearchProductsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(query.ToQuery(), paging.ToPageRequest(), ct)))
            .WithName("SearchProducts").WithSummary("Search purchasable listings: text, category (incl. sub-categories), shop, price range, stock; sorted and paged.")
            .Produces<PagedResult<ProductCardDto>>();

        publicApi.MapGet("/products/{productId:guid}", async (Guid productId, GetProductHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(productId, ct)).ToHttp())
            .WithName("GetProduct").WithSummary("Product detail (active products of active shops only).")
            .Produces<ProductDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);

        var admin = api.MapGroup("/admin/categories").WithTags("Admin: Catalog").RequirePermission(Permissions.CategoriesManage);

        admin.MapGet("", async (ListCategoriesHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(includeInactive: true, ct)))
            .WithName("AdminListCategories").Produces<IReadOnlyList<CategoryDto>>();

        admin.MapPost("", async (CreateCategoryRequest request, ClaimsPrincipal user, CreateCategoryHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), request, ct)).ToCreated(c => $"/api/v1/admin/categories/{c.Id}"))
            .Validate<CreateCategoryRequest>()
            .WithName("CreateCategory").Produces<CategoryDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{categoryId:guid}", async (Guid categoryId, UpdateCategoryRequest request, ClaimsPrincipal user, UpdateCategoryHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), categoryId, request, ct)).ToHttp())
            .Validate<UpdateCategoryRequest>()
            .WithName("UpdateCategory").WithSummary("Rename, reorder or (de)activate a category.")
            .Produces<CategoryDto>().ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}

internal sealed record ProductSearchParameters(
    [property: FromQuery(Name = "q")] string? Search,
    [property: FromQuery(Name = "categoryId")] Guid? CategoryId,
    [property: FromQuery(Name = "shopId")] Guid? ShopId,
    [property: FromQuery(Name = "minPrice")] decimal? MinPrice,
    [property: FromQuery(Name = "maxPrice")] decimal? MaxPrice,
    [property: FromQuery(Name = "inStock")] bool? InStock,
    [property: FromQuery(Name = "sort")] ProductSort? Sort)
{
    public ProductSearchQuery ToQuery() => new(Search, CategoryId, ShopId, MinPrice, MaxPrice, InStock ?? false, Sort ?? ProductSort.Newest);
}
