using System.Security.Claims;
using Cicek.Api.Auth;
using Cicek.Application.Authorization;
using Cicek.Application.Carts;
using Cicek.Application.Common;
using Cicek.Application.Ordering;
using Cicek.Domain.Ordering;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.Endpoints;

/// <summary>Cart, checkout, customer orders (own orders only) and order administration.</summary>
internal static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder api)
    {
        var cart = api.MapGroup("/cart").WithTags("Cart").RequireAuthorization();

        cart.MapGet("", async (ClaimsPrincipal user, GetCartHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(user.RequiredUserId(), ct)))
            .WithName("GetCart").WithSummary("Cart priced from current data, grouped by shop (one order per shop at checkout).")
            .Produces<CartDto>();

        cart.MapPut("/items/{productId:guid}", async (Guid productId, SetCartItemRequest request, ClaimsPrincipal user, SetCartItemHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), productId, request.Quantity, ct)).ToHttp())
            .Validate<SetCartItemRequest>()
            .WithName("SetCartItem").WithSummary("Adds the product or sets its quantity (1–20). Prices are never accepted from the client.")
            .Produces<CartDto>().ProducesProblem(StatusCodes.Status409Conflict);

        cart.MapDelete("/items/{productId:guid}", async (Guid productId, ClaimsPrincipal user, RemoveCartItemHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), productId, ct)).ToHttp())
            .WithName("RemoveCartItem").Produces<CartDto>().ProducesProblem(StatusCodes.Status404NotFound);

        cart.MapDelete("", async (ClaimsPrincipal user, ClearCartHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.RequiredUserId(), ct);
                return Results.NoContent();
            })
            .WithName("ClearCart").Produces(StatusCodes.Status204NoContent);

        api.MapPost("/checkout", async (CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                    ClaimsPrincipal user, PlaceCheckoutHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), idempotencyKey, request, ct)).ToHttp())
            .RequireAuthorization()
            .Validate<CheckoutRequest>()
            .WithTags("Checkout").WithName("PlaceCheckout")
            .WithSummary("Creates one PendingPayment order per shop from the cart. Requires an Idempotency-Key header; repeating the request returns the original result (replayed=true).")
            .Produces<CheckoutResultDto>().ProducesProblem(StatusCodes.Status409Conflict);

        var mine = api.MapGroup("/me/orders").WithTags("Customer: Orders").RequireAuthorization();

        mine.MapGet("", async (OrderStatus? status, [AsParameters] PagingParameters paging, ClaimsPrincipal user, ListCustomerOrdersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(user.RequiredUserId(), status, paging.ToPageRequest(), ct)))
            .WithName("ListMyOrders").Produces<PagedResult<OrderSummaryDto>>();

        mine.MapGet("/{orderNumber}", async (string orderNumber, ClaimsPrincipal user, GetCustomerOrderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), orderNumber, ct)).ToHttp())
            .WithName("GetMyOrder").WithSummary("404 also for orders of other customers.")
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);

        mine.MapPost("/{orderNumber}/cancel", async (string orderNumber, ReasonRequest request, ClaimsPrincipal user, CancelCustomerOrderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), orderNumber, request.Reason, ct)).ToHttp())
            .Validate<ReasonRequest>()
            .WithName("CancelMyOrder").WithSummary("Allowed until the shop accepts the order; stock is released.")
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var admin = api.MapGroup("/admin/orders").WithTags("Admin: Orders");

        admin.MapGet("", async (OrderStatus? status, Guid? shopId, [AsParameters] PagingParameters paging, ListOrdersForAdminHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(status, shopId, paging.ToPageRequest(), ct)))
            .RequirePermission(Permissions.OrdersRead)
            .WithName("AdminListOrders").Produces<PagedResult<OrderSummaryDto>>();

        admin.MapGet("/{orderNumber}", async (string orderNumber, GetOrderForAdminHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(orderNumber, ct)).ToHttp())
            .RequirePermission(Permissions.OrdersRead)
            .WithName("AdminGetOrder").Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{orderNumber}/record-payment", async (string orderNumber, RecordPaymentRequest request, ClaimsPrincipal user,
                    RecordManualPaymentHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), orderNumber, request.Reference, ct)).ToHttp())
            .RequirePermission(Permissions.OrdersManage)
            .Validate<RecordPaymentRequest>()
            .WithName("RecordManualPayment")
            .WithSummary("Manual payment confirmation (e.g. bank transfer): PendingPayment → Paid. Audited. Not a payment-provider integration.")
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{orderNumber}/cancel", async (string orderNumber, ReasonRequest request, ClaimsPrincipal user,
                    AdminCancelOrderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(user.RequiredUserId(), orderNumber, request.Reason, ct)).ToHttp())
            .RequirePermission(Permissions.OrdersManage)
            .Validate<ReasonRequest>()
            .WithName("AdminCancelOrder").WithSummary("Cancels any non-terminal order; stock released; refund due recorded if paid. Audited.")
            .Produces<OrderDetailDto>().ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
