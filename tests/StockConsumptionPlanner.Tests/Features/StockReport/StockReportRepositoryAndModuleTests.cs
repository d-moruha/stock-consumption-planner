using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportRepositoryAndModuleTests
{
    [Theory]
    [InlineData("UnknownDb")]
    [InlineData("InMemory")]
    public void AddStockReportFeature_UnsupportedProvider_ThrowsInvalidOperationException(string provider)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = provider
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => services.AddStockReportFeature(configuration));
    }

    [Fact]
    public async Task EfCoreSqliteRepository_SaveReplaceRetrieveAndClear_PersistsAcrossDbContexts()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-sqlite-repo-{Guid.NewGuid():N}.db");

        try
        {
            ServiceCollection services = new();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False"
                })
                .Build();

            services.AddStockReportFeature(configuration);

            await using ServiceProvider provider = services.BuildServiceProvider();

            foreach (IHostedService hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            IStockReportRepository repository = provider.GetRequiredService<IStockReportRepository>();
            Assert.IsType<EfCoreStockReportRepository>(repository);

            Assert.Null(await repository.GetCurrentAsync());

            StockReportBatch firstBatch = new(
                "first_report.xlsx",
                "Залишки",
                new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero),
                [
                    new StockReportItem(5, "0049001000", "0001", 2026, "0000100", "Перший матеріал", 12.500m, 1500.75m),
                    new StockReportItem(6, "0049001001", "0002", 2025, "0000200", "Другий матеріал", 7.250m, 499.25m)
                ]);

            await repository.SaveCurrentAsync(firstBatch);

            StockReportBatch? loadedFirst = await repository.GetCurrentAsync();
            Assert.NotNull(loadedFirst);
            Assert.Equal("first_report.xlsx", loadedFirst.SourceFileName);
            Assert.Equal("Залишки", loadedFirst.SheetName);
            Assert.Equal(StockLifecycleStatus.Active, loadedFirst.LifecycleStatus);
            Assert.Equal(2, loadedFirst.TotalRows);
            Assert.Equal(19.750m, loadedFirst.TotalQuantity);
            Assert.Equal(2000.00m, loadedFirst.TotalValue);
            Assert.Equal("0049001000", loadedFirst.Items[0].MaterialDocumentNumber);
            Assert.Equal("0001", loadedFirst.Items[0].MaterialDocumentItem);

            StockReportBatch replacementBatch = new(
                "second_report.xlsx",
                "Sheet2",
                new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero),
                [
                    new StockReportItem(2, "0050009999", "0010", 2026, "MAT-999", "Replacement item", 3.000m, 300.00m)
                ]);

            await repository.SaveCurrentAsync(replacementBatch);

            StockReportBatch? loadedReplacement = await repository.GetCurrentAsync();
            Assert.NotNull(loadedReplacement);
            Assert.Equal("second_report.xlsx", loadedReplacement.SourceFileName);
            Assert.Single(loadedReplacement.Items);
            Assert.Equal("0050009999", loadedReplacement.Items[0].MaterialDocumentNumber);

            await repository.ClearAsync();
            Assert.Null(await repository.GetCurrentAsync());
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task EfCoreSqliteRepository_LifecycleTransitions_StagingToActiveToSupersededAndPurged()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-sqlite-lifecycle-{Guid.NewGuid():N}.db");

        try
        {
            ServiceCollection services = new();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False"
                })
                .Build();

            services.AddStockReportFeature(configuration);

            await using ServiceProvider provider = services.BuildServiceProvider();
            foreach (IHostedService hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            EfCoreStockReportRepository repository = Assert.IsType<EfCoreStockReportRepository>(
                provider.GetRequiredService<IStockReportRepository>());
            IDbContextFactory<PlannerDbContext> dbFactory =
                provider.GetRequiredService<IDbContextFactory<PlannerDbContext>>();

            StockReportBatch report1 = new(
                "report1.xlsx",
                "Sheet1",
                DateTimeOffset.UtcNow,
                [
                    new StockReportItem(2, "0049000001", "0001", 2026, "MAT-1", "Item 1", 10m, 100m),
                    new StockReportItem(3, "0049000002", "0001", 2026, "MAT-2", "Item 2", 20m, 200m)
                ]);

            // Stage Report 1 without promoting yet: should be Staging and invisible to GetCurrentAsync.
            int report1BatchId = await repository.StageBatchInChunksAsync(report1, chunkSize: 1);
            Assert.Null(await repository.GetCurrentAsync());

            await using (PlannerDbContext db = await dbFactory.CreateDbContextAsync())
            {
                StockReportBatchEntity stagedBatch = await db.StockReportBatches.SingleAsync(b => b.Id == report1BatchId);
                Assert.Equal(StockLifecycleStatus.Staging, stagedBatch.LifecycleStatus);

                List<StockReportItemEntity> stagedItems = await db.StockReportItems
                    .Where(i => i.BatchId == report1BatchId)
                    .ToListAsync();
                Assert.Equal(2, stagedItems.Count);
                Assert.All(stagedItems, i => Assert.Equal(StockLifecycleStatus.Staging, i.LifecycleStatus));
            }

            // Promote Report 1 to Active: now visible to GetCurrentAsync.
            await repository.PromoteStagedBatchAsync(report1BatchId);
            StockReportBatch? activeReport1 = await repository.GetCurrentAsync();
            Assert.NotNull(activeReport1);
            Assert.Equal("report1.xlsx", activeReport1.SourceFileName);
            Assert.Equal(2, activeReport1.TotalRows);

            // Import Report 2 with purgeSupersededImmediately = false to inspect Superseded state before deletion.
            StockReportBatch report2 = new(
                "report2.xlsx",
                "Sheet1",
                DateTimeOffset.UtcNow,
                [
                    new StockReportItem(2, "0049000099", "0001", 2026, "MAT-99", "New Item", 50m, 500m)
                ]);

            int report2BatchId = await repository.SaveCurrentAsync(
                report2,
                purgeSupersededImmediately: false,
                chunkSize: 1);

            await using (PlannerDbContext db = await dbFactory.CreateDbContextAsync())
            {
                StockReportBatchEntity oldBatch = await db.StockReportBatches.SingleAsync(b => b.Id == report1BatchId);
                StockReportBatchEntity newBatch = await db.StockReportBatches.SingleAsync(b => b.Id == report2BatchId);
                Assert.Equal(StockLifecycleStatus.Superseded, oldBatch.LifecycleStatus);
                Assert.Equal(StockLifecycleStatus.Active, newBatch.LifecycleStatus);

                List<StockReportItemEntity> oldItems = await db.StockReportItems
                    .Where(i => i.BatchId == report1BatchId)
                    .ToListAsync();
                Assert.All(oldItems, i => Assert.Equal(StockLifecycleStatus.Superseded, i.LifecycleStatus));
            }

            // GetCurrentAsync must return only Report 2 (Active).
            StockReportBatch? activeReport2 = await repository.GetCurrentAsync();
            Assert.NotNull(activeReport2);
            Assert.Equal("report2.xlsx", activeReport2.SourceFileName);
            Assert.Single(activeReport2.Items);

            // Purge Superseded batches via ExecuteDeleteAsync.
            int deletedBatches = await repository.PurgeNonActiveBatchesAsync();
            Assert.Equal(1, deletedBatches);

            await using (PlannerDbContext db = await dbFactory.CreateDbContextAsync())
            {
                Assert.Equal(1, await db.StockReportBatches.CountAsync());
                Assert.Equal(1, await db.StockReportItems.CountAsync());
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task EfCoreSqliteRepository_Large2500RowBatchImport_StagesInChunksPromotesAndReplacesPreviousReport()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-sqlite-2500-{Guid.NewGuid():N}.db");

        try
        {
            ServiceCollection services = new();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False"
                })
                .Build();

            services.AddStockReportFeature(configuration);

            await using ServiceProvider provider = services.BuildServiceProvider();
            foreach (IHostedService hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            IStockReportRepository repository = provider.GetRequiredService<IStockReportRepository>();
            IDbContextFactory<PlannerDbContext> dbFactory =
                provider.GetRequiredService<IDbContextFactory<PlannerDbContext>>();

            List<StockReportItem> initialItems = Enumerable.Range(1, 2000)
                .Select(idx => new StockReportItem(
                    idx + 1,
                    $"4900{idx:D6}",
                    "0001",
                    2025,
                    $"MAT-{idx:D5}",
                    $"Initial Material {idx}",
                    1.5m,
                    15.0m))
                .ToList();

            await repository.SaveCurrentAsync(new StockReportBatch(
                "initial_2000.xlsx",
                "Залишки",
                DateTimeOffset.UtcNow,
                initialItems));

            List<StockReportItem> secondItems = Enumerable.Range(1, 2500)
                .Select(idx => new StockReportItem(
                    idx + 1,
                    $"5000{idx:D6}",
                    "0002",
                    2026,
                    $"MAT-NEW-{idx:D5}",
                    $"Updated Material {idx}",
                    2.0m,
                    25.0m))
                .ToList();

            await repository.SaveCurrentAsync(new StockReportBatch(
                "second_2500.xlsx",
                "Залишки",
                DateTimeOffset.UtcNow,
                secondItems));

            StockReportBatch? current = await repository.GetCurrentAsync();
            Assert.NotNull(current);
            Assert.Equal("second_2500.xlsx", current.SourceFileName);
            Assert.Equal(2500, current.TotalRows);
            Assert.Equal(5000.0m, current.TotalQuantity);
            Assert.Equal(62500.0m, current.TotalValue);
            Assert.Equal("5000000001", current.Items[0].MaterialDocumentNumber);
            Assert.Equal("5000002500", current.Items[^1].MaterialDocumentNumber);

            await using (PlannerDbContext db = await dbFactory.CreateDbContextAsync())
            {
                Assert.Equal(1, await db.StockReportBatches.CountAsync());
                Assert.Equal(2500, await db.StockReportItems.CountAsync());
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqlitePersistence_SurvivesAcrossSeparateHostServiceProvidersAndSupportsPurgeOfStaleStaging()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-sqlite-restart-{Guid.NewGuid():N}.db");

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False"
                })
                .Build();

            // Host instance 1: initialize schema, import an Active batch, and leave an interrupted Staging batch.
            {
                ServiceCollection services1 = new();
                services1.AddStockReportFeature(configuration);

                await using ServiceProvider provider1 = services1.BuildServiceProvider();
                foreach (IHostedService hostedService in provider1.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                EfCoreStockReportRepository repo1 = Assert.IsType<EfCoreStockReportRepository>(
                    provider1.GetRequiredService<IStockReportRepository>());

                await repo1.SaveCurrentAsync(new StockReportBatch(
                    "persisted_across_restart.xlsx",
                    "Залишки",
                    DateTimeOffset.UtcNow,
                    [
                        new StockReportItem(2, "0049007777", "0001", 2026, "MAT-777", "Persisted Material", 15.5m, 1550.0m)
                    ]));

                // Simulate an interrupted import that stayed in Staging.
                await repo1.StageBatchInChunksAsync(new StockReportBatch(
                    "interrupted_staging.xlsx",
                    "Залишки",
                    DateTimeOffset.UtcNow,
                    [
                        new StockReportItem(2, "0049008888", "0001", 2026, "MAT-888", "Unfinished Staging", 99.0m, 9900.0m)
                    ]));
            }

            // Host instance 2: connect to the same SQLite DB file, verify only Active batch is returned, and purge stale Staging.
            {
                ServiceCollection services2 = new();
                services2.AddStockReportFeature(configuration);

                await using ServiceProvider provider2 = services2.BuildServiceProvider();
                foreach (IHostedService hostedService in provider2.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                }

                EfCoreStockReportRepository repo2 = Assert.IsType<EfCoreStockReportRepository>(
                    provider2.GetRequiredService<IStockReportRepository>());

                StockReportBatch? active = await repo2.GetCurrentAsync();
                Assert.NotNull(active);
                Assert.Equal("persisted_across_restart.xlsx", active.SourceFileName);
                Assert.Single(active.Items);
                Assert.Equal("0049007777", active.Items[0].MaterialDocumentNumber);

                int purgedCount = await repo2.PurgeNonActiveBatchesAsync();
                Assert.Equal(1, purgedCount);

                IDbContextFactory<PlannerDbContext> dbFactory2 =
                    provider2.GetRequiredService<IDbContextFactory<PlannerDbContext>>();
                await using PlannerDbContext db = await dbFactory2.CreateDbContextAsync();
                Assert.Equal(1, await db.StockReportBatches.CountAsync());
                Assert.Equal(1, await db.StockReportItems.CountAsync());
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void AddStockReportFeature_PostgresProvider_RegistersEfCoreRepositoryWithNpgsql()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=stock_planner;Username=postgres;Password=postgres"
            })
            .Build();

        services.AddStockReportFeature(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IStockReportRepository repository = provider.GetRequiredService<IStockReportRepository>();
        Assert.IsType<EfCoreStockReportRepository>(repository);

        IDbContextFactory<PlannerDbContext> factory = provider.GetRequiredService<IDbContextFactory<PlannerDbContext>>();
        using PlannerDbContext dbContext = factory.CreateDbContext();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Theory]
    [InlineData("MaterialDocumentYear", StockReportField.MaterialDocumentYear)]
    [InlineData("MaterialDccoumentYear", StockReportField.MaterialDocumentYear)]
    [InlineData("Рік документа", StockReportField.MaterialDocumentYear)]
    [InlineData("Документ матеріалу", StockReportField.MaterialDocumentNumber)]
    [InlineData("Позиція", StockReportField.MaterialDocumentItem)]
    [InlineData("Номенклатура", StockReportField.MaterialNumber)]
    [InlineData("Найменування", StockReportField.MaterialDescription)]
    [InlineData("Кількість", StockReportField.MaterialQuantity)]
    [InlineData("Вартість", StockReportField.MaterialValue)]
    public void TryParseFieldKey_RecognizesCanonicalTypoAndUkrainianKeys(string rawKey, StockReportField expected)
    {
        bool parsed = StockReportFieldCatalog.TryParseFieldKey(rawKey, out StockReportField actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }
}
