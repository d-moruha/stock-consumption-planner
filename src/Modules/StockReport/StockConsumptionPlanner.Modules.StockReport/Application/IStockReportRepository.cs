using StockConsumptionPlanner.Modules.StockReport.Domain;

namespace StockConsumptionPlanner.Modules.StockReport.Application;

public interface IStockReportRepository
{
    Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default);

    Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
