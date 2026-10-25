using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.ReconcileOrphans;

/// <summary>
/// After each stock import promotion, marks plans without an Active stock row as orphaned and
/// re-links orphaned plans whose stock key is Active again. Plans that stay orphaned keep their
/// original <see cref="ConsumptionPlan.OrphanedAtUtc"/>.
/// </summary>
public sealed class ConsumptionPlanPromotionParticipant(
    TimeProvider timeProvider,
    ILogger<ConsumptionPlanPromotionParticipant> logger) : IStockPromotionParticipant
{
    public async Task OnStockPromotedAsync(
        PlannerDbContext dbContext,
        int activeBatchId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        IQueryable<StockReportItemEntity> activeStock = dbContext.ActiveStockRows();

        int orphaned = await dbContext.ConsumptionPlans
            .Where(p => p.OrphanedAtUtc == null &&
                        !activeStock.Any(i => i.MaterialDocumentYear == p.MaterialDocumentYear &&
                                              i.MaterialDocumentNumber == p.MaterialDocumentNumber &&
                                              i.MaterialDocumentItem == p.MaterialDocumentItem))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.OrphanedAtUtc, (DateTimeOffset?)nowUtc),
                cancellationToken);

        int relinked = await dbContext.ConsumptionPlans
            .Where(p => p.OrphanedAtUtc != null &&
                        activeStock.Any(i => i.MaterialDocumentYear == p.MaterialDocumentYear &&
                                             i.MaterialDocumentNumber == p.MaterialDocumentNumber &&
                                             i.MaterialDocumentItem == p.MaterialDocumentItem))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.OrphanedAtUtc, (DateTimeOffset?)null),
                cancellationToken);

        if (orphaned > 0 || relinked > 0)
        {
            logger.LogInformation(
                "Stock batch {BatchId} promoted: {OrphanedCount} consumption plan(s) orphaned, {RelinkedCount} re-linked.",
                activeBatchId,
                orphaned,
                relinked);
        }
    }
}
