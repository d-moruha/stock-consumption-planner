using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public static class ConsumptionPlanQueries
{
    public static Task<bool> PlanExistsAsync(
        this PlannerDbContext dbContext,
        StockDocumentKey key,
        PlanTargetMonth target,
        CancellationToken cancellationToken)
    {
        return dbContext.ConsumptionPlans.AsNoTracking().AnyAsync(
            p => p.MaterialDocumentYear == key.MaterialDocumentYear &&
                 p.MaterialDocumentNumber == key.MaterialDocumentNumber &&
                 p.MaterialDocumentItem == key.MaterialDocumentItem &&
                 p.TargetYear == target.Year &&
                 p.TargetMonth == target.Month,
            cancellationToken);
    }
}
