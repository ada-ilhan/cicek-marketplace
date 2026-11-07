using Cicek.Domain.Catalog;
using Cicek.Domain.Commissions;
using Cicek.Domain.Shops;
using Cicek.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cicek.Infrastructure.Persistence.Configurations;

internal sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("SHP_SHOPS", table =>
        {
            table.HasCheckConstraint("CK_SHP_SHOPS_STATUS", OracleCheckConstraints.InList("STATUS", Enum.GetNames<ShopStatus>(), allowNull: false));
            table.HasCheckConstraint(
                "CK_SHP_SHOPS_COMMISSION",
                $"COMMISSION_RATE_BPS IS NULL OR COMMISSION_RATE_BPS BETWEEN 0 AND {CommissionRate.MaxBasisPoints}");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(Shop.NameMaxLength);
        builder.Property(s => s.NormalizedName).HasMaxLength(Shop.NameMaxLength);
        builder.Property(s => s.Slug).HasMaxLength(Shop.SlugMaxLength).IsUnicode(false);
        builder.Property(s => s.Description).HasMaxLength(Shop.DescriptionMaxLength);
        builder.Property(s => s.ContactEmail).HasMaxLength(Shop.EmailMaxLength);
        builder.Property(s => s.ContactPhone).HasMaxLength(Shop.PhoneMaxLength).IsUnicode(false);
        builder.Property(s => s.AddressLine).HasMaxLength(Shop.AddressLineMaxLength);
        builder.Property(s => s.District).HasMaxLength(Shop.DistrictMaxLength);
        builder.Property(s => s.City).HasMaxLength(Shop.CityMaxLength);
        builder.Property(s => s.PostalCode).HasMaxLength(Shop.PostalCodeMaxLength).IsUnicode(false);
        builder.Property(s => s.ImageUrl).HasMaxLength(ProductImage.UrlMaxLength).IsUnicode(false);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(s => s.StatusReason).HasMaxLength(Shop.StatusReasonMaxLength);

        builder.HasIndex(s => s.Slug).IsUnique();
        builder.HasIndex(s => s.Status);

        builder.HasMany(s => s.Members).WithOne().HasForeignKey(m => m.ShopId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<long>(AppDbContext.RowVersionProperty).IsConcurrencyToken();
    }
}

internal sealed class ShopMemberConfiguration : IEntityTypeConfiguration<ShopMember>
{
    public void Configure(EntityTypeBuilder<ShopMember> builder)
    {
        builder.ToTable("SHP_SHOP_MEMBERS", table =>
            table.HasCheckConstraint("CK_SHP_SHOP_MEMBERS_ROLE", OracleCheckConstraints.InList("ROLE", Enum.GetNames<ShopMemberRole>(), allowNull: false)));

        builder.HasKey(m => new { m.ShopId, m.UserId });
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.HasIndex(m => m.UserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
