using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Carts;

/// <summary>
/// A customer's cart. It stores only product ids and quantities: prices, availability and totals are always
/// recalculated on the server from current product data, never taken from the client or cached here.
/// Items from several shops are allowed; checkout splits them into one order per shop.
/// </summary>
public sealed class Cart : Entity
{
    public const int MaxQuantityPerItem = 20;
    public const int MaxDistinctItems = 50;

    private readonly List<CartItem> _items = [];

    private Cart(Guid id, Guid customerId, DateTimeOffset now)
        : base(id)
    {
        CustomerId = customerId;
        UpdatedAt = now;
    }

    private Cart()
    {
    }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<CartItem> Items => _items;

    public static Cart Create(Guid customerId, DateTimeOffset now) =>
        customerId == Guid.Empty
            ? throw new DomainException("cart.customer_required", "A cart belongs to a customer.")
            : new Cart(Guid.CreateVersion7(), customerId, now);

    /// <summary>Adds the product or replaces its quantity.</summary>
    public void SetQuantity(Guid productId, int quantity, DateTimeOffset now)
    {
        if (quantity is < 1 or > MaxQuantityPerItem)
        {
            throw new DomainException("cart.invalid_quantity", $"Quantity must be between 1 and {MaxQuantityPerItem}.");
        }

        var existing = _items.Find(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.Quantity = quantity;
        }
        else
        {
            if (_items.Count >= MaxDistinctItems)
            {
                throw new DomainException("cart.too_many_items", $"A cart can hold at most {MaxDistinctItems} different products.");
            }

            _items.Add(new CartItem(Id, productId, quantity, now));
        }

        UpdatedAt = now;
    }

    public bool Remove(Guid productId, DateTimeOffset now)
    {
        var removed = _items.RemoveAll(i => i.ProductId == productId) > 0;
        if (removed)
        {
            UpdatedAt = now;
        }

        return removed;
    }

    public void Clear(DateTimeOffset now)
    {
        _items.Clear();
        UpdatedAt = now;
    }
}

public sealed class CartItem
{
    internal CartItem(Guid cartId, Guid productId, int quantity, DateTimeOffset addedAt)
    {
        Id = Guid.CreateVersion7();
        CartId = cartId;
        ProductId = productId;
        Quantity = quantity;
        AddedAt = addedAt;
    }

    private CartItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; internal set; }

    public DateTimeOffset AddedAt { get; private set; }
}
