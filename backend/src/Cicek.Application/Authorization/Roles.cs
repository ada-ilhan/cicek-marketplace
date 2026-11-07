namespace Cicek.Application.Authorization;

/// <summary>Platform roles. Store-scoped florist permissions are added with the Stores module (Stage 3).</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string FloristOwner = "FloristOwner";
    public const string FloristStaff = "FloristStaff";
    public const string Support = "Support";
    public const string Finance = "Finance";
    public const string Administrator = "Administrator";

    public static readonly IReadOnlyList<string> All =
        [Customer, FloristOwner, FloristStaff, Support, Finance, Administrator];
}
