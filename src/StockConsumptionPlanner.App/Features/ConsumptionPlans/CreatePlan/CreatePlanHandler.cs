using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;

public sealed class CreatePlanHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    PlanEditPolicy editPolicy,
    ConsumptionPlanReadModel readModel)
{
    public async Task<ConsumptionPlanResult<ConsumptionPlanDto>> HandleAsync(
        CreateConsumptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        StockDocumentKey key = StockDocumentKey.Create(
            request.MaterialDocumentYear,
            request.MaterialDocumentNumber,
            request.MaterialDocumentItem);
        if (!key.IsComplete)
        {
            return ConsumptionPlanError.Validation(
                "Material document year, number, and item are required to identify the stock record.");
        }

        PlanTargetMonth target = new(request.TargetYear, request.TargetMonth);
        if (!target.IsValid)
        {
            return ConsumptionPlanError.InvalidTarget();
        }

        if (ConsumptionPlan.ValidatePlannedQuantity(request.PlannedQuantity) is { } quantityError)
        {
            return ConsumptionPlanError.Validation(quantityError);
        }

        if (!editPolicy.CanCreate(target))
        {
            return ConsumptionPlanError.TargetMonthInPast(target);
        }

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        ConsumptionPlanResult<ActiveStockRow> stock = await dbContext.FindActiveStockRowAsync(key, cancellationToken);
        if (stock.Error is not null)
        {
            return stock.Error;
        }

        if (ActiveStockQueries.EnsureWithinStock(request.PlannedQuantity, stock.Value!) is { } stockError)
        {
            return stockError;
        }

        if (await dbContext.PlanExistsAsync(key, target, cancellationToken))
        {
            return ConsumptionPlanError.DuplicatePlan(key, target);
        }

        ConsumptionPlan plan = ConsumptionPlan.Create(
            key,
            target,
            request.PlannedQuantity,
            stock.Value!.MaterialNumber,
            stock.Value.MaterialDescription,
            editPolicy.UtcNow);

        dbContext.ConsumptionPlans.Add(plan);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.PlanExistsAsync(key, target, cancellationToken))
            {
                return ConsumptionPlanError.DuplicatePlan(key, target);
            }

            throw;
        }

        return ConsumptionPlanResult<ConsumptionPlanDto>.Success(readModel.ToDto(plan, stock.Value.TotalStockQuantity));
    }
}
