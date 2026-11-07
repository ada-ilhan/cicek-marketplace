using FluentValidation;

namespace Cicek.Application.Auth;

/// <summary>Shared limits; aligned with the identity column sizes.</summary>
public static class AuthLimits
{
    public const int EmailMaxLength = 256;

    /// <summary>NIST SP 800-63B style: length over composition rules.</summary>
    public const int PasswordMinLength = 10;

    public const int PasswordMaxLength = 128;

    public const int RefreshTokenMaxLength = 128;
}

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(AuthLimits.EmailMaxLength).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(AuthLimits.PasswordMinLength).MaximumLength(AuthLimits.PasswordMaxLength);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(AuthLimits.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(AuthLimits.PasswordMaxLength);
    }
}

public sealed class RefreshTokensCommandValidator : AbstractValidator<RefreshTokensCommand>
{
    public RefreshTokensCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(AuthLimits.RefreshTokenMaxLength);
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(AuthLimits.RefreshTokenMaxLength);
}
