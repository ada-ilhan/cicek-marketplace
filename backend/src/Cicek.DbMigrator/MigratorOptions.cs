using System.ComponentModel.DataAnnotations;

namespace Cicek.DbMigrator;

/// <summary>Configuration section "Migrator".</summary>
public sealed class MigratorOptions
{
    public const string SectionName = "Migrator";

    /// <summary>Role that receives runtime object privileges (created by db/bootstrap).</summary>
    [Required]
    [RegularExpression("^[A-Z][A-Z0-9_$#]{0,127}$")]
    public string RuntimeRole { get; set; } = "CICEK_APP_ROLE";
}
