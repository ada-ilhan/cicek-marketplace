using System.Security.Claims;
using System.Text;
using Cicek.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cicek.Api.Auth;

/// <summary>Issues HS256 JWT access tokens. Roles are embedded; permissions are derived server-side.</summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(UserAccount account)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now + jwt.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(account.Roles.Select(role => new Claim(AuthConstants.RoleClaim, role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }

    internal static SymmetricSecurityKey CreateKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}

internal static class AuthConstants
{
    public const string RoleClaim = "role";
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;
}
