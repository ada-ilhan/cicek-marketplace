using Cicek.Application.Abstractions.Persistence;

namespace Cicek.Application.SystemStatus;

public sealed record SystemStatusDto(DateTimeOffset ServerTimeUtc, DatabaseStatusDto Database);

/// <summary>Operator-facing database status. Contains no connection details or credentials.</summary>
public sealed record DatabaseStatusDto(
    bool IsReachable,
    int? AppliedMigrationCount,
    IReadOnlyList<string> PendingMigrations,
    string? LatestAppliedMigration,
    string? FailureCode);

public sealed class GetSystemStatusHandler(IDatabaseStatusProbe databaseProbe, TimeProvider timeProvider)
{
    public async Task<SystemStatusDto> HandleAsync(CancellationToken cancellationToken)
    {
        var db = await databaseProbe.GetStatusAsync(cancellationToken);
        return new SystemStatusDto(
            timeProvider.GetUtcNow(),
            new DatabaseStatusDto(
                db.IsReachable,
                db.AppliedMigrationCount,
                db.PendingMigrations,
                db.LatestAppliedMigration,
                db.FailureCode));
    }
}
