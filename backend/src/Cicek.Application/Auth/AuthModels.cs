namespace Cicek.Application.Auth;

public sealed record RegisterCustomerCommand(string Email, string Password);

public sealed record LoginCommand(string Email, string Password);

public sealed record RefreshTokensCommand(string RefreshToken);

public sealed record LogoutCommand(string RefreshToken);

/// <summary>The authenticated user as exposed to clients.</summary>
public sealed record CurrentUserDto(Guid Id, string Email, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>Tokens returned after login, registration or refresh. Consumed by the BFF, never stored in browser JS.</summary>
public sealed record AuthSessionDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserDto User);
