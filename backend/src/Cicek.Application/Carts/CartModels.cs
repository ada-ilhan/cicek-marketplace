using Cicek.Domain.Carts;
using FluentValidation;

namespace Cicek.Application.Carts;

public sealed record SetCartItemRequest(int Quantity);

/// <summary>Why a cart line cannot be bought right now.</summary>
public enum CartLineIssue
{
    None,
    ProductUnavailable,
    ShopClosed,
    InsufficientStock,
}

public sealed record CartLineDto(
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableQuantity,
    CartLineIssue Issue);

/// <summary>One shop's part of the cart: becomes one order at checkout.</summary>
public sealed record CartShopGroupDto(
    Guid ShopId,
    string ShopName,
    IReadOnlyList<CartLineDto> Lines,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total);

/// <summary>Server-computed cart. Totals include only lines without issues; <see cref="CanCheckout"/> requires none.</summary>
public sealed record CartDto(
    IReadOnlyList<CartShopGroupDto> Shops,
    string Currency,
    decimal Total,
    int ItemCount,
    bool CanCheckout);

public sealed class SetCartItemRequestValidator : AbstractValidator<SetCartItemRequest>
{
    public SetCartItemRequestValidator() => RuleFor(x => x.Quantity).InclusiveBetween(1, Cart.MaxQuantityPerItem);
}
