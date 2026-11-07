using Cicek.Infrastructure.Persistence.Migrations;
using Cicek.Tests.Shared;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Tests.Oracle;

/// <summary>
/// The Azerbaijani seed-name migration must not overwrite category names an administrator has changed. Runs against the
/// isolated TEST schema only (the fixture refuses any other schema name) and restores every row it touches.
/// </summary>
[Collection(OracleCollection.Name)]
public sealed class CategoryNameMigrationTests(OracleTestDatabase database)
{
    private static readonly Guid Flowers = new("0199d6a0-0000-7000-8000-000000000001");
    private static readonly Guid Roses = new("0199d6a0-0000-7000-8000-000000000011");
    private static readonly Guid Orchids = new("0199d6a0-0000-7000-8000-000000000012");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Hex(Guid id) => Convert.ToHexString(id.ToByteArray());

    private static async Task<string?> NameAsync(OracleConnection connection, Guid id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT NAME FROM CAT_CATEGORIES WHERE ID = HEXTORAW('{Hex(id)}')";
        return (string?)await command.ExecuteScalarAsync(Ct);
    }

    private static async Task SetNameAsync(OracleConnection connection, Guid id, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE CAT_CATEGORIES SET NAME = :name WHERE ID = HEXTORAW('{Hex(id)}')";
        command.Parameters.Add(new OracleParameter("name", name));
        await command.ExecuteNonQueryAsync(Ct);
    }

    [Fact]
    public async Task A_fresh_installation_ends_with_the_Azerbaijani_seed_names()
    {
        database.SkipIfUnavailable();
        await using var connection = new OracleConnection(database.RequiredSettings.OwnerConnectionString());
        await connection.OpenAsync(Ct);

        // The fixture migrated an empty schema: Turkish seed names, then this migration.
        Assert.Equal("Çiçəklər", await NameAsync(connection, Flowers));
        Assert.Equal("Qızılgüllər", await NameAsync(connection, Roses));
    }

    [Fact]
    public async Task Re_running_the_migration_keeps_administrator_edits_and_still_renames_untouched_seed_rows()
    {
        database.SkipIfUnavailable();
        await using var connection = new OracleConnection(database.RequiredSettings.OwnerConnectionString());
        await connection.OpenAsync(Ct);
        try
        {
            await SetNameAsync(connection, Flowers, "Admin tərəfindən dəyişdi"); // an administrator's rename
            await SetNameAsync(connection, Orchids, "Orkideler"); // still the original Turkish seed name

            var statements = new AzerbaijaniCategoryNames().UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();
            Assert.NotEmpty(statements);
            foreach (var sql in statements)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(Ct);
            }

            Assert.Equal("Admin tərəfindən dəyişdi", await NameAsync(connection, Flowers));
            Assert.Equal("Orxideyalar", await NameAsync(connection, Orchids));
        }
        finally
        {
            await SetNameAsync(connection, Flowers, "Çiçəklər");
            await SetNameAsync(connection, Orchids, "Orxideyalar");
        }
    }
}
