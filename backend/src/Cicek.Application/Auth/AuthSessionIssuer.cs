using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Authorization;
using Cicek.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Auth;

/// <summary>Creates the access token and refresh-session pair returned to clients.</summary>
public sealed class AuthSessionIssuer(
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshSessionStore sessions,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options)
{
    /// <summary>Starts a new rotation family (login / registration) and persists it.</summary>
    public async Task<AuthSessionDto> StartAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var (token, hash) = RefreshTokenSecrets.Generate();
        var session = RefreshSession.StartFamily(
            account.Id,
            hash,
            timeProvider.GetUtcNow(),
            options.Value.TokenLifetime,
            options.Value.FamilyLifetime);

        sessions.Add(session);
        await sessions.SaveChangesAsync(cancellationToken);

        return Build(account, token, session);
    }

    /// <summary>Builds the response for an already persisted refresh session.</summary>
    public AuthSessionDto Build(UserAccount account, string refreshToken, RefreshSession session)
    {
        var access = accessTokenIssuer.Issue(account);
        return new AuthSessionDto(access.Value, access.ExpiresAt, refreshToken, session.ExpiresAt, ToCurrentUser(account));
    }

    public static CurrentUserDto ToCurrentUser(UserAccount account) =>
        new(account.Id, account.Email, account.Roles, [.. RolePermissions.PermissionsFor(account.Roles).Order(StringComparer.Ordinal)]);
}
