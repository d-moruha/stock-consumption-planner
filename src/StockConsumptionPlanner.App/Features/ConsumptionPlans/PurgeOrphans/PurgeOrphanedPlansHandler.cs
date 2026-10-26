using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.PurgeOrphans;

public sealed class PurgeOrphanedPlansHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ConsumptionPlanOptions options)
{
    public DateTimeOffset GetCutoffUtc() => timeProvider.GetUtcNow().AddMonths(-options.OrphanRetentionMonths);

    /// <summary>Deletes plans that have been orphaned for at least the retention period. Safe to run concurrently.</summary>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoffUtc = GetCutoffUtc();

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ConsumptionPlans
            .Where(p => p.OrphanedAtUtc != null && p.OrphanedAtUtc <= cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
