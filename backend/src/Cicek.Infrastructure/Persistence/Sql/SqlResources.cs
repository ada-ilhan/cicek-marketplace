namespace Cicek.Infrastructure.Persistence.Sql;

/// <summary>
/// Loads versioned SQL/PL/SQL sources embedded in this assembly. Files under <c>Persistence/Sql</c> are
/// immutable once a migration references them; a change ships as a new version folder and a new migration.
/// </summary>
internal static class SqlResources
{
    public const string SchemaAccessSpecV1 = "Packages/cicek_schema_access/v1/package_spec.sql";
    public const string SchemaAccessBodyV1 = "Packages/cicek_schema_access/v1/package_body.sql";
    public const string SchemaAccessSpecV2 = "Packages/cicek_schema_access/v2/package_spec.sql";
    public const string SchemaAccessBodyV2 = "Packages/cicek_schema_access/v2/package_body.sql";

    public static string Read(string relativePath)
    {
        var assembly = typeof(SqlResources).Assembly;
        var resourceName = $"{typeof(SqlResources).Namespace}.{relativePath.Replace('/', '.')}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);

        // Normalise line endings so the stored PL/SQL source is identical regardless of checkout platform.
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }
}
