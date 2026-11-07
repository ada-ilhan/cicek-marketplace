using Cicek.Domain.Media;
using Cicek.Domain.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cicek.Infrastructure.Persistence.Configurations;

/// <summary>Image metadata only; the bytes live in the configured <c>IImageStorage</c>, never in Oracle.</summary>
internal sealed class MediaImageConfiguration : IEntityTypeConfiguration<MediaImage>
{
    public void Configure(EntityTypeBuilder<MediaImage> builder)
    {
        builder.ToTable("MED_IMAGES", table =>
        {
            table.HasCheckConstraint("CK_MED_IMAGES_SIZE", "SIZE_BYTES > 0");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.StorageKey).HasMaxLength(MediaImage.StorageKeyMaxLength).IsUnicode(false);
        builder.Property(m => m.ContentType).HasMaxLength(32).IsUnicode(false);
        builder.HasIndex(m => m.ShopId);
        builder.HasIndex(m => m.StorageKey).IsUnique();
        builder.HasOne<Shop>().WithMany().HasForeignKey(m => m.ShopId).OnDelete(DeleteBehavior.Restrict);
    }
}
