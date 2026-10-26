using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed record ActiveStockRow(decimal MaterialQuantity, string MaterialNumber, string MaterialDescription);

public static class ActiveStockQueries
{
    public static IQueryable<StockReportItemEntity> ActiveStockRows(this PlannerDbContext dbContext)
    {
        return dbContext.StockReportItems.Where(i => i.LifecycleStatus == StockLifecycleStatus.Active);
    }

    /// <summary>
    /// Resolves the single Active stock row for <paramref name="key"/>, or an error when the key is
    /// missing or matches more than one row in the current report.
    /// </summary>
    public static async Task<ConsumptionPlanResult<ActiveStockRow>> FindActiveStockRowAsync(
        this PlannerDbContext dbContext,
        StockDocumentKey key,
        CancellationToken cancellationToken)
    {
        List<ActiveStockRow> rows = await dbContext.ActiveStockRows()
            .Where(i => i.MaterialDocumentYear == key.MaterialDocumentYear &&
                        i.MaterialDocumentNumber == key.MaterialDocumentNumber &&
                        i.MaterialDocumentItem == key.MaterialDocumentItem)
            .OrderBy(i => i.Id)
            .Select(i => new ActiveStockRow(i.MaterialQuantity, i.MaterialNumber, i.MaterialDescription))
            .Take(2)
            .ToListAsync(cancellationToken);

        return rows.Count switch
        {
            0 => new ConsumptionPlanError(
                ConsumptionPlanErrorCode.StockNotActive,
                $"Stock record {key} is not in the current stock report."),
            1 => ConsumptionPlanResult<ActiveStockRow>.Success(rows[0]),
            _ => new ConsumptionPlanError(
                ConsumptionPlanErrorCode.AmbiguousStock,
                $"Stock record {key} appears more than once in the current stock report.")
        };
    }

    public static ConsumptionPlanError? EnsureWithinStock(decimal plannedQuantity, ActiveStockRow stock)
    {
        return plannedQuantity > stock.MaterialQuantity
            ? new ConsumptionPlanError(
                ConsumptionPlanErrorCode.QuantityExceedsStock,
                $"Planned quantity {FormatQuantity(plannedQuantity)} exceeds the current stock quantity {FormatQuantity(stock.MaterialQuantity)}.")
            : null;
    }

    private static string FormatQuantity(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
