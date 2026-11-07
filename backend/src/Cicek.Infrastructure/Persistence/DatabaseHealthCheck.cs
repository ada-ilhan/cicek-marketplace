using Cicek.Application.Abstractions.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cicek.Infrastructure.Persistence;

/// <summary>Readiness check: a SELECT 1 FROM DUAL round trip. Reports no connection details.</summary>
internal sealed class DatabaseHealthCheck(IDatabaseStatusProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await probe.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Database unreachable.");
}
