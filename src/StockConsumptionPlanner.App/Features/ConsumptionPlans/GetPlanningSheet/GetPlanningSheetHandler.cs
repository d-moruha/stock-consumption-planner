using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;

/// <summary>Active stock rows of the current report, each with its plan for the requested month (if any).</summary>
public sealed class GetPlanningSheetHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    PlanEditPolicy editPolicy,
    ConsumptionPlanReadModel readModel)
{
    public async Task<ConsumptionPlanResult<PlanningSheetDto>> HandleAsync(
        PlanningSheetQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PlanTargetMonth target = new(query.TargetYear, query.TargetMonth);
        if (!target.IsValid)
        {
            return ConsumptionPlanError.Validation(
                $"Target month must be a month 1–12 of a year {PlanTargetMonth.MinYear}–{PlanTargetMonth.MaxYear}.");
        }

        (int page, int pageSize) = PagedResult<PlanningSheetRowDto>.Normalize(query.Page, query.PageSize);

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<StockReportItemEntity> stock = dbContext.ActiveStockRows().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            stock = stock.Where(i =>
                i.MaterialNumber.Contains(search) ||
                i.MaterialDescription.Contains(search) ||
                i.MaterialDocumentNumber.Contains(search));
        }

        int totalCount = await stock.CountAsync(cancellationToken);

        IQueryable<ConsumptionPlan> monthPlans = dbContext.ConsumptionPlans
            .AsNoTracking()
            .Where(p => p.TargetYear == target.Year && p.TargetMonth == target.Month);

        List<StockWithPlan> rows = await stock
            .OrderBy(i => i.SourceRowNumber)
            .ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                monthPlans,
                i => new { i.MaterialDocumentYear, i.MaterialDocumentNumber, i.MaterialDocumentItem },
                p => new { p.MaterialDocumentYear, p.MaterialDocumentNumber, p.MaterialDocumentItem },
                (i, plans) => new { Item = i, Plans = plans })
            .SelectMany(
                x => x.Plans.DefaultIfEmpty(),
                (x, plan) => new StockWithPlan(x.Item, plan))
            .ToListAsync(cancellationToken);

        List<PlanningSheetRowDto> items = rows
            .OrderBy(r => r.Item.SourceRowNumber)
            .ThenBy(r => r.Item.Id)
            .Select(r => new PlanningSheetRowDto(
                r.Item.SourceRowNumber,
                r.Item.MaterialDocumentYear,
                r.Item.MaterialDocumentNumber,
                r.Item.MaterialDocumentItem,
                r.Item.MaterialNumber,
                r.Item.MaterialDescription,
                r.Item.MaterialQuantity,
                r.Plan is null ? null : readModel.ToDto(r.Plan, r.Item.MaterialQuantity)))
            .ToList();

        return ConsumptionPlanResult<PlanningSheetDto>.Success(new PlanningSheetDto(
            target.Year,
            target.Month,
            editPolicy.CanCreate(target),
            editPolicy.GetMonthStartUtc(target),
            editPolicy.GetLockBeforeMonthUtc(target),
            new PagedResult<PlanningSheetRowDto>(items, totalCount, page, pageSize)));
    }

    private sealed record StockWithPlan(StockReportItemEntity Item, ConsumptionPlan? Plan);
}
