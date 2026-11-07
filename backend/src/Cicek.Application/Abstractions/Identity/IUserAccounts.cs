using Cicek.SharedKernel.Results;

namespace Cicek.Application.Abstractions.Identity;

/// <summary>Read model of a user account as needed by authentication use cases.</summary>
public sealed record UserAccount(Guid Id, string Email, IReadOnlyList<string> Roles, bool IsLockedOut);

/// <summary>
/// Port over the identity store (implemented with ASP.NET Core Identity in Infrastructure).
/// Password hashing, lockout and normalization are the implementation's responsibility.
/// </summary>
public interface IUserAccounts
{
    Task<Result<UserAccount>> CreateCustomerAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies credentials and records failed attempts for lockout. Must not reveal whether the
    /// account exists: unknown email, wrong password and locked accounts produce the same error.
    /// </summary>
    Task<Result<UserAccount>> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}
