using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Auth;
using Cicek.Application.Authorization;
using Cicek.Infrastructure.Persistence;
using Cicek.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity implementation of <see cref="IUserAccounts"/>.</summary>
internal sealed class IdentityUserAccounts(
    UserManager<AppUser> userManager,
    AppDbContext db,
    TimeProvider timeProvider) : IUserAccounts
{
    // Used to spend comparable time hashing when the email is unknown, so response timing does not
    // reveal whether an account exists.
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString()));

    public async Task<Result<UserAccount>> CreateCustomerAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedInput = email.Trim();
        var user = new AppUser
        {
            UserName = normalizedInput,
            Email = normalizedInput,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        // User row and role membership must be created atomically.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return MapCreateErrors(created.Errors);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, Roles.Customer);
        if (!roleAdded.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the {Roles.Customer} role: {string.Join(", ", roleAdded.Errors.Select(e => e.Code))}. " +
                "Run the DbMigrator to seed roles.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new UserAccount(user.Id, user.Email!, [Roles.Customer], IsLockedOut: false);
    }

    public async Task<Result<UserAccount>> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyPasswordHash.Value, password);
            return AuthErrors.InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return AuthErrors.InvalidCredentials;
        }

        if (await userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        return await ToAccountAsync(user);
    }

    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : await ToAccountAsync(user);
    }

    private async Task<UserAccount> ToAccountAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var lockedOut = user.LockoutEnabled && user.LockoutEnd is { } end && end > timeProvider.GetUtcNow();
        return new UserAccount(user.Id, user.Email!, [.. roles.Order(StringComparer.Ordinal)], lockedOut);
    }

    private static Error MapCreateErrors(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();
        if (list.Exists(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AuthErrors.EmailAlreadyRegistered;
        }

        return Error.Validation("auth.registration_rejected", string.Join(" ", list.Select(e => e.Description)));
    }
}
