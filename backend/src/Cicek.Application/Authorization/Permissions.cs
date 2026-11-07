namespace Cicek.Application.Authorization;

/// <summary>
/// Code-defined platform permissions. Endpoints require permissions, never role names, so that role
/// composition can change without touching endpoint code. Shop-scoped rights (managing one's own shop) are
/// not platform permissions: they come from shop membership (<c>Domain.Shops.ShopPermission</c>).
/// </summary>
public static class Permissions
{
    /// <summary>View operational system status (database, migrations, build).</summary>
    public const string SystemStatusRead = "system.status.read";

    /// <summary>Approve, suspend and reinstate shops; set commission overrides.</summary>
    public const string ShopsManage = "shops.manage";

    /// <summary>Create and edit catalogue categories.</summary>
    public const string CategoriesManage = "categories.manage";

    /// <summary>View all orders across shops.</summary>
    public const string OrdersRead = "orders.read";

    /// <summary>Record manual payments and cancel any order.</summary>
    public const string OrdersManage = "orders.manage";

    /// <summary>Administrator dashboard figures.</summary>
    public const string AdminOverviewRead = "admin.overview.read";

    /// <summary>List users (email, roles, lock state) for administration.</summary>
    public const string UsersRead = "users.read";

    public static readonly IReadOnlyList<string> All =
        [SystemStatusRead, ShopsManage, CategoriesManage, OrdersRead, OrdersManage, AdminOverviewRead, UsersRead];
}
