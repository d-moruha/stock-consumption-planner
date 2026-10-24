using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.Modules.StockReport.Application;
using StockConsumptionPlanner.Modules.StockReport.Contracts;
using StockConsumptionPlanner.Modules.StockReport.Domain;
using StockConsumptionPlanner.Modules.StockReport.Infrastructure;

namespace StockConsumptionPlanner.Modules.StockReport.Tests;

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

    [Theory]
    [InlineData("Sqlite")]
    [InlineData("Postgres")]
    public void AddStockReportModule_ConfiguredFutureProviders_ThrowsNotSupportedException(string provider)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = provider
            })
            .Build();

        Assert.Throws<NotSupportedException>(() => services.AddStockReportModule(configuration));
    }
}
