using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;

public sealed class UpdatePlanHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    PlanEditPolicy editPolicy,
    ConsumptionPlanReadModel readModel)
{
    public async Task<ConsumptionPlanResult<ConsumptionPlanDto>> HandleAsync(
        int id,
        UpdateConsumptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ConsumptionPlan.ValidatePlannedQuantity(request.PlannedQuantity) is { } quantityError)
        {
            return ConsumptionPlanError.Validation(quantityError);
        }

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConsumptionPlan? plan = await dbContext.ConsumptionPlans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
        {
            return ConsumptionPlanError.NotFound(id);
        }

        if (plan.Version != request.Version)
        {
            return ConsumptionPlanError.StaleVersion();
        }

        if (plan.IsOrphaned)
        {
            return new ConsumptionPlanError(
                ConsumptionPlanErrorCode.PlanOrphaned,
                $"Stock record {plan.Key} is no longer in the current stock report, so the plan is read-only.");
        }

        if (!editPolicy.IsWithinEditPeriod(plan.CreatedAtUtc, plan.Target))
        {
            return new ConsumptionPlanError(
                ConsumptionPlanErrorCode.EditPeriodClosed,
                $"The edit period for this plan ended at {editPolicy.GetEditableUntilUtc(plan.CreatedAtUtc, plan.Target):u}.");
        }

        ConsumptionPlanResult<ActiveStockRow> stock = await dbContext.FindActiveStockRowAsync(plan.Key, cancellationToken);
        if (stock.Error is not null)
        {
            return stock.Error;
        }

        if (ActiveStockQueries.EnsureWithinStock(request.PlannedQuantity, stock.Value!) is { } stockError)
        {
            return stockError;
        }

        plan.UpdateQuantity(
            request.PlannedQuantity,
            stock.Value!.MaterialNumber,
            stock.Value.MaterialDescription,
            editPolicy.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConsumptionPlanError.StaleVersion();
        }

        return ConsumptionPlanResult<ConsumptionPlanDto>.Success(readModel.ToDto(plan, stock.Value.MaterialQuantity));
    }
}
