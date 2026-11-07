using Cicek.Application.Abstractions.Identity;
using Cicek.SharedKernel.Results;

namespace Cicek.Application.Auth;

public sealed class LoginHandler(IUserAccounts accounts, AuthSessionIssuer sessionIssuer)
{
    public async Task<Result<AuthSessionDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var verified = await accounts.VerifyCredentialsAsync(command.Email, command.Password, cancellationToken);
        if (!verified.IsSuccess)
        {
            return verified.Error;
        }

        return await sessionIssuer.StartAsync(verified.Value, cancellationToken);
    }
}
