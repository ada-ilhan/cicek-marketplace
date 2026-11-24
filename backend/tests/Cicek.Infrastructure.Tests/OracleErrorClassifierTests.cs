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

    private const string WrapperMessage = "ORA-50201: Oracle Communication: Failed to connect to server or failed to parse connect string";

    [Fact]
    public void Communication_wrapper_with_a_TCP_transport_cause_is_unavailable()
    {
        var cause = new InvalidOperationException(WrapperMessage,
            new InvalidOperationException("ORA-50232: Network Transport: TCP transport address connect failure for host 127.0.0.1 port 1599."));

        var failure = OracleDatabaseErrorClassifier.ClassifyOracleError(50201, WrapperMessage, cause);

        Assert.Equal(DatabaseFailureKind.Unavailable, failure.Kind);
        Assert.Equal(50201, failure.ProviderCode);
    }

    [Fact]
    public void Communication_wrapper_alone_is_not_unavailable()
    {
        Assert.Equal(DatabaseFailureKind.Unknown, OracleDatabaseErrorClassifier.ClassifyOracleError(50201, WrapperMessage, null).Kind);
    }

    [Theory]
    [InlineData("ORA-50201: Oracle Communication: Failed to connect to server or failed to parse connect string")]
    [InlineData("ORA-50203: some other communication error")]
    [InlineData("TNS-12345 unrelated")]
    [InlineData("connect string mentions ORA-50232: but does not start with it")]
    [InlineData("ORA-502321: not the same number")]
    public void Communication_wrapper_with_an_unrelated_cause_stays_unknown(string innerMessage)
    {
        var cause = new InvalidOperationException(innerMessage);

        Assert.Equal(DatabaseFailureKind.Unknown, OracleDatabaseErrorClassifier.ClassifyOracleError(50201, WrapperMessage, cause).Kind);
    }

    [Fact]
    public void A_TCP_transport_cause_does_not_reclassify_other_error_numbers()
    {
        var cause = new InvalidOperationException("ORA-50232: Network Transport: TCP transport address connect failure");

        Assert.Equal(DatabaseFailureKind.Unknown, OracleDatabaseErrorClassifier.ClassifyOracleError(942, "ORA-00942: table or view does not exist", cause).Kind);
    }

    [Theory]
    [InlineData(1017, DatabaseFailureKind.Unavailable)]
    [InlineData(28000, DatabaseFailureKind.Unavailable)]
    [InlineData(12541, DatabaseFailureKind.Unavailable)]
    [InlineData(12170, DatabaseFailureKind.Unavailable)]
    [InlineData(50000, DatabaseFailureKind.Unavailable)]
    [InlineData(1, DatabaseFailureKind.UniqueViolation)]
    [InlineData(60, DatabaseFailureKind.TransientContention)]
    [InlineData(942, DatabaseFailureKind.Unknown)]
    public void Existing_classifications_are_unchanged(int code, DatabaseFailureKind expected)
    {
        Assert.Equal(expected, OracleDatabaseErrorClassifier.ClassifyOracleError(code, $"ORA-{code:D5}: x", null).Kind);
    }

    [Fact]
    public void Error_codes_are_formatted_with_five_digits()
    {
        Assert.Equal("ORA-00001", OracleDatabaseErrorClassifier.FormatCode(1));
        Assert.Equal("ORA-12541", OracleDatabaseErrorClassifier.FormatCode(12541));
    }
}
