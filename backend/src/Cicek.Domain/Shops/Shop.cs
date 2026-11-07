using Cicek.Domain.Commissions;
using Cicek.Domain.Common;
using Cicek.Domain.Media;
using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Shops;

public enum ShopStatus
{
    /// <summary>Applied; not visible to customers until an administrator approves it.</summary>
    PendingApproval,

    /// <summary>Visible and accepting orders.</summary>
    Active,

    /// <summary>Temporarily closed by its owner (holidays, capacity); not visible, accepts no orders.</summary>
    Inactive,

    /// <summary>Blocked by an administrator; the owner cannot reactivate it.</summary>
    Suspended,

    /// <summary>Application declined by an administrator (reason recorded). The owner may correct the profile and resubmit.</summary>
    Rejected,
}

/// <summary>Editable shop profile fields (validated by the application layer before reaching the domain).</summary>
public sealed record ShopProfile(
    string Name,
    string Description,
    string ContactEmail,
    string ContactPhone,
    string AddressLine,
    string District,
    string City,
    string PostalCode,
    string? ImageUrl = null);

/// <summary>
/// A flower shop (florist). Status transitions:
/// PendingApproval → Active (admin approve) or Rejected (admin) → PendingApproval (owner resubmit); Active ⇄ Inactive (owner);
/// any non-suspended → Suspended (admin); Suspended → Active, or PendingApproval if never approved (admin reinstate).
/// </summary>
public sealed class Shop : Entity
{
    public const int NameMaxLength = 100;
    public const int SlugMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;
    public const int AddressLineMaxLength = 200;
    public const int DistrictMaxLength = 100;
    public const int CityMaxLength = 100;
    public const int PostalCodeMaxLength = 16;
    public const int StatusReasonMaxLength = 500;

    private readonly List<ShopMember> _members = [];

    private Shop(Guid id, string slug, ShopProfile profile, DateTimeOffset now)
        : base(id)
    {
        Slug = slug;
        ApplyProfile(profile);
        Status = ShopStatus.PendingApproval;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private Shop()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string ContactEmail { get; private set; } = string.Empty;

    public string ContactPhone { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string District { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string? PostalCode { get; private set; }

    /// <summary>Branding image: an uploaded platform image (<c>/media/{id}</c>) or an allowed HTTPS URL; null when none.</summary>
    public string? ImageUrl { get; private set; }

    public ShopStatus Status { get; private set; }

    public string? StatusReason { get; private set; }

    /// <summary>Shop-specific commission in basis points; null means the platform default applies.</summary>
    public int? CommissionRateBps { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public IReadOnlyList<ShopMember> Members => _members;

    public bool AcceptsOrders => Status == ShopStatus.Active;

    public static Shop Apply(Guid ownerUserId, string slug, ShopProfile profile, DateTimeOffset now)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new DomainException("shop.owner_required", "A shop needs an owner.");
        }

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > SlugMaxLength || slug != SearchText.Slugify(slug, SlugMaxLength))
        {
            throw new DomainException("shop.invalid_slug", "Shop slug is invalid.");
        }

        var shop = new Shop(Guid.CreateVersion7(), slug, profile, now);
        shop._members.Add(new ShopMember(shop.Id, ownerUserId, ShopMemberRole.Owner, now));
        return shop;
    }

    public void UpdateProfile(ShopProfile profile, DateTimeOffset now)
    {
        ApplyProfile(profile);
        UpdatedAt = now;
    }

    public void Approve(DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.PendingApproval, "shop.not_pending");
        Status = ShopStatus.Active;
        StatusReason = null;
        ApprovedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Owner reopens a temporarily closed shop.</summary>
    public void Activate(DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.Inactive, "shop.not_inactive");
        Status = ShopStatus.Active;
        UpdatedAt = now;
    }

    /// <summary>Owner temporarily closes the shop.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.Active, "shop.not_active");
        Status = ShopStatus.Inactive;
        UpdatedAt = now;
    }

    /// <summary>Administrator declines a pending application; the reason is shown to the owner.</summary>
    public void Reject(string reason, DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.PendingApproval, "shop.not_pending");
        var text = Guard.RequireText(reason, StatusReasonMaxLength, "shop.reason_required"); // validate before mutating
        Status = ShopStatus.Rejected;
        StatusReason = text;
        UpdatedAt = now;
    }

    /// <summary>Owner sends a rejected application back to review (after correcting the profile). Approval is never skipped.</summary>
    public void Resubmit(DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.Rejected, "shop.not_rejected");
        Status = ShopStatus.PendingApproval;
        StatusReason = null;
        UpdatedAt = now;
    }

    public void Suspend(string reason, DateTimeOffset now)
    {
        if (Status == ShopStatus.Suspended)
        {
            throw new DomainException("shop.already_suspended", "The shop is already suspended.");
        }

        var text = Guard.RequireText(reason, StatusReasonMaxLength, "shop.reason_required"); // validate before mutating
        Status = ShopStatus.Suspended;
        StatusReason = text;
        UpdatedAt = now;
    }

    public void Reinstate(DateTimeOffset now)
    {
        EnsureStatus(ShopStatus.Suspended, "shop.not_suspended");
        Status = ApprovedAt is null ? ShopStatus.PendingApproval : ShopStatus.Active;
        StatusReason = null;
        UpdatedAt = now;
    }

    public void SetCommissionRate(int? basisPoints, DateTimeOffset now)
    {
        if (basisPoints is { } bps)
        {
            CommissionRate.FromBasisPoints(bps); // validates range
        }

        CommissionRateBps = basisPoints;
        UpdatedAt = now;
    }

    public bool HasPermission(Guid userId, ShopPermission permission) =>
        _members.Exists(m => m.UserId == userId && ShopRolePermissions.Allows(m.Role, permission));

    private static string? NormalizeImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ImageReference.Normalize(value)
               ?? throw new DomainException("shop.invalid_image", "Shop image must be an uploaded platform image or an absolute HTTPS URL.");
    }

    private void ApplyProfile(ShopProfile profile)
    {
        Name = Guard.RequireText(profile.Name, NameMaxLength, "shop.name_required");
        NormalizedName = SearchText.Normalize(Name);
        Description = Guard.OptionalText(profile.Description, DescriptionMaxLength);
        ContactEmail = Guard.RequireText(profile.ContactEmail, EmailMaxLength, "shop.email_required");
        ContactPhone = Guard.RequireText(profile.ContactPhone, PhoneMaxLength, "shop.phone_required");
        AddressLine = Guard.RequireText(profile.AddressLine, AddressLineMaxLength, "shop.address_required");
        District = Guard.RequireText(profile.District, DistrictMaxLength, "shop.district_required");
        City = Guard.RequireText(profile.City, CityMaxLength, "shop.city_required");
        PostalCode = Guard.OptionalText(profile.PostalCode, PostalCodeMaxLength);
        ImageUrl = NormalizeImage(profile.ImageUrl);
    }

    private void EnsureStatus(ShopStatus expected, string code)
    {
        if (Status != expected)
        {
            throw new DomainException(code, $"Operation requires shop status {expected}, but it is {Status}.");
        }
    }
}
