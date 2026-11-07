using Cicek.Domain.Catalog;
using FluentValidation;

namespace Cicek.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string Slug, Guid? ParentId, int SortOrder, bool IsActive);

public sealed record CreateCategoryRequest(string Name, Guid? ParentId, int SortOrder);

public sealed record UpdateCategoryRequest(string Name, int SortOrder, bool IsActive);

public sealed record ShopRefDto(Guid Id, string Name, string Slug);

public sealed record ProductCardDto(
    Guid Id,
    string Name,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? ImageUrl,
    bool InStock,
    ShopRefDto Shop);

public sealed record ProductImageDto(string Url, string? AltText);

public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    bool InStock,
    /// <summary>Most a customer can put in the cart right now (bounded by stock and the per-item limit).</summary>
    int MaxOrderQuantity,
    IReadOnlyList<ProductImageDto> Images,
    ShopRefDto Shop);

public enum ProductSort
{
    Newest,
    PriceAscending,
    PriceDescending,
    Name,
}

public sealed record ProductSearchQuery(
    string? Search,
    Guid? CategoryId,
    Guid? ShopId,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool InStockOnly,
    ProductSort Sort);

public sealed record ProductImageInput(string Url, string? AltText);

/// <summary>Create/update input for a shop's product. <see cref="Version"/> is required for updates (optimistic concurrency).</summary>
public sealed record ProductUpsertRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    IReadOnlyList<ProductImageInput>? Images,
    long? Version);

/// <summary>A shop's own view of its product, including stock and inactive products.</summary>
public sealed record ShopProductDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    ProductStatus Status,
    IReadOnlyList<ProductImageDto> Images,
    DateTimeOffset UpdatedAt,
    long Version);

public sealed class ProductUpsertRequestValidator : AbstractValidator<ProductUpsertRequest>
{
    public ProductUpsertRequestValidator(ImageUrlPolicy imagePolicy)
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Product.DescriptionMaxLength);
        RuleFor(x => x.Price).GreaterThan(0m).LessThanOrEqualTo(Product.MaxPrice).PrecisionScale(9, 2, ignoreTrailingZeros: true)
            .WithMessage("Price must be positive, at most 1,000,000 and have at most two decimals.");
        RuleFor(x => x.StockQuantity).InclusiveBetween(0, Product.MaxStock);
        RuleFor(x => x.Images).Must(i => i is null || i.Count <= Product.MaxImages).WithMessage($"At most {Product.MaxImages} images.");
        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.Url).Must(imagePolicy.IsAllowed).WithMessage("Image URL must be HTTPS on an allowed host.");
            image.RuleFor(i => i.AltText).MaximumLength(ProductImage.AltTextMaxLength);
        });
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0).When(x => x.Version is not null);
    }
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength)
            .Must(n => Domain.Common.SearchText.Slugify(n).Length > 0).WithMessage("Name must contain letters or digits.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10_000);
    }
}
