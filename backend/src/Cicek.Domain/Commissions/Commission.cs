using Cicek.SharedKernel.Domain;
using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Commissions;

/// <summary>Commission rate in basis points (1 bp = 0.01 %). 1500 = 15 %.</summary>
public readonly record struct CommissionRate
{
    public const int MaxBasisPoints = 10_000;

    private CommissionRate(int basisPoints) => BasisPoints = basisPoints;

    public int BasisPoints { get; }

    public static CommissionRate FromBasisPoints(int basisPoints) =>
        basisPoints is >= 0 and <= MaxBasisPoints
            ? new CommissionRate(basisPoints)
            : throw new DomainException("commission.invalid_rate", "Commission rate must be between 0 and 10000 basis points.");

    public override string ToString() => $"{BasisPoints / 100m:0.##}%";
}

/// <summary>Commission figures recorded on an order. <c>Commission + ShopEarnings == Base</c> exactly.</summary>
public sealed record CommissionBreakdown(Money Base, CommissionRate Rate, Money Commission, Money ShopEarnings);

/// <summary>
/// Platform commission rule. The base is the item subtotal after shop-funded discounts; delivery fees are
/// excluded (they belong to the platform, which arranges delivery). The commission is rounded to the
/// currency's minor units (half away from zero) and the shop receives the exact remainder, so no amount is
/// lost or created by rounding.
/// </summary>
public static class CommissionCalculator
{
    public static CommissionBreakdown Calculate(Money commissionBase, CommissionRate rate)
    {
        if (commissionBase.IsNegative)
        {
            throw new DomainException("commission.negative_base", "Commission base cannot be negative.");
        }

        var raw = commissionBase.Amount * rate.BasisPoints / CommissionRate.MaxBasisPoints;
        var commission = new Money(
            decimal.Round(raw, commissionBase.Currency.MinorUnits, MidpointRounding.AwayFromZero),
            commissionBase.Currency);
        return new CommissionBreakdown(commissionBase, rate, commission, commissionBase - commission);
    }

    /// <summary>Shop-specific override when set, otherwise the platform default.</summary>
    public static CommissionRate ResolveRate(int? shopOverrideBasisPoints, CommissionRate platformDefault) =>
        shopOverrideBasisPoints is { } bps ? CommissionRate.FromBasisPoints(bps) : platformDefault;
}
