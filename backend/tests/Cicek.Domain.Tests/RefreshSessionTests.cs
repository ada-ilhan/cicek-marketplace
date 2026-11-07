using Cicek.Domain.Identity;
using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Tests;

public sealed class RefreshSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan FamilyLifetime = TimeSpan.FromDays(30);

    private static string Hash(char c) => new(c, RefreshSession.TokenHashLength);

    private static RefreshSession NewSession() =>
        RefreshSession.StartFamily(Guid.NewGuid(), Hash('a'), Now, TokenLifetime, FamilyLifetime);

    [Fact]
    public void New_session_is_active_until_it_expires()
    {
        var session = NewSession();

        Assert.Equal(RefreshSessionState.Active, session.GetState(Now));
        Assert.Equal(RefreshSessionState.Active, session.GetState(Now + TokenLifetime - TimeSpan.FromSeconds(1)));
        Assert.Equal(RefreshSessionState.Expired, session.GetState(Now + TokenLifetime));
    }

    [Fact]
    public void Rotation_consumes_the_token_and_keeps_the_family()
    {
        var session = NewSession();

        var successor = session.Rotate(Hash('b'), Now.AddHours(1), TokenLifetime);

        Assert.Equal(RefreshSessionState.AlreadyRotated, session.GetState(Now.AddHours(1)));
        Assert.Equal(successor.Id, session.ReplacedBySessionId);
        Assert.Equal(session.FamilyId, successor.FamilyId);
        Assert.Equal(session.UserId, successor.UserId);
        Assert.Equal(RefreshSessionState.Active, successor.GetState(Now.AddHours(1)));
    }

    [Fact]
    public void Rotated_token_cannot_be_rotated_again()
    {
        var session = NewSession();
        session.Rotate(Hash('b'), Now, TokenLifetime);

        var ex = Assert.Throws<DomainException>(() => session.Rotate(Hash('c'), Now, TokenLifetime));
        Assert.Equal("identity.refresh.not_active", ex.Code);
    }

    [Fact]
    public void Successor_never_outlives_the_family()
    {
        var session = NewSession();
        var second = session.Rotate(Hash('b'), Now + TimeSpan.FromDays(10), TokenLifetime);

        // Day 20 + 14 days would be day 34; the family ends on day 30.
        var third = second.Rotate(Hash('c'), Now + TimeSpan.FromDays(20), TokenLifetime);

        Assert.Equal(Now + TimeSpan.FromDays(24), second.ExpiresAt);
        Assert.Equal(Now + FamilyLifetime, third.ExpiresAt);
    }

    [Fact]
    public void Revocation_is_idempotent_and_keeps_the_first_reason()
    {
        var session = NewSession();

        session.Revoke(RefreshSessionRevocationReason.LoggedOut, Now);
        session.Revoke(RefreshSessionRevocationReason.ReuseDetected, Now.AddMinutes(1));

        Assert.Equal(RefreshSessionRevocationReason.LoggedOut, session.RevocationReason);
        Assert.Equal(Now, session.RevokedAt);
        Assert.Equal(RefreshSessionState.Revoked, session.GetState(Now));
    }

    [Fact]
    public void Revoke_cannot_be_used_to_mark_rotation()
    {
        Assert.Throws<DomainException>(() => NewSession().Revoke(RefreshSessionRevocationReason.Rotated, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Invalid_hash_is_rejected(string hash)
    {
        Assert.Throws<DomainException>(() => RefreshSession.StartFamily(Guid.NewGuid(), hash, Now, TokenLifetime, FamilyLifetime));
    }

    [Fact]
    public void Token_lifetime_may_not_exceed_family_lifetime()
    {
        Assert.Throws<DomainException>(() =>
            RefreshSession.StartFamily(Guid.NewGuid(), Hash('a'), Now, TimeSpan.FromDays(40), FamilyLifetime));
    }
}
