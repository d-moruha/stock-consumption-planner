using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

/// <summary>
/// Runs inside the stock promotion transaction, after the staged batch has become Active and
/// before the transaction commits. Throwing rolls back the whole promotion.
/// </summary>
public interface IStockPromotionParticipant
{
    Task OnStockPromotedAsync(PlannerDbContext dbContext, int activeBatchId, CancellationToken cancellationToken);
}
