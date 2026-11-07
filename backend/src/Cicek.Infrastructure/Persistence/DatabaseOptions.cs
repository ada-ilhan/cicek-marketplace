using System.ComponentModel.DataAnnotations;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Configuration section "Database". Credentials are supplied through environment variables
/// (e.g. <c>Database__Password</c>), user-secrets in development, or a secret manager in production;
/// they are never committed.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Oracle data source: EZConnect (<c>host:port/service_name</c>), a TNS alias or a full descriptor.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DataSource { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string UserId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Schema that owns the application objects. Set it when connecting as the least-privilege runtime
    /// user (objects are then schema-qualified). Leave empty when connecting as the schema owner (migrator).
    /// </summary>
    [RegularExpression("^[A-Z][A-Z0-9_$#]{0,127}$", ErrorMessage = "Schema must be an unquoted, upper-case Oracle identifier.")]
    public string? Schema { get; set; }

    [Range(0, 200)]
    public int MinPoolSize { get; set; } = 1;

    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>Seconds to wait for a pooled connection or a new physical connection.</summary>
    [Range(1, 120)]
    public int ConnectionTimeoutSeconds { get; set; } = 15;

    /// <summary>Default command timeout for EF Core commands.</summary>
    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Seconds after which a returned connection is closed instead of pooled (0 = unlimited). A finite
    /// value lets the pool rebalance after RAC/failover events and firewall idle timeouts.
    /// </summary>
    [Range(0, 86400)]
    public int ConnectionLifetimeSeconds { get; set; } = 1800;

    public string? NormalizedSchema => string.IsNullOrWhiteSpace(Schema) ? null : Schema;
}
