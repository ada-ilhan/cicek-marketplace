using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Auth;
using Cicek.Application.Authorization;
using Cicek.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Cicek.Application.Tests.Auth;

public sealed class RefreshTokensHandlerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemorySessionStore _store = new();
    private readonly FakeUserAccounts _accounts = new();
    private readonly IOptions<RefreshTokenOptions> _options = Options.Create(new RefreshTokenOptions());

    private RefreshTokensHandler CreateHandler() =>
        new(_store, _accounts, new AuthSessionIssuer(new FakeTokenIssuer(), _store, _time, _options), _time, _options, NullLogger<RefreshTokensHandler>.Instance);

    private async Task<string> LoginAsync(UserAccount account)
    {
        var issuer = new AuthSessionIssuer(new FakeTokenIssuer(), _store, _time, _options);
        return (await issuer.StartAsync(account, CancellationToken.None)).RefreshToken;
    }

    [Fact]
    public async Task Valid_token_is_rotated_and_old_token_stops_working()
    {
        var account = _accounts.Add(Roles.Customer);
        var token = await LoginAsync(account);

        var first = await CreateHandler().HandleAsync(new RefreshTokensCommand(token), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.NotEqual(token, first.Value.RefreshToken);
        Assert.Equal(account.Id, first.Value.User.Id);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var account = _accounts.Add(Roles.Customer);
        var original = await LoginAsync(account);
        var rotated = await CreateHandler().HandleAsync(new RefreshTokensCommand(original), CancellationToken.None);

        var reuse = await CreateHandler().HandleAsync(new RefreshTokensCommand(original), CancellationToken.None);
        var successorAfterReuse = await CreateHandler().HandleAsync(new RefreshTokensCommand(rotated.Value.RefreshToken), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, reuse.Error);
        Assert.Equal(AuthErrors.InvalidRefreshToken, successorAfterReuse.Error);
        Assert.All(_store.Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.Contains(_store.Sessions, s => s.RevocationReason == RefreshSessionRevocationReason.ReuseDetected);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = await LoginAsync(_accounts.Add(Roles.Customer));
        _time.Advance(_options.Value.TokenLifetime);

        var result = await CreateHandler().HandleAsync(new RefreshTokensCommand(token), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        var result = await CreateHandler().HandleAsync(new RefreshTokensCommand("unknown"), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Locked_out_user_cannot_refresh_and_family_is_revoked()
    {
        var account = _accounts.Add(Roles.Customer);
        var token = await LoginAsync(account);
        _accounts.Lock(account.Id);

        var result = await CreateHandler().HandleAsync(new RefreshTokensCommand(token), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.All(_store.Sessions, s => Assert.Equal(RefreshSessionRevocationReason.AdministrativeAction, s.RevocationReason));
    }

    [Fact]
    public async Task Losing_a_concurrent_rotation_is_rejected_without_revoking_the_family()
    {
        var token = await LoginAsync(_accounts.Add(Roles.Customer));
        _store.FailNextSaveWithConcurrencyConflict = true;

        var result = await CreateHandler().HandleAsync(new RefreshTokensCommand(token), CancellationToken.None);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.DoesNotContain(_store.Sessions, s => s.RevocationReason == RefreshSessionRevocationReason.ReuseDetected);
    }

    [Fact]
    public async Task Logout_revokes_every_session_in_the_family()
    {
        var token = await LoginAsync(_accounts.Add(Roles.Customer));
        var rotated = await CreateHandler().HandleAsync(new RefreshTokensCommand(token), CancellationToken.None);

        await new LogoutHandler(_store, _time).HandleAsync(new LogoutCommand(rotated.Value.RefreshToken), CancellationToken.None);

        Assert.All(_store.Sessions, s => Assert.NotNull(s.RevokedAt));
    }

    private sealed class FakeTokenIssuer : IAccessTokenIssuer
    {
        public AccessToken Issue(UserAccount account) => new($"access-{account.Id}", DateTimeOffset.MaxValue);
    }

    private sealed class FakeUserAccounts : IUserAccounts
    {
        private readonly Dictionary<Guid, UserAccount> _accounts = [];

        public UserAccount Add(params string[] roles)
        {
            var account = new UserAccount(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", roles, false);
            _accounts[account.Id] = account;
            return account;
        }

        public void Lock(Guid id) => _accounts[id] = _accounts[id] with { IsLockedOut = true };

        public Task<Cicek.SharedKernel.Results.Result<UserAccount>> CreateCustomerAsync(string email, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Cicek.SharedKernel.Results.Result<UserAccount>> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.GetValueOrDefault(userId));
    }

    private sealed class InMemorySessionStore : IRefreshSessionStore
    {
        private readonly List<RefreshSession> _pending = [];

        public List<RefreshSession> Sessions { get; } = [];

        public bool FailNextSaveWithConcurrencyConflict { get; set; }

        public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.SingleOrDefault(s => s.TokenHash == tokenHash));

        public Task<IReadOnlyList<RefreshSession>> ListUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RefreshSession>>(Sessions.Where(s => s.FamilyId == familyId && s.RevokedAt is null).ToList());

        public void Add(RefreshSession session) => _pending.Add(session);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailNextSaveWithConcurrencyConflict)
            {
                FailNextSaveWithConcurrencyConflict = false;
                _pending.Clear();
                throw new ConcurrencyConflictException("simulated");
            }

            Sessions.AddRange(_pending);
            _pending.Clear();
            return Task.CompletedTask;
        }
    }
}
