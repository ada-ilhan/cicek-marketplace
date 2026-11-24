using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Cicek.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Maps Oracle error numbers to provider-neutral <see cref="DatabaseFailure"/> values.
/// Raw ORA messages never leave this layer except in server-side logs.
/// </summary>
internal sealed partial class OracleDatabaseErrorClassifier : IDatabaseErrorClassifier
{
    private const int UniqueConstraintViolated = 1;            // ORA-00001
    private const int ParentKeyNotFound = 2291;                // ORA-02291
    private const int ChildRecordFound = 2292;                 // ORA-02292
    private const int Deadlock = 60;                           // ORA-00060
    private const int CannotSerialize = 8177;                  // ORA-08177
    private const int CommunicationFailure = 50201;            // ODP.NET managed: generic "Oracle Communication" wrapper
    private const int TcpTransportConnectFailure = 50232;      // ODP.NET managed: TCP transport address connect failure
    private const int ApplicationErrorMin = 20000;            // ORA-20000 … ORA-20999 (RAISE_APPLICATION_ERROR)
    private const int ApplicationErrorMax = 20999;

    /// <summary>Connectivity, availability, timeout and connect-time authentication errors.</summary>
    private static readonly FrozenSet<int> UnavailableCodes = new[]
    {
        1013,  // user requested cancel (command timeout)
        1017,  // invalid username/password
        1033,  // initialization or shutdown in progress
        1034,  // Oracle not available
        1089,  // immediate shutdown in progress
        1090,  // shutdown in progress
        3113,  // end-of-file on communication channel
        3114,  // not connected
        3135,  // connection lost contact
        12152, // TNS: unable to send break message
        12170, // TNS: connect timeout
        12514, // TNS: listener does not know of service
        12516, // TNS: no available handler
        12518, // TNS: listener could not hand off
        12519, // TNS: no appropriate service handler
        12520, // TNS: no available handler for server type
        12521, // TNS: listener does not know of instance
        12528, // TNS: all instances blocking new connections
        12537, // TNS: connection closed
        12541, // TNS: no listener
        12543, // TNS: destination host unreachable
        12545, // connect failed: target host or object does not exist
        12547, // TNS: lost contact
        12560, // TNS: protocol adapter error
        12571, // TNS: packet writer failure
        28000, // account locked
        50000, // ODP.NET: connection request timed out (pool exhausted)
    }.ToFrozenSet();

    public DatabaseFailure? Classify(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return new DatabaseFailure(DatabaseFailureKind.ConcurrencyConflict, null);
        }

        var oracle = FindOracleException(exception);
        if (oracle is null)
        {
            return null;
        }

        return ClassifyOracleError(oracle.Number, oracle.Message, oracle.InnerException);
    }

    /// <summary>Classification by Oracle error number; <paramref name="cause"/> is the provider exception's inner chain.</summary>
    internal static DatabaseFailure ClassifyOracleError(int code, string message, Exception? cause)
    {
        return code switch
        {
            UniqueConstraintViolated => new DatabaseFailure(DatabaseFailureKind.UniqueViolation, code),
            ParentKeyNotFound or ChildRecordFound => new DatabaseFailure(DatabaseFailureKind.ReferenceViolation, code),
            Deadlock or CannotSerialize => new DatabaseFailure(DatabaseFailureKind.TransientContention, code),
            >= ApplicationErrorMin and <= ApplicationErrorMax =>
                new DatabaseFailure(DatabaseFailureKind.ApplicationRule, code, ExtractApplicationCode(message)),
            _ when UnavailableCodes.Contains(code) => new DatabaseFailure(DatabaseFailureKind.Unavailable, code),
            // ORA-50201 is a generic wrapper (also raised for connect-string and other client-side problems);
            // it means "unavailable" only when the driver reports a TCP transport connect failure as its cause.
            CommunicationFailure when HasTcpTransportConnectFailure(cause) => new DatabaseFailure(DatabaseFailureKind.Unavailable, code),
            _ => new DatabaseFailure(DatabaseFailureKind.Unknown, code),
        };
    }

    /// <summary>
    /// True when an inner exception's message starts with the ORA-50232 error number (managed driver: "TCP transport address
    /// connect failure"). Only the leading error number is read, not the descriptive text. The driver's internal exception
    /// types are not referenced because they are not public API.
    /// </summary>
    internal static bool HasTcpTransportConnectFailure(Exception? cause)
    {
        for (var current = cause; current is not null; current = current.InnerException)
        {
            if (LeadingOraNumber(current.Message) == TcpTransportConnectFailure)
            {
                return true;
            }
        }

        return false;
    }

    private static int? LeadingOraNumber(string? message)
    {
        var match = message is null ? null : LeadingOraPattern().Match(message);
        return match is { Success: true } ? int.Parse(match.Groups["n"].ValueSpan, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"^\s*ORA-(?<n>\d{5}):", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingOraPattern();

    /// <summary>Formats an Oracle error number for operator-facing output, e.g. "ORA-12541".</summary>
    public static string FormatCode(int code) => $"ORA-{code:D5}";

    internal static OracleException? FindOracleException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracle)
            {
                return oracle;
            }
        }

        return null;
    }

    /// <summary>
    /// PL/SQL error contract (docs/oracle-database.md): RAISE_APPLICATION_ERROR(-20nnn, '&lt;code&gt;: message')
    /// where &lt;code&gt; is a stable lower-case identifier such as "schema_access.role_not_found".
    /// </summary>
    internal static string? ExtractApplicationCode(string message)
    {
        var match = ApplicationCodePattern().Match(message);
        return match.Success ? match.Groups["code"].Value : null;
    }

    [GeneratedRegex(@"ORA-20\d{3}: (?<code>[a-z][a-z0-9_]*(\.[a-z0-9_]+)+):", RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationCodePattern();
}
