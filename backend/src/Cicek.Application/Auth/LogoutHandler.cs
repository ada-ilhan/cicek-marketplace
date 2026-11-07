using Cicek.Application.Abstractions.Identity;
using Cicek.Domain.Identity;

namespace Cicek.Application.Auth;

/// <summary>
/// Revokes every unrevoked session in the presented token's family. Always succeeds so that the
/// response does not reveal whether a token was valid.
/// </summary>
public sealed class LogoutHandler(IRefreshSessionStore sessions, TimeProvider timeProvider)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByTokenHashAsync(RefreshTokenSecrets.Hash(command.RefreshToken), cancellationToken);
        if (session is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var member in await sessions.ListUnrevokedInFamilyAsync(session.FamilyId, cancellationToken))
        {
            member.Revoke(RefreshSessionRevocationReason.LoggedOut, now);
        }

        await sessions.SaveChangesAsync(cancellationToken);
    }
}
