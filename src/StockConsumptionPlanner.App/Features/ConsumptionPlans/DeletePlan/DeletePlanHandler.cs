using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.DeletePlan;

/// <summary>Deletes follow the same time rule as updates; orphaned plans may be deleted too.</summary>
public sealed class DeletePlanHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    PlanEditPolicy editPolicy)
{
    public async Task<ConsumptionPlanResult<bool>> HandleAsync(
        int id,
        int version,
        CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConsumptionPlan? plan = await dbContext.ConsumptionPlans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
        {
            return ConsumptionPlanError.NotFound(id);
        }

        if (plan.Version != version)
        {
            return ConsumptionPlanError.StaleVersion();
        }

        if (!editPolicy.IsWithinEditPeriod(plan.CreatedAtUtc, plan.Target))
        {
            return new ConsumptionPlanError(
                ConsumptionPlanErrorCode.EditPeriodClosed,
                $"The edit period for this plan ended at {editPolicy.GetEditableUntilUtc(plan.CreatedAtUtc, plan.Target):u}.");
        }

        dbContext.ConsumptionPlans.Remove(plan);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConsumptionPlanError.StaleVersion();
        }

        return ConsumptionPlanResult<bool>.Success(true);
    }
}
