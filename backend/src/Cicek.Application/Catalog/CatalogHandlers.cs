using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Application.Shops;
using Cicek.Domain.Auditing;
using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Common;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Monetary;
using Cicek.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Catalog;

public static class CatalogErrors
{
    public static readonly Error ProductNotFound = Error.NotFound("product.not_found", "Product not found.");
    public static readonly Error CategoryNotFound = Error.Validation("category.not_found", "The category does not exist.");
    public static readonly Error CategoryInactive = Error.Validation("category.inactive", "The category is not active.");
    public static readonly Error CategoryExists = Error.Conflict("category.exists", "A category with this name already exists.");
    public static readonly Error ParentNotFound = Error.Validation("category.parent_not_found", "The parent category does not exist or is not top-level.");
    public static readonly Error VersionRequired = Error.Validation("product.version_required", "The current version is required to update a product.");
}

public sealed class ListCategoriesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(bool includeInactive, CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.ParentId != null).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
}

/// <summary>Public catalogue search: active products of active shops only, paged, with one SQL query for the page.</summary>
public sealed class SearchProductsHandler(IAppDbContext db)
{
    public async Task<PagedResult<ProductCardDto>> HandleAsync(ProductSearchQuery query, PageRequest page, CancellationToken ct)
    {
        var products =
            from p in db.Products.AsNoTracking()
            join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
            where p.Status == ProductStatus.Active && s.Status == ShopStatus.Active
            select new { Product = p, Shop = s };

        var search = SearchText.Normalize(query.Search);
        if (search.Length > 0)
        {
            products = products.Where(x => x.Product.NormalizedName.Contains(search));
        }

        if (query.CategoryId is { } categoryId)
        {
            // A top-level category includes its sub-categories.
            var ids = await db.Categories.AsNoTracking()
                .Where(c => c.Id == categoryId || c.ParentId == categoryId)
                .Select(c => c.Id)
                .ToListAsync(ct);
            products = products.Where(x => ids.Contains(x.Product.CategoryId));
        }

        if (query.ShopId is { } shopId)
        {
            products = products.Where(x => x.Product.ShopId == shopId);
        }

        if (query.MinPrice is { } min)
        {
            products = products.Where(x => x.Product.PriceAmount >= min);
        }

        if (query.MaxPrice is { } max)
        {
            products = products.Where(x => x.Product.PriceAmount <= max);
        }

        if (query.InStockOnly)
        {
            products = products.Where(x => x.Product.StockQuantity > 0);
        }

        products = query.Sort switch
        {
            ProductSort.PriceAscending => products.OrderBy(x => x.Product.PriceAmount).ThenBy(x => x.Product.Id),
            ProductSort.PriceDescending => products.OrderByDescending(x => x.Product.PriceAmount).ThenBy(x => x.Product.Id),
            ProductSort.Name => products.OrderBy(x => x.Product.NormalizedName).ThenBy(x => x.Product.Id),
            _ => products.OrderByDescending(x => x.Product.CreatedAt).ThenBy(x => x.Product.Id),
        };

        var total = await products.CountAsync(ct);
        var items = await products
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(x => new ProductCardDto(
                x.Product.Id,
                x.Product.Name,
                x.Product.PriceAmount,
                x.Product.CurrencyCode,
                x.Product.CategoryId,
                x.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                x.Product.StockQuantity > 0,
                new ShopRefDto(x.Shop.Id, x.Shop.Name, x.Shop.Slug)))
            .ToListAsync(ct);

        return new PagedResult<ProductCardDto>(items, page.SafePage, page.SafePageSize, total);
    }
}

public sealed class GetProductHandler(IAppDbContext db)
{
    public async Task<Result<ProductDetailDto>> HandleAsync(Guid productId, CancellationToken ct)
    {
        var row = await (
                from p in db.Products.AsNoTracking().Include(p => p.Images)
                join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
                where p.Id == productId && p.Status == ProductStatus.Active && s.Status == ShopStatus.Active
                select new { Product = p, Shop = s })
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        var product = row.Product;
        return new ProductDetailDto(
            product.Id, product.Name, product.Description, product.PriceAmount, product.CurrencyCode, product.CategoryId,
            product.StockQuantity > 0,
            Math.Min(product.StockQuantity, Cart.MaxQuantityPerItem),
            [.. product.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Url, i.AltText))],
            new ShopRefDto(row.Shop.Id, row.Shop.Name, row.Shop.Slug));
    }
}

// ---- Shop catalogue management (store-scoped) -----------------------------------------------------

internal static class ProductMapping
{
    public static ShopProductDto ToShopDto(this Product p, long version) => new(
        p.Id, p.CategoryId, p.Name, p.Description, p.PriceAmount, p.CurrencyCode, p.StockQuantity, p.Status,
        [.. p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Url, i.AltText))], p.UpdatedAt, version);
}

