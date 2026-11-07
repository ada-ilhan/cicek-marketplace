using System.Security.Claims;
using Cicek.Api.Auth;
using Cicek.Application.Authorization;
using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Cicek.Application.Ordering;
using Cicek.Application.Shops;
using Cicek.Domain.Catalog;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.Endpoints;

/// <summary>
/// Shop onboarding and store-scoped management. Every /my/shops/{shopId} endpoint checks the caller's
/// membership of that shop in the application layer (403 for non-members), never only in the UI.
/// </summary>
internal static class ShopEndpoints
{
    public static RouteGroupBuilder MapShopEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/shops", async (ShopProfileRequest request, ClaimsPrincipal user, ApplyForShopHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), request, ct)).ToCreated(s => $"/api/v1/my/shops/{s.Id}"))
            .RequireAuthorization()
            .Validate<ShopProfileRequest>()
            .WithTags("Seller: Shop").WithName("ApplyForShop")
            .WithSummary("Apply to open a shop; it stays PendingApproval (not public) until an administrator approves it.")
            .Produces<ShopManagementDto>(StatusCodes.Status201Created);

        var mine = api.MapGroup("/my/shops").RequireAuthorization();

        mine.MapGet("", async (ClaimsPrincipal user, GetMyShopsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(user.RequiredUserId(), ct)))
            .WithTags("Seller: Shop").WithName("ListMyShops").Produces<IReadOnlyList<ShopManagementDto>>();

