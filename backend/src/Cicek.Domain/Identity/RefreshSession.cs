using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Identity;

/// <summary>
/// One refresh token in a rotation family. Only the SHA-256 hash of the token is stored.
/// Rules:
/// <list type="bullet">
/// <item>A token can be used exactly once; using it rotates it into a new session of the same family.</item>
/// <item>Presenting an already-rotated token is treated as theft (reuse) and revokes the whole family.</item>
/// <item>A family never outlives its absolute expiry, regardless of how often it is rotated.</item>
/// </list>
/// </summary>
public sealed class RefreshSession : Entity
{
    public const int TokenHashLength = 64; // hex-encoded SHA-256

    private RefreshSession(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset familyExpiresAt)
        : base(id)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        FamilyExpiresAt = familyExpiresAt;
    }

    private RefreshSession()
    {
    }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset FamilyExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshSessionRevocationReason? RevocationReason { get; private set; }

    public Guid? ReplacedBySessionId { get; private set; }

    public static RefreshSession StartFamily(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan tokenLifetime,
        TimeSpan familyLifetime)
    {
        EnsureValidHash(tokenHash);
        if (userId == Guid.Empty)
        {
            throw new DomainException("identity.refresh.user_required", "A refresh session requires a user.");
        }

        if (tokenLifetime <= TimeSpan.Zero || familyLifetime < tokenLifetime)
        {
            throw new DomainException("identity.refresh.invalid_lifetime", "Token lifetime must be positive and not exceed the family lifetime.");
        }

        var familyExpiresAt = now + familyLifetime;
        return new RefreshSession(Guid.CreateVersion7(), userId, Guid.CreateVersion7(), tokenHash, now, now + tokenLifetime, familyExpiresAt);
    }

    public RefreshSessionState GetState(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return RevocationReason == RefreshSessionRevocationReason.Rotated
                ? RefreshSessionState.AlreadyRotated
                : RefreshSessionState.Revoked;
        }

        return now >= ExpiresAt ? RefreshSessionState.Expired : RefreshSessionState.Active;
    }

    /// <summary>
    /// Consumes this token and returns its successor in the same family.
    /// Callers must persist both sessions in one transaction; the persistence layer uses an
    /// optimistic concurrency token so that two concurrent rotations of the same token cannot both succeed.
    /// </summary>
    public RefreshSession Rotate(string newTokenHash, DateTimeOffset now, TimeSpan tokenLifetime)
    {
        EnsureValidHash(newTokenHash);
        if (GetState(now) != RefreshSessionState.Active)
        {
            throw new DomainException("identity.refresh.not_active", "Only an active refresh session can be rotated.");
        }

        var expiresAt = now + tokenLifetime;
        if (expiresAt > FamilyExpiresAt)
        {
            expiresAt = FamilyExpiresAt;
        }

        var successor = new RefreshSession(Guid.CreateVersion7(), UserId, FamilyId, newTokenHash, now, expiresAt, FamilyExpiresAt);
        RevokedAt = now;
        RevocationReason = RefreshSessionRevocationReason.Rotated;
        ReplacedBySessionId = successor.Id;
        return successor;
    }

    /// <summary>Revokes the session. Idempotent: an already revoked session keeps its original reason.</summary>
    public void Revoke(RefreshSessionRevocationReason reason, DateTimeOffset now)
    {
        if (reason == RefreshSessionRevocationReason.Rotated)
        {
            throw new DomainException("identity.refresh.invalid_reason", "Use Rotate() to rotate a session.");
        }

        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevocationReason = reason;
    }

    private static void EnsureValidHash(string tokenHash)
    {
        if (string.IsNullOrEmpty(tokenHash) || tokenHash.Length != TokenHashLength)
        {
            throw new DomainException("identity.refresh.invalid_hash", "Refresh token hash must be a hex-encoded SHA-256 value.");
        }
    }
}

public enum RefreshSessionState
{
    Active,
    Expired,
    AlreadyRotated,
    Revoked,
}

public enum RefreshSessionRevocationReason
{
    Rotated,
    LoggedOut,
    ReuseDetected,
    AdministrativeAction,
}
