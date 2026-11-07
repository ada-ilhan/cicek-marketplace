using System.ComponentModel.DataAnnotations;
using Cicek.Domain.Commissions;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Application.Common;

/// <summary>Configuration section "Marketplace": market-specific settings, never hard-coded.</summary>
public sealed class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    /// <summary>The single currency all prices are set in (ISO-4217).</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; set; } = "AZN";

    /// <summary>IANA time zone of the market; delivery dates are calendar dates in this zone.</summary>
    [Required]
    public string TimeZone { get; set; } = "Asia/Baku";

    [Range(0, CommissionRate.MaxBasisPoints)]
    public int DefaultCommissionRateBps { get; set; } = 1500;

    /// <summary>Flat delivery fee charged per shop order (delivery zones are later work).</summary>
    [Range(typeof(decimal), "0", "10000")]
    public decimal DeliveryFeePerOrder { get; set; }

    [Range(1, 90)]
    public int MaxDeliveryDaysAhead { get; set; } = 30;

    /// <summary>Unpaid orders older than this expire and release their stock.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "7.00:00:00")]
    public TimeSpan PaymentTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Allowed external image hosts (exact host names). Platform-uploaded images (/media/...) are always allowed.</summary>
    public string[] ImageHostAllowlist { get; set; } = [];

    /// <summary>
    /// DEVELOPMENT/TEST ONLY: accept HTTPS images from any host when the allowlist is empty. Defaults to false, so an empty
    /// allowlist in production rejects every external image URL instead of silently accepting arbitrary hosts.
    /// </summary>
    public bool AllowAnyExternalImageHost { get; set; }

    public Currency MarketCurrency => SharedKernel.Monetary.Currency.FromCode(Currency);

    public Money DeliveryFee => new(DeliveryFeePerOrder, MarketCurrency);

    public CommissionRate DefaultCommissionRate => CommissionRate.FromBasisPoints(DefaultCommissionRateBps);
}

/// <summary>Calendar of the market's time zone (delivery dates are local dates).</summary>
public sealed class MarketCalendar(TimeProvider timeProvider, Microsoft.Extensions.Options.IOptions<MarketplaceOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _zone).DateTime);

    public bool IsValidDeliveryDate(DateOnly date) => date >= Today && date <= Today.AddDays(options.Value.MaxDeliveryDaysAhead);
}
