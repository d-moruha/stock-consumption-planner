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

        if (!Enum.IsDefined(query.Sort))
        {
            return ConsumptionPlanError.Validation($"Unknown sort '{query.Sort}'.");
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

        List<StockWithPlan> rows = await Order(stock, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                dbContext.ConsumptionPlans.AsNoTracking(),
                i => new { i.MaterialDocumentYear, i.MaterialDocumentNumber, i.MaterialDocumentItem },
                p => new { p.MaterialDocumentYear, p.MaterialDocumentNumber, p.MaterialDocumentItem },
                (i, plans) => new { Item = i, Plans = plans })
            .SelectMany(
                x => x.Plans.DefaultIfEmpty(),
                (x, plan) => new StockWithPlan(x.Item, plan))
            .ToListAsync(cancellationToken);

        ILookup<int, ConsumptionPlan?> plansByItem = rows.ToLookup(r => r.Item.Id, r => r.Plan);
        IQueryable<StockReportItemEntity> pageItems = rows
            .Select(r => r.Item)
            .DistinctBy(i => i.Id)
            .AsQueryable();
        DateOnly today = editPolicy.Today;
        List<PlanningSheetRowDto> items = Order(pageItems, query.Sort)
            .AsEnumerable()
            .Select(item => ToRow(item, plansByItem[item.Id], target, today))
            .ToList();

        return ConsumptionPlanResult<PlanningSheetDto>.Success(new PlanningSheetDto(
            target.Year,
            target.Month,
            editPolicy.CanCreate(target),
            editPolicy.GetMonthStartUtc(target),
            editPolicy.GetLockBeforeMonthUtc(target),
            new PagedResult<PlanningSheetRowDto>(items, totalCount, page, pageSize)));
    }

    private static IOrderedQueryable<StockReportItemEntity> Order(
        IQueryable<StockReportItemEntity> stock,
        PlanningSheetSort sort)
    {
        return sort switch
        {
            PlanningSheetSort.StockAgeAscending => stock
                .OrderBy(i => i.StockCreationDate == null)
                .ThenByDescending(i => i.StockCreationDate)
                .ThenBy(i => i.SourceRowNumber)
                .ThenBy(i => i.Id),
            PlanningSheetSort.StockAgeDescending => stock
                .OrderBy(i => i.StockCreationDate == null)
                .ThenBy(i => i.StockCreationDate)
                .ThenBy(i => i.SourceRowNumber)
                .ThenBy(i => i.Id),
            // SQLite's decimal ORDER BY uses the current culture and throws for uk-UA.
            PlanningSheetSort.StockValueAscending => stock
                .OrderBy(i => i.TotalStockValue == null)
                .ThenBy(i => (long?)(i.TotalStockValue * 100m))
                .ThenBy(i => i.SourceRowNumber)
                .ThenBy(i => i.Id),
            PlanningSheetSort.StockValueDescending => stock
                .OrderBy(i => i.TotalStockValue == null)
                .ThenByDescending(i => (long?)(i.TotalStockValue * 100m))
                .ThenBy(i => i.SourceRowNumber)
                .ThenBy(i => i.Id),
            _ => stock
                .OrderBy(i => i.SourceRowNumber)
                .ThenBy(i => i.Id)
        };
    }

    private PlanningSheetRowDto ToRow(
        StockReportItemEntity item,
        IEnumerable<ConsumptionPlan?> plans,
        PlanTargetMonth target,
        DateOnly today)
    {
        List<ConsumptionPlanDto> chronological = plans
            .OfType<ConsumptionPlan>()
            .OrderBy(p => p.TargetYear)
            .ThenBy(p => p.TargetMonth)
            .Select(p => readModel.ToDto(p, item.TotalStockQuantity))
            .ToList();

        decimal plannedQuantity = chronological.Sum(p => p.PlannedQuantity);
        decimal? unplannedQuantity = item.TotalStockQuantity - plannedQuantity;

        return new PlanningSheetRowDto(
            item.SourceRowNumber,
            item.MaterialDocumentYear,
            item.MaterialDocumentNumber,
            item.MaterialDocumentItem,
            item.MaterialNumber,
            item.MaterialDescription,
            item.TotalStockQuantity,
            unplannedQuantity,
            item.TotalStockValue,
            chronological.FirstOrDefault(
                p => p.TargetYear == target.Year && p.TargetMonth == target.Month),
            chronological,
            item.GoodsReceiptDate,
            item.StockCreationDate,
            item.StockCreationDate is { } created ? today.DayNumber - created.DayNumber : null);
    }

    private sealed record StockWithPlan(StockReportItemEntity Item, ConsumptionPlan? Plan);
}
