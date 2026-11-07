using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Auditing;
using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Identity;
using Cicek.Domain.Media;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Persistence.Conventions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the modular monolith (ADR-0001). Module tables are distinguished by
/// table-name prefix (IDN_ = Identity, …) inside one Oracle schema owner (ADR-0009).
/// </summary>
public sealed class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IAppDbContext
{
    /// <summary>Name of the shadow optimistic-concurrency property added to versioned entities.</summary>
    public const string RowVersionProperty = "RowVersion";

    public AppDbContext(DbContextOptions<AppDbContext> options, DatabaseSchema schema)
        : base(options) => Schema = schema.Name;

    /// <summary>Owner schema used to qualify objects, or null when connected as the owner.</summary>
    public string? Schema { get; }

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<ShopMember> ShopMembers => Set<ShopMember>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Checkout> Checkouts => Set<Checkout>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<MediaImage> MediaImages => Set<MediaImage>();

    async Task<int> IAppDbContext.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The data was modified by another request.", ex);
        }
    }

    public void SetExpectedVersion(object entity, long version) =>
        Entry(entity).Property(RowVersionProperty).OriginalValue = version;

    public long GetVersion(object entity) => (long)Entry(entity).Property(RowVersionProperty).CurrentValue!;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IncrementRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money and other decimals: NUMBER(19,4) unless configured otherwise (provider default is NUMBER(18,2)).
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        if (Schema is not null)
        {
            builder.HasDefaultSchema(Schema);
        }

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        OracleNamingConvention.Apply(builder);
    }

    /// <summary>
    /// Oracle has no automatically maintained row version (unlike SQL Server rowversion or PostgreSQL xmin),
    /// so versioned entities carry a NUMBER(19) shadow column incremented on every update. EF Core includes
    /// the original value in the UPDATE's WHERE clause and raises DbUpdateConcurrencyException on mismatch.
    /// </summary>
    private void IncrementRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified || entry.Metadata.FindProperty(RowVersionProperty) is null)
            {
                continue;
            }

            var property = entry.Property(RowVersionProperty);
            property.CurrentValue = (long)property.OriginalValue! + 1;
        }
    }
}

/// <summary>DI-provided owner schema for <see cref="AppDbContext"/>.</summary>
public sealed record DatabaseSchema(string? Name)
{
    public static readonly DatabaseSchema Owner = new((string?)null);
}
