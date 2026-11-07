namespace Cicek.Application.Abstractions.Identity;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Issues short-lived bearer access tokens. Implemented by the API host (JWT).</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account);
}