public sealed class ListShopProductsHandler(ShopAccess access, IAppDbContext db)
{
    public async Task<Result<PagedResult<ShopProductDto>>> HandleAsync(Guid shopId, Guid userId, ProductStatus? status, string? search, bool? outOfStock, PageRequest page, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageCatalog, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var query = db.Products.Include(p => p.Images).Where(p => p.ShopId == shopId);
        if (status is { } s)
        {
            query = query.Where(p => p.Status == s);
        }

        var normalized = Domain.Common.SearchText.Normalize(search);
        if (normalized.Length > 0)
        {
            query = query.Where(p => p.NormalizedName.Contains(normalized));
        }

        if (outOfStock == true)
        {
            query = query.Where(p => p.StockQuantity == 0);
        }

        var total = await query.CountAsync(ct);
        var products = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<ShopProductDto>([.. products.Select(p => p.ToShopDto(db.GetVersion(p)))], page.SafePage, page.SafePageSize, total);
    }
}

public sealed class GetShopProductHandler(ShopAccess access, IAppDbContext db)
{
    public async Task<Result<ShopProductDto>> HandleAsync(Guid shopId, Guid productId, Guid userId, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageCatalog, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var product = await db.Products.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == productId && p.ShopId == shopId, ct);
        return product is null ? CatalogErrors.ProductNotFound : product.ToShopDto(db.GetVersion(product));
    }
}

/// <summary>Creates or updates a product of the caller's shop. Prices are always in the marketplace currency.</summary>
public sealed class SaveShopProductHandler(
    ShopAccess access, IAppDbContext db, TimeProvider time, IOptions<MarketplaceOptions> options, Media.MediaReferenceValidator media)
{
    public async Task<Result<ShopProductDto>> HandleAsync(Guid shopId, Guid? productId, Guid userId, ProductUpsertRequest request, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageCatalog, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        // An uploaded platform image may only be used by the shop that owns it.
        var imageCheck = await media.ValidateAsync(shopId, (request.Images ?? []).Select(i => i.Url), ct);
        if (!imageCheck.IsSuccess)
        {
            return imageCheck.Error;
        }

        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
        {
            return CatalogErrors.CategoryNotFound;
        }

        var now = time.GetUtcNow();
        var price = new Money(request.Price, options.Value.MarketCurrency);
        var images = (request.Images ?? []).Select(i => (i.Url, i.AltText)).ToList();
        Product product;

        if (productId is null)
        {
            if (!category.IsActive)
            {
                return CatalogErrors.CategoryInactive;
            }

            product = Product.Create(shopId, category, request.Name, request.Description ?? string.Empty, price, request.StockQuantity, now);
            product.ReplaceImages(images, now);
            db.Products.Add(product);
        }
        else
        {
            if (request.Version is not { } version)
            {
                return CatalogErrors.VersionRequired;
            }

            var existing = await db.Products.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == productId && p.ShopId == shopId, ct);
            if (existing is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            if (!category.IsActive && category.Id != existing.CategoryId)
            {
                return CatalogErrors.CategoryInactive;
            }

            product = existing;
            db.SetExpectedVersion(product, version);
            product.UpdateDetails(category, request.Name, request.Description ?? string.Empty, price, now);
            product.SetStock(request.StockQuantity, now);
            product.ReplaceImages(images, now);
        }

        await db.SaveChangesAsync(ct);
        return product.ToShopDto(db.GetVersion(product));
    }
}

public sealed class SetShopProductStatusHandler(ShopAccess access, IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ShopProductDto>> HandleAsync(Guid shopId, Guid productId, Guid userId, bool active, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(shopId, userId, ShopPermission.ManageCatalog, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var product = await db.Products.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == productId && p.ShopId == shopId, ct);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        if (active)
        {
            product.Activate(time.GetUtcNow());
        }
        else
        {
            product.Deactivate(time.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        return product.ToShopDto(db.GetVersion(product));
    }
}

// ---- Category administration ------------------------------------------------------------------------

public sealed class CreateCategoryHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<CategoryDto>> HandleAsync(Guid adminId, CreateCategoryRequest request, CancellationToken ct)
    {
        Category? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await db.Categories.SingleOrDefaultAsync(c => c.Id == parentId && c.ParentId == null, ct);
            if (parent is null)
            {
                return CatalogErrors.ParentNotFound;
            }
        }

        var slug = SearchText.Slugify(request.Name, Category.SlugMaxLength);
        if (await db.Categories.AnyAsync(c => c.Slug == slug, ct))
        {
            return CatalogErrors.CategoryExists;
        }

        var category = Category.Create(request.Name, slug, parent, request.SortOrder);
        db.Categories.Add(category);
        db.AuditEntries.Add(new AuditEntry(adminId, "category.created", "Category", category.Id.ToString(), category.Name, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentId, category.SortOrder, category.IsActive);
    }
}

public sealed class UpdateCategoryHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<CategoryDto>> HandleAsync(Guid adminId, Guid categoryId, UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, ct);
        if (category is null)
        {
            return Error.NotFound("category.not_found", "Category not found.");
        }

        category.Update(request.Name, request.SortOrder, request.IsActive);
        db.AuditEntries.Add(new AuditEntry(adminId, "category.updated", "Category", category.Id.ToString(),
            $"{category.Name}; active={category.IsActive}", time.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentId, category.SortOrder, category.IsActive);
    }
}
