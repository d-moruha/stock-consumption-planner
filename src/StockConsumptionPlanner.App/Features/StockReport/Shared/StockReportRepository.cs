using Microsoft.EntityFrameworkCore;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public interface IStockReportRepository
{
    Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default);

    Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

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

public sealed class EfCoreStockReportRepository(IDbContextFactory<StockReportDbContext> dbContextFactory)
    : IStockReportRepository
{
    public async Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await using StockReportDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<StockReportBatchEntity> existingBatches = await dbContext.StockReportBatches
            .ToListAsync(cancellationToken);

        if (existingBatches.Count > 0)
        {
            dbContext.StockReportBatches.RemoveRange(existingBatches);
        }

        StockReportBatchEntity entity = new()
        {
            SourceFileName = batch.SourceFileName,
            SheetName = batch.SheetName,
            ImportedAtUtc = batch.ImportedAtUtc,
            Items = batch.Items
                .Select(item => new StockReportItemEntity
                {
                    SourceRowNumber = item.SourceRowNumber,
                    MaterialDocumentNumber = item.MaterialDocumentNumber,
                    MaterialDocumentItem = item.MaterialDocumentItem,
                    MaterialDocumentYear = item.MaterialDocumentYear,
                    MaterialNumber = item.MaterialNumber,
                    MaterialDescription = item.MaterialDescription,
                    MaterialQuantity = item.MaterialQuantity,
                    MaterialValue = item.MaterialValue
                })
                .ToList()
        };

        dbContext.StockReportBatches.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using StockReportDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        StockReportBatchEntity? entity = await dbContext.StockReportBatches
            .AsNoTracking()
            .Include(b => b.Items)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        List<StockReportItem> items = entity.Items
            .OrderBy(i => i.SourceRowNumber)
            .ThenBy(i => i.Id)
            .Select(i => new StockReportItem(
                i.SourceRowNumber,
                i.MaterialDocumentNumber,
                i.MaterialDocumentItem,
                i.MaterialDocumentYear,
                i.MaterialNumber,
                i.MaterialDescription,
                i.MaterialQuantity,
                i.MaterialValue))
            .ToList();

        return new StockReportBatch(
            entity.SourceFileName,
            entity.SheetName,
            entity.ImportedAtUtc,
            items);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using StockReportDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<StockReportBatchEntity> existingBatches = await dbContext.StockReportBatches
            .ToListAsync(cancellationToken);

        if (existingBatches.Count > 0)
        {
            dbContext.StockReportBatches.RemoveRange(existingBatches);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
