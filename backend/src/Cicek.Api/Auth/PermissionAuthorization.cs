using System.Security.Claims;
using Cicek.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Cicek.Api.Auth;

/// <summary>Requires a code-defined permission (see <see cref="Permissions"/>).</summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Grants the requirement when any of the caller's roles maps to the permission.</summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(AuthConstants.RoleClaim).Select(c => c.Value);
        if (RolePermissions.AnyRoleHasPermission(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal static class AuthorizationExtensions
{
    /// <summary>Requires an authenticated caller holding <paramref name="permission"/>.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission)));

    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(AuthConstants.SubjectClaim), out var id) ? id : null;
}
