using Cicek.Domain.Shops;
using Cicek.SharedKernel.Domain;
using static Cicek.Domain.Tests.Marketplace.TestData;

namespace Cicek.Domain.Tests.Marketplace;

public sealed class ShopTests
{
    [Fact]
    public void Application_starts_pending_with_the_applicant_as_owner()
    {
        var owner = Guid.NewGuid();
        var shop = Shop.Apply(owner, "gul-bahcesi", Profile(), Now);

        Assert.Equal(ShopStatus.PendingApproval, shop.Status);
        Assert.False(shop.AcceptsOrders);
        Assert.True(shop.HasPermission(owner, ShopPermission.ManageProfile));
        Assert.False(shop.HasPermission(Guid.NewGuid(), ShopPermission.ViewShop));
        Assert.Equal("gul bahcesi", shop.NormalizedName);
    }

    [Fact]
    public void Approval_activates_and_owner_can_toggle_active_and_inactive()
    {
        var shop = ActiveShop(Guid.NewGuid());
        Assert.True(shop.AcceptsOrders);

        shop.Deactivate(Now);
        Assert.Equal(ShopStatus.Inactive, shop.Status);
        Assert.False(shop.AcceptsOrders);

        shop.Activate(Now);
        Assert.Equal(ShopStatus.Active, shop.Status);
    }

    [Fact]
    public void Owner_cannot_reactivate_a_suspended_shop()
    {
        var shop = ActiveShop(Guid.NewGuid());
        shop.Suspend("Policy violation", Now);

        Assert.Throws<DomainException>(() => shop.Activate(Now));
        Assert.Equal("Policy violation", shop.StatusReason);
    }

    [Fact]
    public void Reinstating_returns_to_pending_when_never_approved()
    {
        var pending = Shop.Apply(Guid.NewGuid(), "a-shop", Profile(), Now);
        pending.Suspend("Fraud check", Now);
        pending.Reinstate(Now);
        Assert.Equal(ShopStatus.PendingApproval, pending.Status);

        var approved = ActiveShop(Guid.NewGuid());
        approved.Suspend("Check", Now);
        approved.Reinstate(Now);
        Assert.Equal(ShopStatus.Active, approved.Status);
    }

    [Fact]
    public void Approve_requires_pending_status()
    {
        var shop = ActiveShop(Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => shop.Approve(Now));
        Assert.Equal("shop.not_pending", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not A Slug")]
    [InlineData("-leading")]
    public void Invalid_slug_is_rejected(string slug)
    {
        Assert.Throws<DomainException>(() => Shop.Apply(Guid.NewGuid(), slug, Profile(), Now));
    }

    [Fact]
    public void Required_profile_fields_are_enforced()
    {
        Assert.Throws<DomainException>(() => Shop.Apply(Guid.NewGuid(), "x", Profile(name: "  "), Now));
        Assert.Throws<DomainException>(() => Shop.Apply(Guid.NewGuid(), "x", Profile(name: new string('a', 101)), Now));
    }

    [Theory]
    [InlineData(ShopMemberRole.Owner, ShopPermission.ManageProfile, true)]
    [InlineData(ShopMemberRole.Staff, ShopPermission.ManageProfile, false)]
    [InlineData(ShopMemberRole.Staff, ShopPermission.ManageCatalog, true)]
    [InlineData(ShopMemberRole.Staff, ShopPermission.ManageOrders, true)]
    public void Shop_role_permissions(ShopMemberRole role, ShopPermission permission, bool expected)
    {
        Assert.Equal(expected, ShopRolePermissions.Allows(role, permission));
    }

    [Fact]
    public void Commission_override_is_range_checked()
    {
        var shop = ActiveShop(Guid.NewGuid());

        shop.SetCommissionRate(1200, Now);
        Assert.Equal(1200, shop.CommissionRateBps);
        Assert.Throws<DomainException>(() => shop.SetCommissionRate(10_001, Now));
    }

    [Fact]
    public void Rejection_requires_a_reason_and_a_pending_shop()
    {
        var shop = Shop.Apply(Guid.NewGuid(), "a-shop", Profile(), Now);

        Assert.Throws<DomainException>(() => shop.Reject("  ", Now));
        Assert.Equal(ShopStatus.PendingApproval, shop.Status); // a failed validation must not change state
        shop.Reject("Missing business license", Now);

        Assert.Equal(ShopStatus.Rejected, shop.Status);
        Assert.Equal("Missing business license", shop.StatusReason);
        Assert.False(shop.AcceptsOrders);
        Assert.Throws<DomainException>(() => shop.Approve(Now)); // approval cannot skip resubmission
        Assert.Throws<DomainException>(() => ActiveShop(Guid.NewGuid()).Reject("x", Now));
    }

    [Fact]
    public void Resubmission_returns_to_review_and_clears_the_reason()
    {
        var shop = Shop.Apply(Guid.NewGuid(), "a-shop", Profile(), Now);
        shop.Reject("Incomplete address", Now);

        shop.Resubmit(Now);

        Assert.Equal(ShopStatus.PendingApproval, shop.Status);
        Assert.Null(shop.StatusReason);
        Assert.Throws<DomainException>(() => shop.Resubmit(Now));
    }
}
