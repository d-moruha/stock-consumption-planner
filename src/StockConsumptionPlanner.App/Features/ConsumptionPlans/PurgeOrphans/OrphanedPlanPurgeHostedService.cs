using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.PurgeOrphans;

/// <summary>
/// Purges long-orphaned plans at startup and then every <see cref="ConsumptionPlanOptions.PurgeInterval"/>.
/// Runs independently of imports so cleanup still happens when no reports are imported for months.
/// </summary>
public sealed class OrphanedPlanPurgeHostedService(
    PurgeOrphanedPlansHandler handler,
    ConsumptionPlanOptions options,
    TimeProvider timeProvider,
    ILogger<OrphanedPlanPurgeHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.PurgeInterval <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Yield();
        await PurgeOnceAsync(stoppingToken);

        using PeriodicTimer timer = new(options.PurgeInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PurgeOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PurgeOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            int purged = await handler.PurgeAsync(stoppingToken);
            if (purged > 0)
            {
                logger.LogInformation("Purged {PurgedCount} consumption plan(s) orphaned before {CutoffUtc:u}.", purged, handler.GetCutoffUtc());
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Purging orphaned consumption plans failed; it will be retried on the next run.");
        }
    }
}
