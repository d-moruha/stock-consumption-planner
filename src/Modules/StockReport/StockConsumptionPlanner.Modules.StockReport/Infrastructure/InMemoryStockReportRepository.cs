using StockConsumptionPlanner.Modules.StockReport.Application;
using StockConsumptionPlanner.Modules.StockReport.Domain;

namespace StockConsumptionPlanner.Modules.StockReport.Infrastructure;

public sealed class InMemoryStockReportRepository : IStockReportRepository
{
    private readonly Lock _syncLock = new();
    private StockReportBatch? _currentBatch;

    public Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        lock (_syncLock)
        {
            _currentBatch = batch;
        }

        return Task.CompletedTask;
    }

    public Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            return Task.FromResult(_currentBatch);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            _currentBatch = null;
        }

        return Task.CompletedTask;
    }
}
