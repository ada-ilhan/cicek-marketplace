using Cicek.Domain.Common;
using Cicek.Domain.Media;
using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Catalog;

public enum ProductStatus
{
    Active,

    /// <summary>Withdrawn by the shop; kept for order history, never purchasable.</summary>
    Inactive,
}

/// <summary>
/// A product listed by exactly one shop. Purchasable only when the product is active, has stock and its shop
/// accepts orders. Stock is decremented atomically by the persistence layer at checkout (see ADR-0012), so
/// this entity never decrements stock in memory.
/// </summary>
public sealed class Product : Entity
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int MaxImages = 8;
    public const int MaxStock = 100_000;
    public static readonly decimal MaxPrice = 1_000_000m;

    private readonly List<ProductImage> _images = [];

    private Product(Guid id, Guid shopId, DateTimeOffset now)
        : base(id)
    {
        ShopId = shopId;
        Status = ProductStatus.Active;
        CreatedAt = now;
    }

    private Product()
    {
    }

    public Guid ShopId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal PriceAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public int StockQuantity { get; private set; }

    public ProductStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ProductImage> Images => _images;

    public Money Price => new(PriceAmount, Currency.FromCode(CurrencyCode));

    public static Product Create(
        Guid shopId,
        Category category,
        string name,
        string description,
        Money price,
        int stockQuantity,
        DateTimeOffset now)
    {
        if (shopId == Guid.Empty)
        {
            throw new DomainException("product.shop_required", "A product belongs to a shop.");
        }

        var product = new Product(Guid.CreateVersion7(), shopId, now);
        product.UpdateDetails(category, name, description, price, now);
        product.SetStock(stockQuantity, now);
        return product;
    }

    public void UpdateDetails(Category category, string name, string description, Money price, DateTimeOffset now)
    {
        // An already assigned, since-deactivated category may be kept, but not newly chosen.
        if (!category.IsActive && category.Id != CategoryId)
        {
            throw new DomainException("product.category_inactive", "The category is not active.");
        }

        if (price.Amount <= 0m || price.Amount > MaxPrice || decimal.Round(price.Amount, price.Currency.MinorUnits) != price.Amount)
        {
            throw new DomainException("product.invalid_price", "Price must be positive, within limits and use the currency's minor units.");
        }

        CategoryId = category.Id;
        Name = Guard.RequireText(name, NameMaxLength, "product.name_required");
        NormalizedName = SearchText.Normalize(Name);
        Description = Guard.OptionalText(description, DescriptionMaxLength);
        PriceAmount = price.Amount;
        CurrencyCode = price.Currency.Code;
        UpdatedAt = now;
    }

    public void SetStock(int quantity, DateTimeOffset now)
    {
        if (quantity is < 0 or > MaxStock)
        {
            throw new DomainException("product.invalid_stock", $"Stock must be between 0 and {MaxStock}.");
        }

        StockQuantity = quantity;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = ProductStatus.Active;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        Status = ProductStatus.Inactive;
        UpdatedAt = now;
    }

    /// <summary>Replaces the image list (order = display order). URLs are validated by the application layer.</summary>
    public void ReplaceImages(IReadOnlyList<(string Url, string? AltText)> images, DateTimeOffset now)
    {
        if (images.Count > MaxImages)
        {
            throw new DomainException("product.too_many_images", $"A product can have at most {MaxImages} images.");
        }

        _images.Clear();
        for (var i = 0; i < images.Count; i++)
        {
            _images.Add(new ProductImage(Id, images[i].Url, images[i].AltText, i));
        }

        UpdatedAt = now;
    }

    public bool IsPurchasable(bool shopAcceptsOrders) =>
        shopAcceptsOrders && Status == ProductStatus.Active && StockQuantity > 0;
}

/// <summary>Image metadata: an HTTPS URL to externally stored media, never a file path or binary.</summary>
public sealed class ProductImage
{
    public const int UrlMaxLength = 1000;
    public const int AltTextMaxLength = 200;

    internal ProductImage(Guid productId, string url, string? altText, int sortOrder)
    {
        var normalized = ImageReference.Normalize(url);
        if (normalized is null)
        {
            throw new DomainException("product.invalid_image_url", "Image must be an uploaded platform image or an absolute HTTPS URL.");
        }

        Id = Guid.CreateVersion7();
        ProductId = productId;
        Url = normalized;
        AltText = Guard.OptionalText(altText, AltTextMaxLength);
        SortOrder = sortOrder;
    }

    private ProductImage()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string? AltText { get; private set; }

    public int SortOrder { get; private set; }
}
