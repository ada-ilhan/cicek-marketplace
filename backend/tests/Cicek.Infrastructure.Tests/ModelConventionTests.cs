using System.Text.RegularExpressions;
using Cicek.Infrastructure.Persistence;
using Cicek.Infrastructure.Persistence.Conventions;
using Cicek.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cicek.Infrastructure.Tests;

/// <summary>Oracle mapping rules verified on the EF Core model (no database needed).</summary>
public sealed partial class ModelConventionTests
{
    private static AppDbContext CreateContext() => new AppDbContextDesignTimeFactory().CreateDbContext([]);

    [Fact]
    public void Model_matches_the_latest_migration_snapshot()
    {
        using var db = CreateContext();

        Assert.False(db.Database.HasPendingModelChanges(), "Run `dotnet ef migrations add` — the model changed without a migration.");
    }

    [Fact]
    public void Every_string_column_has_an_explicit_maximum_length()
    {
        using var db = CreateContext();
        var unbounded =
            from entity in db.Model.GetEntityTypes()
            from property in entity.GetProperties()
            where property.ClrType == typeof(string) && property.GetMaxLength() is null && property.GetColumnType() is null
            select $"{entity.GetTableName()}.{property.GetColumnName()}";

        Assert.Empty(unbounded);
    }

    [Fact]
    public void No_DateTime_properties_are_mapped_use_DateTimeOffset_in_UTC()
    {
        using var db = CreateContext();
        var offenders =
            from entity in db.Model.GetEntityTypes()
            from property in entity.GetProperties()
            where (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTime)
            select $"{entity.GetTableName()}.{property.GetColumnName()}";

        Assert.Empty(offenders);
    }

    [Fact]
    public void Decimals_have_explicit_precision_and_scale()
    {
        using var db = CreateContext();
        var offenders =
            from entity in db.Model.GetEntityTypes()
            from property in entity.GetProperties()
            where (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal)
                  && (property.GetPrecision() is null || property.GetScale() is null)
            select property.Name;

        Assert.Empty(offenders);
    }

    [Fact]
    public void Indexes_have_no_filters_because_the_Oracle_provider_does_not_support_them()
    {
        using var db = CreateContext();

        Assert.DoesNotContain(db.Model.GetEntityTypes().SelectMany(e => e.GetIndexes()), i => i.GetFilter() is not null);
    }

    [Fact]
    public void All_database_identifiers_are_unquoted_style_upper_case_and_unique()
    {
        using var db = CreateContext();
        var names = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            names.Add(table);
            names.AddRange(entity.GetProperties().Select(p => p.GetColumnName(store)!).Select(c => $"{table}.{c}"));
            names.AddRange(entity.GetKeys().Select(k => k.GetName()!));
            names.AddRange(entity.GetForeignKeys().Select(f => f.GetConstraintName()!));
            names.AddRange(entity.GetIndexes().Select(i => i.GetDatabaseName()!));
        }

        foreach (var name in names)
        {
            foreach (var part in name.Split('.'))
            {
                Assert.Matches(OracleIdentifier(), part);
                Assert.True(part.Length <= 128, $"{part} exceeds 128 bytes");
            }
        }

        var schemaObjects = names.Where(n => !n.Contains('.', StringComparison.Ordinal)).ToList();
        Assert.Equal(schemaObjects.Count, schemaObjects.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("NormalizedUserName", "NORMALIZED_USER_NAME")]
    [InlineData("IPAddress", "IP_ADDRESS")]
    [InlineData("IDN_USERS", "IDN_USERS")]
    [InlineData("Id", "ID")]
    [InlineData("Line2Text", "LINE2_TEXT")]
    public void Upper_snake_case_conversion(string input, string expected)
    {
        Assert.Equal(expected, OracleNamingConvention.ToUpperSnakeCase(input));
    }

    [Fact]
    public void Embedded_PLSQL_sources_are_present_and_free_of_SQLPlus_terminators()
    {
        foreach (var resource in new[] { SqlResources.SchemaAccessSpecV1, SqlResources.SchemaAccessBodyV1 })
        {
            var source = SqlResources.Read(resource);
            Assert.StartsWith("CREATE OR REPLACE PACKAGE", source, StringComparison.Ordinal);
            Assert.EndsWith("END cicek_schema_access;", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", source, StringComparison.Ordinal);
            Assert.DoesNotMatch(SqlPlusSlash(), source);
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_$#]*$")]
    private static partial Regex OracleIdentifier();

    [GeneratedRegex(@"^\s*/\s*$", RegexOptions.Multiline)]
    private static partial Regex SqlPlusSlash();
}
