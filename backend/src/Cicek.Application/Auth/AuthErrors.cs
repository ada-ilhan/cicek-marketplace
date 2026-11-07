using Cicek.SharedKernel.Results;

namespace Cicek.Application.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "auth.invalid_credentials",
        "The email or password is incorrect, or the account is temporarily locked.");

    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "auth.invalid_refresh_token",
        "The session has expired. Please sign in again.");

    public static readonly Error EmailAlreadyRegistered = Error.Conflict(
        "auth.email_taken",
        "An account with this email address already exists.");

    public static readonly Error UserNotFound = Error.Unauthorized(
        "auth.user_not_found",
        "The account no longer exists.");
}
