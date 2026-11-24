using Cicek.Application;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Common;
using Cicek.Application.Ordering;
using Cicek.Domain.Catalog;
using Cicek.Domain.Commissions;
using Cicek.Domain.Ordering;
using Cicek.Domain.Shops;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Persistence;
using Cicek.SharedKernel.Monetary;
using Cicek.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Tests.Oracle;

/// <summary>Stock concurrency, append-only tables, financial constraints and order expiry on real Oracle.</summary>
[Collection(OracleCollection.Name)]
public sealed class OracleMarketplaceTests(OracleTestDatabase database)
{
    private static readonly Guid RosesCategory = new("0199d6a0-0000-7000-8000-000000000011");
    private static readonly Currency Azn = Currency.FromCode("AZN");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServiceProvider AppServices(TimeProvider? clock = null) =>
        OracleTestDatabase.CreateServices(database.RequiredSettings.AppConfiguration(), services =>
        {
            if (clock is not null)
            {
                services.AddSingleton(clock);
            }

            services.AddApplication();
            services.AddOptions<MarketplaceOptions>();
        });

    /// <summary>Creates an owner, an active shop and a product with the given stock, as the runtime user.</summary>
    private static async Task<(Guid ShopId, Guid ProductId, Guid CustomerId)> SeedAsync(ServiceProvider services, int stock, DateTimeOffset now)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = new AppUser { UserName = $"owner.{Guid.NewGuid():N}", Email = "o@example.com", CreatedAt = now };
        var customer = new AppUser { UserName = $"customer.{Guid.NewGuid():N}", Email = "c@example.com", CreatedAt = now };
        db.Users.AddRange(owner, customer);
        var shop = Shop.Apply(owner.Id, $"oracle-{Guid.NewGuid():N}"[..30], new ShopProfile("Oracle Shop", "", "s@example.com", "+905551112233", "Cad. 1", "Kadıköy", "İstanbul", ""), now);
        shop.Approve(now);
        db.Shops.Add(shop);
        var category = await db.Categories.SingleAsync(c => c.Id == RosesCategory, Ct);
        var product = Product.Create(shop.Id, category, "Güller", "", new Money(100m, Azn), stock, now);
        db.Products.Add(product);
        await db.SaveChangesAsync(Ct);
        return (shop.Id, product.Id, customer.Id);
    }

    [Fact]
    public async Task Parallel_stock_reservations_never_exceed_available_stock()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var (_, productId, _) = await SeedAsync(services, stock: 3, DateTimeOffset.UtcNow);

        async Task<bool> ReserveInOwnTransaction()
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            var ok = await scope.ServiceProvider.GetRequiredService<IProductStock>().TryReserveAsync(productId, 1, 100m, Ct) == StockReservation.Reserved;
            await tx.CommitAsync(Ct);
            return ok;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(ReserveInOwnTransaction, Ct)));

        Assert.Equal(3, results.Count(r => r));
        await using var verify = services.CreateAsyncScope();
        var stock = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking()
            .Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync(Ct);
        Assert.Equal(0, stock);
    }

    [Fact]
    public async Task Reservation_is_refused_when_the_price_changed_after_the_quote()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var (_, productId, _) = await SeedAsync(services, stock: 5, DateTimeOffset.UtcNow);

        await using var scope = services.CreateAsyncScope();
        var stock = scope.ServiceProvider.GetRequiredService<IProductStock>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);

        // Quoted at 90 while the product costs 100.
        Assert.Equal(StockReservation.PriceChanged, await stock.TryReserveAsync(productId, 1, 90m, Ct));
        Assert.Equal(StockReservation.InsufficientStock, await stock.TryReserveAsync(productId, 6, 100m, Ct));
        Assert.Equal(StockReservation.Unavailable, await stock.TryReserveAsync(Guid.NewGuid(), 1, 100m, Ct));
        await tx.RollbackAsync(Ct);

        await using var verify = services.CreateAsyncScope();
        Assert.Equal(5, await verify.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking()
            .Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync(Ct));
    }

    [Fact]
    public async Task Reservation_is_refused_when_the_shop_is_no_longer_active()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var (shopId, productId, _) = await SeedAsync(services, stock: 5, DateTimeOffset.UtcNow);

        await using (var closing = services.CreateAsyncScope())
        {
            var appDb = closing.ServiceProvider.GetRequiredService<AppDbContext>();
            var shop = await appDb.Shops.Include(s => s.Members).SingleAsync(s => s.Id == shopId, Ct);
            shop.Deactivate(DateTimeOffset.UtcNow);
            await appDb.SaveChangesAsync(Ct);
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        Assert.Equal(StockReservation.Unavailable,
            await scope.ServiceProvider.GetRequiredService<IProductStock>().TryReserveAsync(productId, 1, 100m, Ct));
        await tx.RollbackAsync(Ct);
    }

    [Fact]
    public async Task Checkout_rolls_back_all_reserved_stock_when_a_later_line_fails_on_price()
    {
        database.SkipIfUnavailable();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var services = AppServices(clock);
        var (shopId, firstId, customerId) = await SeedAsync(services, stock: 5, clock.GetUtcNow());
        Guid secondId;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.SingleAsync(c => c.Id == RosesCategory, Ct);
            var second = Product.Create(shopId, category, "Orkide", "", new Money(200m, Azn), 5, clock.GetUtcNow());
            db.Products.Add(second);
            await db.SaveChangesAsync(Ct);
            secondId = second.Id;

            var setItem = scope.ServiceProvider.GetRequiredService<Application.Carts.SetCartItemHandler>();
            Assert.True((await setItem.HandleAsync(customerId, firstId, 2, Ct)).IsSuccess);
            Assert.True((await setItem.HandleAsync(customerId, secondId, 1, Ct)).IsSuccess);
        }

        // Swap the stock port for one that changes the second product's price right before it is reserved,
        // simulating an edit in the window between quoting and reserving.
        await using var racing = AppServices(clock);
        await using var checkoutScope = racing.CreateAsyncScope();
        var realStock = checkoutScope.ServiceProvider.GetRequiredService<IProductStock>();
        var handler = new PlaceCheckoutHandler(
            checkoutScope.ServiceProvider.GetRequiredService<IAppDbContext>(),
            new PriceEditingStock(realStock, racing, secondId),
            checkoutScope.ServiceProvider.GetRequiredService<Application.Carts.CartPricer>(),
            checkoutScope.ServiceProvider.GetRequiredService<MarketCalendar>(),
            checkoutScope.ServiceProvider.GetRequiredService<IDatabaseErrorClassifier>(),
            clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaceCheckoutHandler>.Instance);
        var today = checkoutScope.ServiceProvider.GetRequiredService<MarketCalendar>().Today;
        var request = new CheckoutRequest("Ayşe", "+905550000000", "Moda Cad. 5", "Kadıköy", "İstanbul", "34710", today.AddDays(1), null);

        var result = await handler.HandleAsync(customerId, Guid.NewGuid().ToString(), request, Ct);

        Assert.Equal("checkout.price_changed", result.Error?.Code);
        await using var verify = services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await verifyDb.Products.AsNoTracking().Where(p => p.Id == firstId).Select(p => p.StockQuantity).SingleAsync(Ct));
        Assert.Equal(0, await verifyDb.Orders.CountAsync(o => o.CustomerId == customerId, Ct));
        Assert.Equal(0, await verifyDb.Checkouts.CountAsync(c => c.CustomerId == customerId, Ct));
    }

    /// <summary>Test double: edits a product's price (as another request would) just before reserving it.</summary>
    private sealed class PriceEditingStock(IProductStock inner, ServiceProvider services, Guid productToEdit) : IProductStock
    {
        public async Task<StockReservation> TryReserveAsync(Guid productId, int quantity, decimal expectedUnitPrice, CancellationToken cancellationToken)
        {
            if (productId == productToEdit)
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var product = await db.Products.SingleAsync(p => p.Id == productId, cancellationToken);
                product.UpdateDetails(
                    await db.Categories.SingleAsync(c => c.Id == product.CategoryId, cancellationToken),
                    product.Name, string.Empty, new Money(250m, Azn), DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }

            return await inner.TryReserveAsync(productId, quantity, expectedUnitPrice, cancellationToken);
        }

        public Task ReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken) =>
            inner.ReleaseAsync(productId, quantity, cancellationToken);
    }

    [Fact]
    public async Task Rolled_back_reservation_restores_stock()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var (_, productId, _) = await SeedAsync(services, stock: 2, DateTimeOffset.UtcNow);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            Assert.True(await scope.ServiceProvider.GetRequiredService<IProductStock>().TryReserveAsync(productId, 2, 100m, Ct) == StockReservation.Reserved);
            await tx.RollbackAsync(Ct);
        }

        await using var verify = services.CreateAsyncScope();
        Assert.Equal(2, await verify.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking()
            .Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("UPDATE {0}.AUD_AUDIT_ENTRIES SET ACTION = 'tampered'", "UPDATE", "AUD_AUDIT_ENTRIES")]
    [InlineData("DELETE FROM {0}.AUD_AUDIT_ENTRIES", "DELETE", "AUD_AUDIT_ENTRIES")]
    [InlineData("UPDATE {0}.ORD_ORDER_STATUS_HISTORY SET REASON = 'tampered'", "UPDATE", "ORD_ORDER_STATUS_HISTORY")]
    [InlineData("DELETE FROM {0}.ORD_ORDER_STATUS_HISTORY", "DELETE", "ORD_ORDER_STATUS_HISTORY")]
    public async Task Audit_and_order_history_are_append_only_for_the_runtime_user(string sql, string privilege, string table)
    {
        database.SkipIfUnavailable();
        await using var app = new OracleConnection(database.RequiredSettings.AppConnectionString());
        await app.OpenAsync(Ct);
        await using var command = app.CreateCommand();
        command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, database.RequiredSettings.OwnerUser);

        var error = await Assert.ThrowsAsync<OracleException>(() => command.ExecuteNonQueryAsync(Ct));
        AssertObjectPrivilegeDenied(error, privilege, table);
    }

    /// <summary>
    /// The runtime user must be refused. Oracle 21c and earlier report ORA-01031 (insufficient privileges); Oracle 23ai/26ai report
    /// ORA-41900 ("missing privilege &lt;privilege&gt; on &lt;owner&gt;.&lt;object&gt;"). ORA-41900 is accepted only when its message names the
    /// attempted privilege and the protected table, so it proves the same denial; any other error number fails the test.
    /// </summary>
    private static void AssertObjectPrivilegeDenied(OracleException error, string privilege, string table)
    {
        if (error.Number == 1031)
        {
            return;
        }

        Assert.True(error.Number == 41900, $"Expected ORA-01031 or ORA-41900 but got ORA-{error.Number:D5}.");
        Assert.Contains(privilege, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(table, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE {0}.CAT_PRODUCTS SET STOCK_QUANTITY = -1 WHERE ID = :id")]
    [InlineData("UPDATE {0}.CAT_PRODUCTS SET PRICE_AMOUNT = 0 WHERE ID = :id")]
    public async Task Database_constraints_reject_invalid_product_values_from_any_access_path(string sql)
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var (_, productId, _) = await SeedAsync(services, stock: 1, DateTimeOffset.UtcNow);
        await using var app = new OracleConnection(database.RequiredSettings.AppConnectionString());
        await app.OpenAsync(Ct);
        await using var command = app.CreateCommand();
        command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, database.RequiredSettings.OwnerUser);
        command.Parameters.Add(new OracleParameter("id", OracleDbType.Raw, productId.ToByteArray(), System.Data.ParameterDirection.Input));

        var error = await Assert.ThrowsAsync<OracleException>(() => command.ExecuteNonQueryAsync(Ct));
        Assert.Equal(2290, error.Number); // ORA-02290: check constraint violated
    }

    [Fact]
    public async Task Order_financial_invariants_are_enforced_by_check_constraints()
    {
        database.SkipIfUnavailable();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var services = AppServices(clock);
        var orderNumber = await PlaceOrderAsync(services, clock, stock: 5, quantity: 1);
        await using var app = new OracleConnection(database.RequiredSettings.AppConnectionString());
        await app.OpenAsync(Ct);

        foreach (var sql in new[]
                 {
                     "UPDATE {0}.ORD_ORDERS SET TOTAL_AMOUNT = TOTAL_AMOUNT + 1 WHERE ORDER_NUMBER = :n",
                     "UPDATE {0}.ORD_ORDERS SET COMMISSION_AMOUNT = COMMISSION_AMOUNT + 1 WHERE ORDER_NUMBER = :n",
                 })
        {
            await using var command = app.CreateCommand();
            command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, database.RequiredSettings.OwnerUser);
            command.Parameters.Add(new OracleParameter("n", orderNumber));
            var error = await Assert.ThrowsAsync<OracleException>(() => command.ExecuteNonQueryAsync(Ct));
            Assert.Equal(2290, error.Number);
        }
    }

    [Fact]
    public async Task Unpaid_orders_expire_after_the_timeout_and_release_stock()
    {
        database.SkipIfUnavailable();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var services = AppServices(clock);
        var orderNumber = await PlaceOrderAsync(services, clock, stock: 4, quantity: 3);

        await using (var scope = services.CreateAsyncScope())
        {
            // Not yet due.
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ExpireUnpaidOrdersHandler>().HandleAsync(100, Ct));
        }

        clock.Advance(new MarketplaceOptions().PaymentTimeout + TimeSpan.FromMinutes(1));
        await using (var scope = services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ExpireUnpaidOrdersHandler>().HandleAsync(100, Ct) >= 1);
        }

        await using var verify = services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).SingleAsync(o => o.OrderNumber == orderNumber, Ct);
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(OrderActor.System, order.History.OrderBy(h => h.OccurredAt).Last().Actor);
        var stock = await db.Products.AsNoTracking().Where(p => p.Id == order.Items[0].ProductId).Select(p => p.StockQuantity).SingleAsync(Ct);
        Assert.Equal(4, stock);
    }

    /// <summary>Places a real order through the checkout handler (cart → checkout) as the runtime user.</summary>
    private static async Task<string> PlaceOrderAsync(ServiceProvider services, FakeTimeProvider clock, int stock, int quantity)
    {
        var (_, productId, customerId) = await SeedAsync(services, stock, clock.GetUtcNow());
        await using var scope = services.CreateAsyncScope();
        var setItem = scope.ServiceProvider.GetRequiredService<Application.Carts.SetCartItemHandler>();
        Assert.True((await setItem.HandleAsync(customerId, productId, quantity, Ct)).IsSuccess);

        var today = scope.ServiceProvider.GetRequiredService<MarketCalendar>().Today;
        var request = new CheckoutRequest("Ayşe", "+905550000000", "Moda Cad. 5", "Kadıköy", "İstanbul", "34710", today.AddDays(1), null);
        var result = await scope.ServiceProvider.GetRequiredService<PlaceCheckoutHandler>().HandleAsync(customerId, Guid.NewGuid().ToString(), request, Ct);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.Orders[0].OrderNumber;
    }
}
