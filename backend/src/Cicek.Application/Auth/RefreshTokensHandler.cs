using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Identity;
using Cicek.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicek.Application.Auth;

/// <summary>
/// Rotates a refresh token. Reuse of an already-rotated token revokes the whole family because it
/// indicates the token was copied. A lost concurrent rotation (two requests racing with the same
/// token) is rejected without revoking the family, so the winning request's session stays valid.
/// </summary>
public sealed class RefreshTokensHandler(
    IRefreshSessionStore sessions,
    IUserAccounts accounts,
    AuthSessionIssuer sessionIssuer,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options,
    ILogger<RefreshTokensHandler> logger)
{
    public async Task<Result<AuthSessionDto>> HandleAsync(RefreshTokensCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.FindByTokenHashAsync(RefreshTokenSecrets.Hash(command.RefreshToken), cancellationToken);
        if (session is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        switch (session.GetState(now))
        {
            case RefreshSessionState.AlreadyRotated:
                logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}, family {FamilyId}; revoking family.",
                    session.UserId,
                    session.FamilyId);
                await RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.ReuseDetected, now, cancellationToken);
                return AuthErrors.InvalidRefreshToken;
            case RefreshSessionState.Expired:
            case RefreshSessionState.Revoked:
                return AuthErrors.InvalidRefreshToken;
            case RefreshSessionState.Active:
                break;
            default:
                throw new InvalidOperationException("Unhandled refresh session state.");
        }

        var account = await accounts.FindByIdAsync(session.UserId, cancellationToken);
        if (account is null || account.IsLockedOut)
        {
            await RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.AdministrativeAction, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var (token, hash) = RefreshTokenSecrets.Generate();
        var successor = session.Rotate(hash, now, options.Value.TokenLifetime);
        sessions.Add(successor);

        try
        {
            await sessions.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogInformation("Concurrent refresh of the same token for user {UserId}; rejecting the losing request.", session.UserId);
            return AuthErrors.InvalidRefreshToken;
        }

        return sessionIssuer.Build(account, token, successor);
    }

    private async Task RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var member in await sessions.ListUnrevokedInFamilyAsync(familyId, cancellationToken))
        {
            member.Revoke(reason, now);
        }

        try
        {
            await sessions.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request changed a family member at the same moment. Re-read and retry once;
            // revocation is idempotent, so repeating it is safe.
            foreach (var member in await sessions.ListUnrevokedInFamilyAsync(familyId, cancellationToken))
            {
                member.Revoke(reason, now);
            }

            await sessions.SaveChangesAsync(cancellationToken);
        }
    }
}
