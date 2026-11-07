using Cicek.Application.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cicek.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically expires unpaid orders so their reserved stock returns to sale. Safe to run in several API
/// instances at once: each order transition is optimistic-concurrency protected, so an order is expired once.
/// </summary>
internal sealed class OrderExpiryWorker(IServiceScopeFactory scopes, ILogger<OrderExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<ExpireUnpaidOrdersHandler>().HandleAsync(BatchSize, stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} unpaid order(s) and released their stock.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A failed sweep (e.g. database briefly down) must not stop the worker; it is logged and retried.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Order expiry sweep failed; will retry in {Interval}.", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
