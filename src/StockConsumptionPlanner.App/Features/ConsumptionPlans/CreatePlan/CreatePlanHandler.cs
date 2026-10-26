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
            return ConsumptionPlanError.Validation(
                $"Target month must be a month 1–12 of a year {PlanTargetMonth.MinYear}–{PlanTargetMonth.MaxYear}.");
        }

        if (ConsumptionPlan.ValidatePlannedQuantity(request.PlannedQuantity) is { } quantityError)
        {
            return ConsumptionPlanError.Validation(quantityError);
        }

        if (!editPolicy.CanCreate(target))
        {
            return new ConsumptionPlanError(
                ConsumptionPlanErrorCode.TargetMonthInPast,
                $"Plans cannot be created for {target}, which has already ended.");
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

        if (await PlanExistsAsync(dbContext, key, target, cancellationToken))
        {
            return DuplicatePlan(key, target);
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
            if (await PlanExistsAsync(dbContext, key, target, cancellationToken))
            {
                return DuplicatePlan(key, target);
            }

            throw;
        }

        return ConsumptionPlanResult<ConsumptionPlanDto>.Success(readModel.ToDto(plan, stock.Value.MaterialQuantity));
    }

    private static Task<bool> PlanExistsAsync(
        PlannerDbContext dbContext,
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

    private static ConsumptionPlanError DuplicatePlan(StockDocumentKey key, PlanTargetMonth target)
    {
        return new ConsumptionPlanError(
            ConsumptionPlanErrorCode.DuplicatePlan,
            $"A plan for stock record {key} in {target} already exists.");
    }
}
