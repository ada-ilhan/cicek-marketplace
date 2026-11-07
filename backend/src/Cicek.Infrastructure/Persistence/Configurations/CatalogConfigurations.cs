using Cicek.Domain.Catalog;
using Cicek.Domain.Shops;
using Cicek.SharedKernel.Monetary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cicek.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("CAT_CATEGORIES");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength);
        builder.Property(c => c.Slug).HasMaxLength(Category.SlugMaxLength).IsUnicode(false);
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasData(CategorySeed.All);
    }
}

/// <summary>Initial reference categories (fixed ids so the migration is deterministic). Names are Azerbaijani (Latin); slugs stay stable identifiers. Administrators manage them afterwards.</summary>
internal static class CategorySeed
{
    public static readonly Guid Flowers = new("0199d6a0-0000-7000-8000-000000000001");
    public static readonly Guid Gifts = new("0199d6a0-0000-7000-8000-000000000002");

    public static readonly object[] All =
    [
        Row(Flowers, "Çiçəklər", "cicekler", null, 1),
        Row(Gifts, "Hədiyyələr", "hediyeler", null, 2),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000011"), "Qızılgüllər", "guller", Flowers, 1),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000012"), "Orxideyalar", "orkideler", Flowers, 2),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000013"), "Buketlər", "buketler", Flowers, 3),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000014"), "Qabda çiçəklər", "saksi-cicekleri", Flowers, 4),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000021"), "Şokoladlar", "cikolatalar", Gifts, 1),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000022"), "Hədiyyə səbətləri", "hediye-sepetleri", Gifts, 2),
        Row(new Guid("0199d6a0-0000-7000-8000-000000000023"), "Yumşaq oyuncaqlar", "pelus-oyuncaklar", Gifts, 3),
    ];

    private static object Row(Guid id, string name, string slug, Guid? parentId, int sortOrder) =>
        new { Id = id, Name = name, Slug = slug, ParentId = parentId, SortOrder = sortOrder, IsActive = true };
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("CAT_PRODUCTS", table =>
        {
            table.HasCheckConstraint("CK_CAT_PRODUCTS_STATUS", OracleCheckConstraints.InList("STATUS", Enum.GetNames<ProductStatus>(), allowNull: false));
            table.HasCheckConstraint("CK_CAT_PRODUCTS_CURRENCY", OracleCheckConstraints.InList("CURRENCY_CODE", Currency.SupportedCodes, allowNull: false));
            table.HasCheckConstraint("CK_CAT_PRODUCTS_PRICE", "PRICE_AMOUNT > 0");
            // Last line of defence against overselling, whatever the access path (ADR-0012).
            table.HasCheckConstraint("CK_CAT_PRODUCTS_STOCK", "STOCK_QUANTITY >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.NormalizedName).HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(p => p.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Ignore(p => p.Price);

        builder.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // Query patterns: public listing (status + category / shop), shop catalogue management.
        builder.HasIndex(p => new { p.Status, p.CategoryId });
        builder.HasIndex(p => new { p.ShopId, p.Status });

        builder.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<long>(AppDbContext.RowVersionProperty).IsConcurrencyToken();
    }
}

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("CAT_PRODUCT_IMAGES");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Url).HasMaxLength(ProductImage.UrlMaxLength).IsUnicode(false);
        builder.Property(i => i.AltText).HasMaxLength(ProductImage.AltTextMaxLength);
        builder.HasIndex(i => i.ProductId);
    }
}
