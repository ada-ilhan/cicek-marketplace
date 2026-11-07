using Cicek.Application.Abstractions.Identity;
using Cicek.SharedKernel.Results;

namespace Cicek.Application.Auth;

public sealed class RegisterCustomerHandler(IUserAccounts accounts, AuthSessionIssuer sessionIssuer)
{
    public async Task<Result<AuthSessionDto>> HandleAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var created = await accounts.CreateCustomerAsync(command.Email, command.Password, cancellationToken);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        return await sessionIssuer.StartAsync(created.Value, cancellationToken);
    }
}
