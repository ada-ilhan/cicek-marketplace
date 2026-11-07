using Cicek.Domain.Shops;
using FluentValidation;

namespace Cicek.Application.Shops;

/// <summary>Shop profile input (application and updates).</summary>
public sealed record ShopProfileRequest(
    string Name,
    string? Description,
    string ContactEmail,
    string ContactPhone,
    string AddressLine,
    string District,
    string City,
    string? PostalCode,
    string? ImageUrl = null)
{
    public ShopProfile ToProfile() =>
        new(Name, Description ?? string.Empty, ContactEmail, ContactPhone, AddressLine, District, City, PostalCode ?? string.Empty, ImageUrl);
}

public sealed record UpdateShopProfileRequest(ShopProfileRequest Profile, long Version);

public sealed record SuspendShopRequest(string Reason);

public sealed record SetCommissionRequest(int? CommissionRateBps);

/// <summary>Public shop card / detail (only active shops are public).</summary>
public sealed record PublicShopDto(Guid Id, string Name, string Slug, string? Description, string District, string City, string? ImageUrl);

/// <summary>Shop as seen by its members and administrators.</summary>
public sealed record ShopManagementDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string ContactEmail,
    string ContactPhone,
    string AddressLine,
    string District,
    string City,
    string? PostalCode,
    string? ImageUrl,
    ShopStatus Status,
    string? StatusReason,
    int? CommissionRateBps,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    ShopMemberRole? MyRole,
    long Version);

public sealed class ShopProfileRequestValidator : AbstractValidator<ShopProfileRequest>
{
    public ShopProfileRequestValidator(Catalog.ImageUrlPolicy imagePolicy)
    {
        RuleFor(x => x.ImageUrl).Must(imagePolicy.IsAllowed).When(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
            .WithMessage("Image must be an uploaded image or an HTTPS URL on an allowed host.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Shop.NameMaxLength)
            .Must(n => Domain.Common.SearchText.Slugify(n).Length > 0).WithMessage("Name must contain letters or digits.");
        RuleFor(x => x.Description).MaximumLength(Shop.DescriptionMaxLength);
        RuleFor(x => x.ContactEmail).NotEmpty().MaximumLength(Shop.EmailMaxLength).EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(Shop.PhoneMaxLength).Matches(@"^\+?[0-9 ()-]{7,}$");
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(Shop.AddressLineMaxLength);
        RuleFor(x => x.District).NotEmpty().MaximumLength(Shop.DistrictMaxLength);
        RuleFor(x => x.City).NotEmpty().MaximumLength(Shop.CityMaxLength);
        RuleFor(x => x.PostalCode).MaximumLength(Shop.PostalCodeMaxLength);
    }
}

public sealed class UpdateShopProfileRequestValidator : AbstractValidator<UpdateShopProfileRequest>
{
    public UpdateShopProfileRequestValidator(IValidator<ShopProfileRequest> profileValidator)
    {
        RuleFor(x => x.Profile).NotNull().SetValidator(profileValidator);
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0);
    }
}

public sealed class SuspendShopRequestValidator : AbstractValidator<SuspendShopRequest>
{
    public SuspendShopRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(Shop.StatusReasonMaxLength);
}

public sealed class SetCommissionRequestValidator : AbstractValidator<SetCommissionRequest>
{
    public SetCommissionRequestValidator() =>
        RuleFor(x => x.CommissionRateBps).InclusiveBetween(0, Domain.Commissions.CommissionRate.MaxBasisPoints).When(x => x.CommissionRateBps is not null);
}
