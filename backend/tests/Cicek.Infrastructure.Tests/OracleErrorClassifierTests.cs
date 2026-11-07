using Cicek.Application.Abstractions.Persistence;
using Cicek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cicek.Infrastructure.Tests;

public sealed class OracleErrorClassifierTests
{
    [Theory]
    [InlineData("ORA-20001: schema_access.invalid_grantee: The grantee must be a simple SQL name.\nORA-06512: at line 1", "schema_access.invalid_grantee")]
    [InlineData("ORA-20002: schema_access.grantee_not_found: Grantee X does not exist.", "schema_access.grantee_not_found")]
    [InlineData("ORA-20001: no code here", null)]
    [InlineData("ORA-20001: UPPER.case: rejected", null)]
    public void Application_error_codes_follow_the_PLSQL_contract(string message, string? expected)
    {
        Assert.Equal(expected, OracleDatabaseErrorClassifier.ExtractApplicationCode(message));
    }

    [Fact]
    public void Concurrency_exception_is_classified_without_an_Oracle_exception()
    {
        var failure = new OracleDatabaseErrorClassifier().Classify(new DbUpdateConcurrencyException("conflict"));

        Assert.Equal(DatabaseFailureKind.ConcurrencyConflict, failure?.Kind);
    }

    [Fact]
    public void Non_database_exceptions_are_not_classified()
    {
        Assert.Null(new OracleDatabaseErrorClassifier().Classify(new InvalidOperationException("x")));
    }

    [Fact]
    public void Error_codes_are_formatted_with_five_digits()
    {
        Assert.Equal("ORA-00001", OracleDatabaseErrorClassifier.FormatCode(1));
        Assert.Equal("ORA-12541", OracleDatabaseErrorClassifier.FormatCode(12541));
    }
}
