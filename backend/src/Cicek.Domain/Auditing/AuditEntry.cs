namespace Cicek.Domain.Auditing;

/// <summary>
/// Append-only record of a sensitive action (administrative decisions, manual payments, catalog governance).
/// The runtime database role can only INSERT and SELECT this table (cicek_schema_access v2).
/// </summary>
public sealed class AuditEntry
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 64;
    public const int EntityIdMaxLength = 64;
    public const int DetailsMaxLength = 1000;

    public AuditEntry(Guid? actorUserId, string action, string entityType, string entityId, string? details, DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        ActorUserId = actorUserId;
        Action = Truncate(action, ActionMaxLength);
        EntityType = Truncate(entityType, EntityTypeMaxLength);
        EntityId = Truncate(entityId, EntityIdMaxLength);
        Details = details is null ? null : Truncate(details, DetailsMaxLength);
        OccurredAt = occurredAt;
    }

    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
