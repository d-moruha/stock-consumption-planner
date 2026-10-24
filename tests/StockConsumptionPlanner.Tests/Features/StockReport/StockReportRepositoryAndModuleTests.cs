using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportRepositoryAndModuleTests
{
    [Fact]
    public async Task InMemoryRepository_SaveGetAndClear_WorksAndComputesTotals()
    {
        InMemoryStockReportRepository repository = new();

        Assert.Null(await repository.GetCurrentAsync());

        StockReportBatch batch = new(
            "stock.xlsx",
            "Sheet1",
            DateTimeOffset.UtcNow,
            [
                new StockReportItem(2, "000100", "0001", 2026, "M-1", "Item 1", 5.5m, 100.25m),
                new StockReportItem(3, "000101", "0002", 2026, "M-2", "Item 2", 4.5m, 299.75m)
            ]);

        await repository.SaveCurrentAsync(batch);

        StockReportBatch? loaded = await repository.GetCurrentAsync();
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.TotalRows);
        Assert.Equal(10.0m, loaded.TotalQuantity);
        Assert.Equal(400.00m, loaded.TotalValue);

        await repository.ClearAsync();
        Assert.Null(await repository.GetCurrentAsync());
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

        IDbContextFactory<StockReportDbContext> factory = provider.GetRequiredService<IDbContextFactory<StockReportDbContext>>();
        using StockReportDbContext dbContext = factory.CreateDbContext();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void AddStockReportFeature_UnsupportedProvider_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "UnknownDb"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => services.AddStockReportFeature(configuration));
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
