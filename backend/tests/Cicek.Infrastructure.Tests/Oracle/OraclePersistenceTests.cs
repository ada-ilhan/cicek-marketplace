using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Identity;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Persistence;
using Cicek.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cicek.Infrastructure.Tests.Oracle;

/// <summary>Constraint, transaction, concurrency and type behaviour as the least-privilege runtime user.</summary>
[Collection(OracleCollection.Name)]
public sealed class OraclePersistenceTests(OracleTestDatabase database)
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 13, 9, 30, 15, 123, TimeSpan.Zero).AddTicks(4567);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServiceProvider AppServices() => OracleTestDatabase.CreateServices(database.RequiredSettings.AppConfiguration());

    private static string RandomHash() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private static async Task<Guid> CreateUserAsync(AppDbContext db)
    {
        var user = new AppUser
        {
            UserName = $"{Guid.NewGuid():N}@example.com",
            NormalizedUserName = $"{Guid.NewGuid():N}@EXAMPLE.COM",
            Email = "persist@example.com",
            CreatedAt = Now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    [Fact]
    public async Task Unique_constraint_violation_is_classified()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateUserAsync(db);
        var hash = RandomHash();

        db.RefreshSessions.Add(RefreshSession.StartFamily(userId, hash, Now, TimeSpan.FromDays(1), TimeSpan.FromDays(2)));
        await db.SaveChangesAsync(Ct);
        db.RefreshSessions.Add(RefreshSession.StartFamily(userId, hash, Now, TimeSpan.FromDays(1), TimeSpan.FromDays(2)));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var failure = scope.ServiceProvider.GetRequiredService<IDatabaseErrorClassifier>().Classify(exception);
        Assert.Equal(DatabaseFailureKind.UniqueViolation, failure?.Kind);
        Assert.Equal(1, failure?.ProviderCode);
    }

    [Fact]
    public async Task Foreign_key_violation_is_classified()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.RefreshSessions.Add(RefreshSession.StartFamily(Guid.NewGuid(), RandomHash(), Now, TimeSpan.FromDays(1), TimeSpan.FromDays(2)));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var failure = scope.ServiceProvider.GetRequiredService<IDatabaseErrorClassifier>().Classify(exception);
        Assert.Equal(DatabaseFailureKind.ReferenceViolation, failure?.Kind);
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_rows()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        Guid userId;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            userId = await CreateUserAsync(db);
            Assert.True(await db.Users.AnyAsync(u => u.Id == userId, Ct));
            await transaction.RollbackAsync(Ct);
        }

        await using var verify = services.CreateAsyncScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Id == userId, Ct));
    }

    [Fact]
    public async Task Concurrent_rotation_of_the_same_token_lets_only_one_request_win()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        var hash = RandomHash();
        await using (var setup = services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await CreateUserAsync(db);
            db.RefreshSessions.Add(RefreshSession.StartFamily(userId, hash, Now, TimeSpan.FromDays(1), TimeSpan.FromDays(2)));
            await db.SaveChangesAsync(Ct);
        }

        await using var scopeA = services.CreateAsyncScope();
        await using var scopeB = services.CreateAsyncScope();
        var storeA = scopeA.ServiceProvider.GetRequiredService<IRefreshSessionStore>();
        var storeB = scopeB.ServiceProvider.GetRequiredService<IRefreshSessionStore>();

        // Both requests read the same version before either writes.
        var sessionA = await storeA.FindByTokenHashAsync(hash, Ct);
        var sessionB = await storeB.FindByTokenHashAsync(hash, Ct);
        storeA.Add(sessionA!.Rotate(RandomHash(), Now.AddMinutes(1), TimeSpan.FromDays(1)));
        storeB.Add(sessionB!.Rotate(RandomHash(), Now.AddMinutes(1), TimeSpan.FromDays(1)));

        await storeA.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => storeB.SaveChangesAsync(Ct));

        await using var verify = services.CreateAsyncScope();
        var family = await verify.ServiceProvider.GetRequiredService<AppDbContext>().RefreshSessions
            .Where(s => s.FamilyId == sessionA.FamilyId).ToListAsync(Ct);
        Assert.Equal(2, family.Count); // original + exactly one successor
    }

    [Fact]
    public async Task Timestamps_round_trip_as_UTC_with_full_precision()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        Guid sessionId;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await CreateUserAsync(db);
            var session = RefreshSession.StartFamily(userId, RandomHash(), Now, TimeSpan.FromDays(1), TimeSpan.FromDays(2));
            db.RefreshSessions.Add(session);
            await db.SaveChangesAsync(Ct);
            sessionId = session.Id;
        }

        await using var verify = services.CreateAsyncScope();
        var loaded = await verify.ServiceProvider.GetRequiredService<AppDbContext>().RefreshSessions.AsNoTracking()
            .SingleAsync(s => s.Id == sessionId, Ct);
        Assert.Equal(Now, loaded.CreatedAt);
        Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
    }

    [Fact]
    public async Task Turkish_characters_are_stored_without_loss()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string email = "çiçekçi.ğüşıöİ@örnek.com.tr";
        var user = new AppUser { UserName = $"{Guid.NewGuid():N}", Email = email, CreatedAt = Now };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        db.ChangeTracker.Clear();
        Assert.Equal(email, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct)).Email);
    }

    [Fact]
    public async Task Identity_registration_and_login_work_as_the_runtime_user()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var email = $"login.{Guid.NewGuid():N}@example.com";

        var created = await accounts.CreateCustomerAsync(email, "correct-horse-battery", Ct);
        var duplicate = await accounts.CreateCustomerAsync(email.ToUpperInvariant(), "correct-horse-battery", Ct);
        var verified = await accounts.VerifyCredentialsAsync(email, "correct-horse-battery", Ct);
        var wrong = await accounts.VerifyCredentialsAsync(email, "wrong-password-1", Ct);

        Assert.True(created.IsSuccess);
        Assert.Equal("auth.email_taken", duplicate.Error?.Code);
        Assert.True(verified.IsSuccess);
        Assert.Equal(["Customer"], verified.Value.Roles);
        Assert.Equal("auth.invalid_credentials", wrong.Error?.Code);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        database.SkipIfUnavailable();
        await using var services = AppServices();
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var email = $"lockout.{Guid.NewGuid():N}@example.com";
        await accounts.CreateCustomerAsync(email, "correct-horse-battery", Ct);

        for (var i = 0; i < 5; i++)
        {
            await accounts.VerifyCredentialsAsync(email, "wrong-password-1", Ct);
        }

        var correctAfterLockout = await accounts.VerifyCredentialsAsync(email, "correct-horse-battery", Ct);
        Assert.Equal("auth.invalid_credentials", correctAfterLockout.Error?.Code);
    }
}
