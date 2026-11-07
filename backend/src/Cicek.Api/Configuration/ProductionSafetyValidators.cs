using Cicek.Application.Common;
using Microsoft.Extensions.Options;

namespace Cicek.Api.Configuration;

/// <summary>
/// Settings that exist only to make development and automated tests convenient must never be active in a deployed
/// environment. "Deployed" means anything other than Development and Testing, so a mistyped or unset
/// <c>ASPNETCORE_ENVIRONMENT</c> (which defaults to Production) is the safe side.
/// </summary>
internal static class EnvironmentSafety
{
    public static bool AllowsDevelopmentConveniences(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing");
}

/// <summary>Refuses to start with <c>Marketplace:AllowAnyExternalImageHost</c> outside development/test.</summary>
internal sealed class MarketplaceSafetyValidator(IHostEnvironment environment) : IValidateOptions<MarketplaceOptions>
{
    public ValidateOptionsResult Validate(string? name, MarketplaceOptions options) =>
        options.AllowAnyExternalImageHost && !EnvironmentSafety.AllowsDevelopmentConveniences(environment)
            ? ValidateOptionsResult.Fail(
                "Marketplace:AllowAnyExternalImageHost is a development/test switch and must be false in this environment; list trusted hosts in Marketplace:ImageHostAllowlist instead.")
            : ValidateOptionsResult.Success;
}

/// <summary>Refuses to start with the local-disk image store (development/test only) outside development/test.</summary>
internal sealed class MediaSafetyValidator(IHostEnvironment environment) : IValidateOptions<MediaOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaOptions options) =>
        string.Equals(options.Provider, "Local", StringComparison.Ordinal) && !EnvironmentSafety.AllowsDevelopmentConveniences(environment)
            ? ValidateOptionsResult.Fail(
                "Media:Provider 'Local' stores images on the API host's disk and is for development/test only; configure an object-storage provider or 'None'.")
            : ValidateOptionsResult.Success;
}
