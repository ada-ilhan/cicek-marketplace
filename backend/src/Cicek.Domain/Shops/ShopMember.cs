namespace Cicek.Domain.Shops;

public enum ShopMemberRole
{
    Owner,
    Staff,
}

/// <summary>Store-scoped actions. Checked against the caller's membership of the specific shop.</summary>
public enum ShopPermission
{
    ViewShop,
    ManageProfile,
    ManageCatalog,
    ManageOrders,
}

/// <summary>Links a user to a shop with a role. A user may belong to several shops.</summary>
public sealed class ShopMember
{
    internal ShopMember(Guid shopId, Guid userId, ShopMemberRole role, DateTimeOffset addedAt)
    {
        ShopId = shopId;
        UserId = userId;
        Role = role;
        AddedAt = addedAt;
    }

    private ShopMember()
    {
    }

    public Guid ShopId { get; private set; }

    public Guid UserId { get; private set; }

    public ShopMemberRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }
}

public static class ShopRolePermissions
{
    /// <summary>Owners can do everything; staff run day-to-day catalog and order work but cannot edit the shop profile.</summary>
    public static bool Allows(ShopMemberRole role, ShopPermission permission) => role switch
    {
        ShopMemberRole.Owner => true,
        ShopMemberRole.Staff => permission is ShopPermission.ViewShop or ShopPermission.ManageCatalog or ShopPermission.ManageOrders,
        _ => false,
    };
}
