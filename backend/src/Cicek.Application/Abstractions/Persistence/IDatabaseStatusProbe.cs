namespace Cicek.Application.Abstractions.Persistence;

/// <param name="IsReachable">Whether a round trip to the database succeeded.</param>
/// <param name="AppliedMigrationCount">Number of applied schema migrations (null when unreachable).</param>
/// <param name="PendingMigrations">Migrations known to this build but not applied (empty when unreachable).</param>
/// <param name="LatestAppliedMigration">Identifier of the newest applied migration.</param>
/// <param name="FailureCode">Provider error identifier when unreachable (e.g. "ORA-12541"), for operators only.</param>
public sealed record DatabaseStatus(
    bool IsReachable,
    int? AppliedMigrationCount,
    IReadOnlyList<string> PendingMigrations,
    string? LatestAppliedMigration,
    string? FailureCode);

public interface IDatabaseStatusProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    Task<DatabaseStatus> GetStatusAsync(CancellationToken cancellationToken);
}
