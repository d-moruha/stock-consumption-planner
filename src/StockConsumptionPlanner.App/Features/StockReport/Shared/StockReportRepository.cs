using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public interface IStockReportRepository
{
    Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default);

    Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class EfCoreStockReportRepository(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    IEnumerable<IStockPromotionParticipant> promotionParticipants)
    : IStockReportRepository
{
    public const int DefaultChunkSize = 2000;

    public Task SaveCurrentAsync(StockReportBatch batch, CancellationToken cancellationToken = default)
    {
        return SaveCurrentAsync(batch, purgeSupersededImmediately: true, DefaultChunkSize, cancellationToken);
    }

    public async Task<int> SaveCurrentAsync(
        StockReportBatch batch,
        bool purgeSupersededImmediately,
        int chunkSize = DefaultChunkSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        int effectiveChunkSize = Math.Max(1, chunkSize);

        int newBatchId = await StageBatchInChunksAsync(batch, effectiveChunkSize, cancellationToken);

        await PromoteStagedBatchAsync(newBatchId, cancellationToken);

        if (purgeSupersededImmediately)
        {
            await PurgeNonActiveBatchesAsync(newBatchId, cancellationToken);
        }

        return newBatchId;
    }

    public async Task<int> StageBatchInChunksAsync(
        StockReportBatch batch,
        int chunkSize = DefaultChunkSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        int effectiveChunkSize = Math.Max(1, chunkSize);

        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        StockReportBatchEntity batchEntity = new()
        {
            SourceFileName = batch.SourceFileName,
            SheetName = batch.SheetName,
            ImportedAtUtc = batch.ImportedAtUtc,
            LifecycleStatus = StockLifecycleStatus.Staging
        };

        dbContext.StockReportBatches.Add(batchEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        int newBatchId = batchEntity.Id;
        dbContext.ChangeTracker.Clear();

        if (batch.Items.Count > 0)
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                foreach (StockReportItem[] chunk in batch.Items.Chunk(effectiveChunkSize))
                {
                    List<StockReportItemEntity> chunkEntities = new(chunk.Length);
                    foreach (StockReportItem item in chunk)
                    {
                        chunkEntities.Add(new StockReportItemEntity
                        {
                            BatchId = newBatchId,
                            SourceRowNumber = item.SourceRowNumber,
                            MaterialDocumentNumber = item.MaterialDocumentNumber,
                            MaterialDocumentItem = item.MaterialDocumentItem,
                            MaterialDocumentYear = item.MaterialDocumentYear,
                            MaterialNumber = item.MaterialNumber,
                            MaterialDescription = item.MaterialDescription,
                            MaterialQuantity = item.MaterialQuantity,
                            MaterialValue = item.MaterialValue,
                            LifecycleStatus = StockLifecycleStatus.Staging
                        });
                    }

                    dbContext.StockReportItems.AddRange(chunkEntities);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                }
            }
            finally
            {
                dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        return newBatchId;
    }

    public async Task PromoteStagedBatchAsync(int stagedBatchId, CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.StockReportItems
            .Where(i => i.BatchId != stagedBatchId && i.LifecycleStatus == StockLifecycleStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.LifecycleStatus, StockLifecycleStatus.Superseded),
                cancellationToken);

        await dbContext.StockReportBatches
            .Where(b => b.Id != stagedBatchId && b.LifecycleStatus == StockLifecycleStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.LifecycleStatus, StockLifecycleStatus.Superseded),
                cancellationToken);

        await dbContext.StockReportItems
            .Where(i => i.BatchId == stagedBatchId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.LifecycleStatus, StockLifecycleStatus.Active),
                cancellationToken);

        await dbContext.StockReportBatches
            .Where(b => b.Id == stagedBatchId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.LifecycleStatus, StockLifecycleStatus.Active),
                cancellationToken);

        foreach (IStockPromotionParticipant participant in promotionParticipants)
        {
            await participant.OnStockPromotedAsync(dbContext, stagedBatchId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> PurgeNonActiveBatchesAsync(
        int? excludeBatchId = null,
        CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<StockReportBatchEntity> query = dbContext.StockReportBatches
            .Where(b => b.LifecycleStatus == StockLifecycleStatus.Superseded ||
                        b.LifecycleStatus == StockLifecycleStatus.Staging);

        if (excludeBatchId.HasValue)
        {
            int excludedId = excludeBatchId.Value;
            query = query.Where(b => b.Id != excludedId);
        }

        return await query.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<StockReportBatch?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        StockReportBatchEntity? batchEntity = await dbContext.StockReportBatches
            .AsNoTracking()
            .Where(b => b.LifecycleStatus == StockLifecycleStatus.Active)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (batchEntity is null)
        {
            return null;
        }

        List<StockReportItem> items = await dbContext.StockReportItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchEntity.Id && i.LifecycleStatus == StockLifecycleStatus.Active)
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
                i.MaterialValue,
                i.LifecycleStatus))
            .ToListAsync(cancellationToken);

        return new StockReportBatch(
            batchEntity.SourceFileName,
            batchEntity.SheetName,
            batchEntity.ImportedAtUtc,
            items,
            batchEntity.LifecycleStatus);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.StockReportBatches.ExecuteDeleteAsync(cancellationToken);
    }
}
