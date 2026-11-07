using Cicek.Domain.Auditing;
using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Commissions;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.Infrastructure.Identity;
using Cicek.SharedKernel.Monetary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cicek.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("CRT_CARTS");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.CustomerId).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property<long>(AppDbContext.RowVersionProperty).IsConcurrencyToken();
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CRT_CART_ITEMS", table =>
            table.HasCheckConstraint("CK_CRT_CART_ITEMS_QUANTITY", $"QUANTITY BETWEEN 1 AND {Cart.MaxQuantityPerItem}"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.HasIndex(i => new { i.CartId, i.ProductId }).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CheckoutConfiguration : IEntityTypeConfiguration<Checkout>
{
    public void Configure(EntityTypeBuilder<Checkout> builder)
    {
        builder.ToTable("ORD_CHECKOUTS", table =>
            table.HasCheckConstraint("CK_ORD_CHECKOUTS_CURRENCY", OracleCheckConstraints.InList("CURRENCY_CODE", Currency.SupportedCodes, allowNull: false)));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.IdempotencyKey).HasMaxLength(Checkout.IdempotencyKeyMaxLength).IsUnicode(false);
        builder.Property(c => c.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false);

        // Duplicate submissions with the same key resolve to the same checkout (idempotency).
        builder.HasIndex(c => new { c.CustomerId, c.IdempotencyKey }).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("ORD_ORDERS", table =>
        {
            table.HasCheckConstraint("CK_ORD_ORDERS_STATUS", OracleCheckConstraints.InList("STATUS", Enum.GetNames<OrderStatus>(), allowNull: false));
            table.HasCheckConstraint("CK_ORD_ORDERS_CURRENCY", OracleCheckConstraints.InList("CURRENCY_CODE", Currency.SupportedCodes, allowNull: false));
            table.HasCheckConstraint("CK_ORD_ORDERS_COMMISSION_RATE", $"COMMISSION_RATE_BPS BETWEEN 0 AND {CommissionRate.MaxBasisPoints}");

            // Financial invariants enforced by the database for every access path.
            table.HasCheckConstraint("CK_ORD_ORDERS_TOTAL", "TOTAL_AMOUNT = SUBTOTAL_AMOUNT + DELIVERY_FEE_AMOUNT - DISCOUNT_AMOUNT");
            table.HasCheckConstraint("CK_ORD_ORDERS_COMMISSION_SPLIT", "COMMISSION_AMOUNT + SHOP_EARNINGS_AMOUNT = COMMISSION_BASE_AMOUNT");
            table.HasCheckConstraint(
                "CK_ORD_ORDERS_AMOUNTS",
                "SUBTOTAL_AMOUNT >= 0 AND DELIVERY_FEE_AMOUNT >= 0 AND DISCOUNT_AMOUNT >= 0 AND COMMISSION_AMOUNT >= 0 AND REFUND_DUE_AMOUNT BETWEEN 0 AND TOTAL_AMOUNT");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.OrderNumber).HasMaxLength(OrderNumber.Length).IsUnicode(false);
        builder.Property(o => o.ShopName).HasMaxLength(Shop.NameMaxLength);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(o => o.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        builder.Property(o => o.RecipientName).HasMaxLength(100);
        builder.Property(o => o.RecipientPhone).HasMaxLength(32).IsUnicode(false);
        builder.Property(o => o.AddressLine).HasMaxLength(200);
        builder.Property(o => o.District).HasMaxLength(100);
        builder.Property(o => o.City).HasMaxLength(100);
        builder.Property(o => o.PostalCode).HasMaxLength(16).IsUnicode(false);
        builder.Property(o => o.GiftMessage).HasMaxLength(Order.GiftMessageMaxLength);
        builder.Property(o => o.PaymentReference).HasMaxLength(Order.PaymentReferenceMaxLength);
        builder.Property(o => o.CancellationReason).HasMaxLength(Order.ReasonMaxLength);

        // The Oracle provider does not support DateOnly; store the calendar date as Oracle DATE (ADR-0005).
        builder.Property(o => o.DeliveryDate)
            .HasConversion(new ValueConverter<DateOnly, DateTime>(d => d.ToDateTime(TimeOnly.MinValue), dt => DateOnly.FromDateTime(dt)))
            .HasColumnType("DATE");

        builder.Ignore(o => o.Currency);
        builder.Ignore(o => o.Total);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CheckoutId);
        builder.HasIndex(o => new { o.CustomerId, o.CreatedAt });
        builder.HasIndex(o => new { o.ShopId, o.Status, o.CreatedAt });
        builder.HasIndex(o => new { o.Status, o.CreatedAt }); // expiry job

        builder.HasOne<Checkout>().WithMany().HasForeignKey(o => o.CheckoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shop>().WithMany().HasForeignKey(o => o.ShopId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<long>(AppDbContext.RowVersionProperty).IsConcurrencyToken();
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("ORD_ORDER_ITEMS", table =>
        {
            table.HasCheckConstraint("CK_ORD_ORDER_ITEMS_QUANTITY", "QUANTITY > 0");
            table.HasCheckConstraint("CK_ORD_ORDER_ITEMS_LINE_TOTAL", "LINE_TOTAL_AMOUNT = UNIT_PRICE_AMOUNT * QUANTITY");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ProductName).HasMaxLength(Product.NameMaxLength);
        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ProductId);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("ORD_ORDER_STATUS_HISTORY", table =>
        {
            table.HasCheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_TO", OracleCheckConstraints.InList("TO_STATUS", Enum.GetNames<OrderStatus>(), allowNull: false));
            table.HasCheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_FROM", OracleCheckConstraints.InList("FROM_STATUS", Enum.GetNames<OrderStatus>(), allowNull: true));
            table.HasCheckConstraint("CK_ORD_ORDER_STATUS_HISTORY_ACTOR", OracleCheckConstraints.InList("ACTOR", Enum.GetNames<OrderActor>(), allowNull: false));
        });
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(h => h.Actor).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(h => h.Reason).HasMaxLength(Order.ReasonMaxLength);
        builder.HasIndex(h => h.OrderId);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AUD_AUDIT_ENTRIES");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Action).HasMaxLength(AuditEntry.ActionMaxLength).IsUnicode(false);
        builder.Property(a => a.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength).IsUnicode(false);
        builder.Property(a => a.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength).IsUnicode(false);
        builder.Property(a => a.Details).HasMaxLength(AuditEntry.DetailsMaxLength);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.OccurredAt);
    }
}
