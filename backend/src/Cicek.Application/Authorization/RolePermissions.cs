using System.Collections.Frozen;

namespace Cicek.Application.Authorization;

/// <summary>Static role → permission mapping. Pure code so it is reviewable and unit-testable.</summary>
public static class RolePermissions
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> Map =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [Roles.Customer] = FrozenSet<string>.Empty,
            [Roles.FloristOwner] = FrozenSet<string>.Empty,
            [Roles.FloristStaff] = FrozenSet<string>.Empty,
            [Roles.Finance] = new[] { Permissions.OrdersRead }.ToFrozenSet(StringComparer.Ordinal),
            [Roles.Support] = new[] { Permissions.SystemStatusRead, Permissions.OrdersRead, Permissions.AdminOverviewRead, Permissions.UsersRead }.ToFrozenSet(StringComparer.Ordinal),
            [Roles.Administrator] = Permissions.All.ToFrozenSet(StringComparer.Ordinal),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool RoleHasPermission(string role, string permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static bool AnyRoleHasPermission(IEnumerable<string> roles, string permission) =>
        roles.Any(role => RoleHasPermission(role, permission));

    public static IReadOnlySet<string> PermissionsFor(IEnumerable<string> roles) =>
        roles.Where(Map.ContainsKey).SelectMany(role => Map[role]).ToHashSet(StringComparer.Ordinal);
}
