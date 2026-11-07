using Cicek.Domain.Auditing;
using Cicek.Domain.Carts;
using Cicek.Domain.Catalog;
using Cicek.Domain.Media;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cicek.Application.Abstractions.Persistence;

/// <summary>
/// Unit of work and query surface for use cases (ADR-0003). Exposes EF Core abstractions only; Oracle-specific
/// behaviour stays in Infrastructure behind dedicated ports such as <see cref="IProductStock"/>.
/// </summary>
public interface IAppDbContext
{
    DbSet<Shop> Shops { get; }

    DbSet<ShopMember> ShopMembers { get; }

    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<Cart> Carts { get; }

    DbSet<Checkout> Checkouts { get; }

    DbSet<Order> Orders { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    DbSet<MediaImage> MediaImages { get; }

    DatabaseFacade Database { get; }

    /// <summary>Used to discard a failed unit of work before retrying.</summary>
    ChangeTracker ChangeTracker { get; }

    /// <exception cref="ConcurrencyConflictException">An optimistic concurrency check failed.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Sets the version the client last read, so the update fails if someone else changed the row since.</summary>
    void SetExpectedVersion(object entity, long version);

    /// <summary>Current optimistic-concurrency version of a tracked entity (exposed to clients for updates).</summary>
    long GetVersion(object entity);
}

/// <summary>Outcome of a stock reservation attempt.</summary>
public enum StockReservation
{
    Reserved,
    InsufficientStock,
    PriceChanged,
    Unavailable,
}

/// <summary>
/// Atomic stock changes. Implemented as single conditional UPDATE statements so concurrent checkouts can never
/// oversell (no read-then-write race), independent of the caller's isolation level.
/// </summary>
public interface IProductStock
{
    /// <summary>
    /// Decrements stock in one conditional statement that also requires the product to be active, its shop to be
    /// active and its price to still equal <paramref name="expectedUnitPrice"/> (the price the order was quoted at).
    /// </summary>
    Task<StockReservation> TryReserveAsync(Guid productId, int quantity, decimal expectedUnitPrice, CancellationToken cancellationToken);

    /// <summary>Returns stock after an order ended without delivery.</summary>
    Task ReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken);
}
