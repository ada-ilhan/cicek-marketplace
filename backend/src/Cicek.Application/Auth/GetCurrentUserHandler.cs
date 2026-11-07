using Cicek.Application.Abstractions.Identity;
using Cicek.SharedKernel.Results;

namespace Cicek.Application.Auth;

/// <summary>Returns the caller's account, with roles read from the store rather than the token.</summary>
public sealed class GetCurrentUserHandler(IUserAccounts accounts)
{
    public async Task<Result<CurrentUserDto>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(userId, cancellationToken);
        return account is null ? AuthErrors.UserNotFound : AuthSessionIssuer.ToCurrentUser(account);
    }
}