        mine.MapGet("/{shopId:guid}", async (Guid shopId, ClaimsPrincipal user, GetMyShopHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), ct)).ToHttp())
            .WithTags("Seller: Shop").WithName("GetMyShop").Produces<ShopManagementDto>().ProducesProblem(StatusCodes.Status403Forbidden);

        mine.MapPut("/{shopId:guid}", async (Guid shopId, UpdateShopProfileRequest request, ClaimsPrincipal user, UpdateShopProfileHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), request, ct)).ToHttp())
            .Validate<UpdateShopProfileRequest>()
            .WithTags("Seller: Shop").WithName("UpdateMyShop").WithSummary("Owner only. Requires the version last read (409 on concurrent change).")
            .Produces<ShopManagementDto>().ProducesProblem(StatusCodes.Status409Conflict);

        mine.MapPost("/{shopId:guid}/resubmit", async (Guid shopId, ClaimsPrincipal user, ResubmitShopHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), ct)).ToHttp())
            .WithTags("Seller: Shop").WithName("ResubmitMyShop").WithSummary("Owner sends a rejected application back to review (Rejected → PendingApproval).")
            .Produces<ShopManagementDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        mine.MapPost("/{shopId:guid}/open", async (Guid shopId, ClaimsPrincipal user, SetShopOpenHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), open: true, ct)).ToHttp())
            .WithTags("Seller: Shop").WithName("OpenMyShop").WithSummary("Inactive → Active (owner).").Produces<ShopManagementDto>();

        mine.MapPost("/{shopId:guid}/close", async (Guid shopId, ClaimsPrincipal user, SetShopOpenHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), open: false, ct)).ToHttp())
            .WithTags("Seller: Shop").WithName("CloseMyShop").WithSummary("Active → Inactive (owner): hidden, no new orders.").Produces<ShopManagementDto>();

        // ---- Products -----------------------------------------------------------------------------
        var products = mine.MapGroup("/{shopId:guid}/products").WithTags("Seller: Products");

        products.MapGet("", async (Guid shopId, ProductStatus? status, string? q, bool? outOfStock, [AsParameters] PagingParameters paging, ClaimsPrincipal user,
                    ListShopProductsHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), status, q, outOfStock, paging.ToPageRequest(), ct)).ToHttp())
            .WithName("ListMyShopProducts").Produces<PagedResult<ShopProductDto>>();

        products.MapGet("/{productId:guid}", async (Guid shopId, Guid productId, ClaimsPrincipal user, GetShopProductHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, productId, user.RequiredUserId(), ct)).ToHttp())
            .WithName("GetMyShopProduct").Produces<ShopProductDto>().ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPost("", async (Guid shopId, ProductUpsertRequest request, ClaimsPrincipal user, SaveShopProductHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, null, user.RequiredUserId(), request, ct)).ToCreated(p => $"/api/v1/my/shops/{shopId}/products/{p.Id}"))
            .Validate<ProductUpsertRequest>()
            .WithName("CreateMyShopProduct").Produces<ShopProductDto>(StatusCodes.Status201Created);

        products.MapPut("/{productId:guid}", async (Guid shopId, Guid productId, ProductUpsertRequest request, ClaimsPrincipal user,
                    SaveShopProductHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, productId, user.RequiredUserId(), request, ct)).ToHttp())
            .Validate<ProductUpsertRequest>()
            .WithName("UpdateMyShopProduct").WithSummary("Requires `version` (409 if the product changed since it was read).")
            .Produces<ShopProductDto>().ProducesProblem(StatusCodes.Status409Conflict);

        products.MapPost("/{productId:guid}/activate", async (Guid shopId, Guid productId, ClaimsPrincipal user, SetShopProductStatusHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, productId, user.RequiredUserId(), active: true, ct)).ToHttp())
            .WithName("ActivateMyShopProduct").Produces<ShopProductDto>();

        products.MapPost("/{productId:guid}/deactivate", async (Guid shopId, Guid productId, ClaimsPrincipal user, SetShopProductStatusHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, productId, user.RequiredUserId(), active: false, ct)).ToHttp())
            .WithName("DeactivateMyShopProduct").WithSummary("Withdraws the product; it can no longer be bought.").Produces<ShopProductDto>();

        // ---- Orders -------------------------------------------------------------------------------
        var orders = mine.MapGroup("/{shopId:guid}/orders").WithTags("Seller: Orders");

        orders.MapGet("", async (Guid shopId, OrderStatus? status, [AsParameters] PagingParameters paging, ClaimsPrincipal user,
                    ListShopOrdersHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), status, paging.ToPageRequest(), ct)).ToHttp())
            .WithName("ListMyShopOrders").Produces<PagedResult<OrderSummaryDto>>();

        orders.MapGet("/{orderNumber}", async (Guid shopId, string orderNumber, ClaimsPrincipal user, GetShopOrderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), orderNumber, ct)).ToHttp())
            .WithName("GetMyShopOrder").Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);

        MapShopOrderAction(orders, "accept", ShopOrderAction.Accept, "Paid → Accepted.");
        MapShopOrderAction(orders, "start-preparation", ShopOrderAction.StartPreparation, "Accepted → Preparing.");
        MapShopOrderAction(orders, "mark-ready", ShopOrderAction.MarkReady, "Preparing → ReadyForDelivery.");
        MapShopOrderAction(orders, "mark-delivered", ShopOrderAction.MarkDelivered, "ReadyForDelivery → Delivered (until courier integration exists).");

        orders.MapPost("/{orderNumber}/reject", async (Guid shopId, string orderNumber, ReasonRequest request, ClaimsPrincipal user,
                    ShopOrderActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), orderNumber, ShopOrderAction.Reject, request.Reason, ct)).ToHttp())
            .Validate<ReasonRequest>()
            .WithName("RejectMyShopOrder").WithSummary("Paid → Rejected; stock is released and a refund becomes due.")
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status409Conflict);

        // ---- Administration ---------------------------------------------------------------------------
        var admin = api.MapGroup("/admin/shops").WithTags("Admin: Shops").RequirePermission(Permissions.ShopsManage);

        admin.MapGet("", async (ShopStatus? status, [FromQuery(Name = "q")] string? search, [AsParameters] PagingParameters paging,
                    ListShopsForAdminHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(status, search, paging.ToPageRequest(), ct)))
            .WithName("AdminListShops").WithSummary("Shops with owner email and active product count; filter by status and name.")
            .Produces<PagedResult<AdminShopDto>>();

        admin.MapGet("/{shopId:guid}", async (Guid shopId, GetShopForAdminHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, ct)).ToHttp())
            .WithName("AdminGetShop").Produces<AdminShopDto>().ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{shopId:guid}/reject", async (Guid shopId, SuspendShopRequest request, ClaimsPrincipal user, AdminShopActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), AdminShopAction.Reject, request.Reason, ct)).ToHttp())
            .Validate<SuspendShopRequest>()
            .WithName("RejectShop").WithSummary("Declines a pending application (reason shown to the owner).")
            .Produces<ShopManagementDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPost("/{shopId:guid}/approve", async (Guid shopId, ClaimsPrincipal user, AdminShopActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), AdminShopAction.Approve, null, ct)).ToHttp())
            .WithName("ApproveShop").Produces<ShopManagementDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPost("/{shopId:guid}/suspend", async (Guid shopId, SuspendShopRequest request, ClaimsPrincipal user, AdminShopActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), AdminShopAction.Suspend, request.Reason, ct)).ToHttp())
            .Validate<SuspendShopRequest>()
            .WithName("SuspendShop").Produces<ShopManagementDto>();

        admin.MapPost("/{shopId:guid}/reinstate", async (Guid shopId, ClaimsPrincipal user, AdminShopActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), AdminShopAction.Reinstate, null, ct)).ToHttp())
            .WithName("ReinstateShop").Produces<ShopManagementDto>();

        admin.MapPut("/{shopId:guid}/commission", async (Guid shopId, SetCommissionRequest request, ClaimsPrincipal user, SetShopCommissionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), request.CommissionRateBps, ct)).ToHttp())
            .Validate<SetCommissionRequest>()
            .WithName("SetShopCommission").WithSummary("Shop-specific commission in basis points; null restores the platform default. Applies to new orders only.")
            .Produces<ShopManagementDto>();

        return api;
    }

    private static void MapShopOrderAction(RouteGroupBuilder orders, string route, ShopOrderAction action, string summary) =>
        orders.MapPost($"/{{orderNumber}}/{route}", async (Guid shopId, string orderNumber, ClaimsPrincipal user,
                    ShopOrderActionHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(shopId, user.RequiredUserId(), orderNumber, action, null, ct)).ToHttp())
            .WithName($"{action}MyShopOrder").WithSummary(summary)
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status409Conflict);
}
