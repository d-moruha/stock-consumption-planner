using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;

public sealed class GetPlansHandler(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    ConsumptionPlanReadModel readModel)
{
    public async Task<PagedResult<ConsumptionPlanDto>> ListAsync(
        GetPlansQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        (int page, int pageSize) = PagedResult<ConsumptionPlanDto>.Normalize(query.Page, query.PageSize);

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<ConsumptionPlan> plans = dbContext.ConsumptionPlans.AsNoTracking();

        if (query.TargetYear is int year)
        {
            plans = plans.Where(p => p.TargetYear == year);
        }

        if (query.TargetMonth is int month)
        {
            plans = plans.Where(p => p.TargetMonth == month);
        }

        plans = query.Status switch
        {
            PlanLinkFilter.Linked => plans.Where(p => p.OrphanedAtUtc == null),
            PlanLinkFilter.Orphaned => plans.Where(p => p.OrphanedAtUtc != null),
            _ => plans
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            plans = plans.Where(p =>
                p.MaterialNumber.Contains(search) ||
                p.MaterialDescription.Contains(search) ||
                p.MaterialDocumentNumber.Contains(search));
        }

        int totalCount = await plans.CountAsync(cancellationToken);

        List<PlanWithStock> rows = await WithCurrentStock(dbContext, plans
                .OrderBy(p => p.TargetYear)
                .ThenBy(p => p.TargetMonth)
                .ThenBy(p => p.MaterialDocumentYear)
                .ThenBy(p => p.MaterialDocumentNumber)
                .ThenBy(p => p.MaterialDocumentItem)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<ConsumptionPlanDto>(
            rows.Select(r => readModel.ToDto(r.Plan, r.CurrentStockQuantity)).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<ConsumptionPlanDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        PlanWithStock? row = await WithCurrentStock(dbContext, dbContext.ConsumptionPlans.AsNoTracking().Where(p => p.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : readModel.ToDto(row.Plan, row.CurrentStockQuantity);
    }

    private static IQueryable<PlanWithStock> WithCurrentStock(PlannerDbContext dbContext, IQueryable<ConsumptionPlan> plans)
    {
        IQueryable<StockReportItemEntity> activeStock = dbContext.ActiveStockRows();
        return plans.Select(p => new PlanWithStock(
            p,
            activeStock
                .Where(i => i.MaterialDocumentYear == p.MaterialDocumentYear &&
                            i.MaterialDocumentNumber == p.MaterialDocumentNumber &&
                            i.MaterialDocumentItem == p.MaterialDocumentItem)
                .OrderBy(i => i.Id)
                .Select(i => (decimal?)i.MaterialQuantity)
                .FirstOrDefault()));
    }

    private sealed record PlanWithStock(ConsumptionPlan Plan, decimal? CurrentStockQuantity);
}
