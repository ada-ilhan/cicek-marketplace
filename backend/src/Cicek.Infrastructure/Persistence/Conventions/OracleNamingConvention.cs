using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cicek.Infrastructure.Persistence.Conventions;

/// <summary>
/// Renames tables, columns and constraints to upper-case snake case (e.g. <c>NORMALIZED_EMAIL</c>).
/// EF Core quotes identifiers; upper-case names mean SQL, PL/SQL and DBA tooling can use them unquoted.
/// Constraint and index names are prefixed with their table name because Oracle requires them to be
/// unique per schema.
/// </summary>
internal static class OracleNamingConvention
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            tableName = ToUpperSnakeCase(tableName);
            entity.SetTableName(tableName);
            var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(table) ?? property.Name;
                property.SetColumnName(ToUpperSnakeCase(column));
            }

            foreach (var key in entity.GetKeys())
            {
                var columns = string.Join("_", key.Properties.Select(p => ToUpperSnakeCase(p.Name)));
                key.SetName(key.IsPrimaryKey() ? $"PK_{tableName}" : $"AK_{tableName}_{columns}");
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var principal = ToUpperSnakeCase(foreignKey.PrincipalEntityType.GetTableName() ?? foreignKey.PrincipalEntityType.ShortName());
                var columns = string.Join("_", foreignKey.Properties.Select(p => ToUpperSnakeCase(p.Name)));
                foreignKey.SetConstraintName($"FK_{tableName}_{principal}_{columns}");
            }

            foreach (var index in entity.GetIndexes())
            {
                // Oracle EF Core does not support filtered indexes (HasFilter). Unique B-tree indexes in Oracle
                // already ignore rows whose key columns are all NULL, which is what the filters were for.
                index.SetFilter(null);
                var columns = string.Join("_", index.Properties.Select(p => ToUpperSnakeCase(p.Name)));
                index.SetDatabaseName($"{(index.IsUnique ? "UX" : "IX")}_{tableName}_{columns}");
            }
        }
    }

    /// <summary>"NormalizedUserName" → "NORMALIZED_USER_NAME"; "IPAddress" → "IP_ADDRESS"; "IDN_USERS" unchanged.</summary>
    public static string ToUpperSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && i > 0)
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (previous != '_' && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower)))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}
