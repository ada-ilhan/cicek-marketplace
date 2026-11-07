using Cicek.Application.Auth;
using Cicek.Application.Authorization;

namespace Cicek.Application.Tests.Auth;

public sealed class AuthRulesTests
{
    [Theory]
    [InlineData(Roles.Administrator, true)]
    [InlineData(Roles.Support, true)]
    [InlineData(Roles.Customer, false)]
    [InlineData(Roles.FloristOwner, false)]
    [InlineData(Roles.FloristStaff, false)]
    [InlineData(Roles.Finance, false)]
    [InlineData("UnknownRole", false)]
    public void System_status_permission_by_role(string role, bool expected)
    {
        Assert.Equal(expected, RolePermissions.RoleHasPermission(role, Permissions.SystemStatusRead));
    }

    [Fact]
    public void Administrator_has_every_permission()
    {
        Assert.All(Permissions.All, p => Assert.True(RolePermissions.RoleHasPermission(Roles.Administrator, p)));
    }

    [Fact]
    public void Every_role_is_mapped()
    {
        Assert.All(Roles.All, role => Assert.NotNull(RolePermissions.PermissionsFor([role])));
    }

    [Fact]
    public void Refresh_tokens_are_random_and_only_the_hash_is_derived()
    {
        var (token1, hash1) = RefreshTokenSecrets.Generate();
        var (token2, _) = RefreshTokenSecrets.Generate();

        Assert.NotEqual(token1, token2);
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, RefreshTokenSecrets.Hash(token1));
        Assert.DoesNotContain(token1, hash1, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("customer@example.com", "long-enough-pass", true)]
    [InlineData("not-an-email", "long-enough-pass", false)]
    [InlineData("customer@example.com", "short", false)]
    [InlineData("", "long-enough-pass", false)]
    public void Registration_validation(string email, string password, bool valid)
    {
        var result = new RegisterCustomerCommandValidator().Validate(new RegisterCustomerCommand(email, password));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Password_upper_bound_is_enforced()
    {
        var result = new RegisterCustomerCommandValidator().Validate(new RegisterCustomerCommand("a@example.com", new string('x', 129)));

        Assert.False(result.IsValid);
    }
}
