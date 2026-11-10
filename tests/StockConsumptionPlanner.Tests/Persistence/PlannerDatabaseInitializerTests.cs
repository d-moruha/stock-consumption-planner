using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;
using StockReportGenerator;

namespace StockConsumptionPlanner.Tests.Persistence;

public sealed class PlannerDatabaseInitializerTests
{
    [Fact]
    public Task Sqlite_OutdatedSchema_IsDroppedAndRecreated()
    {
        return AssertOutdatedSchemaIsRecreatedAsync(SqliteSettings());
    }

    [PostgresFact]
    public Task Postgres_OutdatedSchema_IsDroppedAndRecreated()
    {
        return AssertOutdatedSchemaIsRecreatedAsync(PostgresSettings());
    }

    [Fact]
    public Task Sqlite_UpToDateSchema_IsLeftUntouchedWithItsData()
    {
        return AssertUpToDateSchemaKeepsDataAsync(SqliteSettings());
    }

    [PostgresFact]
    public Task Postgres_UpToDateSchema_IsLeftUntouchedWithItsData()
    {
        return AssertUpToDateSchemaKeepsDataAsync(PostgresSettings());
    }

    [Fact]
    public Task Sqlite_OutdatedSchema_ImportsGeneratedReportAndLoadsConsumptionPlansPage()
    {
        return AssertGeneratedReportImportsAndPlansPageLoadsAsync(SqliteSettings());
    }

    [PostgresFact]
    public Task Postgres_OutdatedSchema_ImportsGeneratedReportAndLoadsConsumptionPlansPage()
    {
        return AssertGeneratedReportImportsAndPlansPageLoadsAsync(PostgresSettings());
    }

    private static async Task AssertOutdatedSchemaIsRecreatedAsync(
        Dictionary<string, string?> settings)
    {
        try
        {
            await CreateOutdatedDatabaseAsync(settings);

            await StartInitializerAsync(settings);

            await using ServiceProvider services = BuildServices(settings);
            await using PlannerDbContext dbContext = await CreateDbContextAsync(services);
            List<string?> units = await dbContext.StockReportItems
                .Select(i => i.BaseUnitOfMeasure)
                .ToListAsync();
            Assert.Empty(units);
            Assert.Single(await dbContext.Set<PlannerSchemaVersion>().ToListAsync());
        }
        finally
        {
            await DeleteDatabaseAsync(settings);
        }
    }

    private static async Task AssertUpToDateSchemaKeepsDataAsync(
        Dictionary<string, string?> settings)
    {
        try
        {
            await StartInitializerAsync(settings);
            await using (ServiceProvider services = BuildServices(settings))
            {
                StockReportItem item = new(2, "0049001234", "0001", 2026, "MAT-1", "Pipe", 5m, 50m);
                await services.GetRequiredService<IStockReportRepository>().SaveCurrentAsync(
                    new StockReportBatch("report.xlsx", "Sheet1", DateTimeOffset.UtcNow, [item]));
            }

            await StartInitializerAsync(settings);

            await using ServiceProvider restarted = BuildServices(settings);
            await using PlannerDbContext dbContext = await CreateDbContextAsync(restarted);
            StockReportItemEntity stored = await dbContext.StockReportItems.SingleAsync();
            Assert.Equal("0049001234", stored.MaterialDocumentNumber);
            Assert.Single(await dbContext.Set<PlannerSchemaVersion>().ToListAsync());
        }
        finally
        {
            await DeleteDatabaseAsync(settings);
        }
    }

    private static async Task AssertGeneratedReportImportsAndPlansPageLoadsAsync(
        Dictionary<string, string?> settings)
    {
        try
        {
            await CreateOutdatedDatabaseAsync(settings);

            await using WebApplicationFactory<Program> baseFactory = new();
            await using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(
                builder =>
                {
                    foreach ((string key, string? value) in settings)
                    {
                        builder.UseSetting(key, value);
                    }
                });
            using HttpClient client = factory.CreateClient();

            StockReportImportResultDto result = await ImportGeneratedReportAsync(client);
            Assert.Equal(20, result.ValidRowCount);
            Assert.Equal(0, result.ErrorRowCount);

            HttpResponseMessage page = await client.GetAsync("/consumption-plans");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            string html = await page.Content.ReadAsStringAsync();
            Assert.Contains(result.Items[0].MaterialDocumentNumber, html);
        }
        finally
        {
            await DeleteDatabaseAsync(settings);
        }
    }

    private static async Task<StockReportImportResultDto> ImportGeneratedReportAsync(
        HttpClient client)
    {
        using MemoryStream workbook = new();
        StockReportWorkbookGenerator.Generate(workbook, new StockReportGeneratorOptions
        {
            RowCount = 20,
            Seed = 42,
            HeaderLanguage = StockReportHeaderLanguage.English,
            SheetName = "Stock"
        });

        string mappingJson = JsonSerializer.Serialize(new
        {
            sheetName = "Stock",
            headerRowNumber = 1,
            dataStartRowNumber = 2,
            fieldColumnMap = StockReportFieldCatalog.All
                .Select((metadata, index) => (metadata.Field, Column: index + 1))
                .ToDictionary(pair => pair.Field.ToString(), pair => pair.Column)
        });

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(workbook.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "generated.xlsx");
        form.Add(new StringContent(mappingJson), "mapping");

        HttpResponseMessage response = await client.PostAsync("/api/stock-reports/import", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        StockReportImportResultDto? result =
            await response.Content.ReadFromJsonAsync<StockReportImportResultDto>();
        Assert.NotNull(result);
        return result;
    }

    private static async Task CreateOutdatedDatabaseAsync(Dictionary<string, string?> settings)
    {
        await using ServiceProvider services = BuildServices(settings);
        await using PlannerDbContext dbContext = await CreateDbContextAsync(services);
        await dbContext.GetService<IRelationalDatabaseCreator>().CreateAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"StockReportItems\" " +
            "(\"Id\" INTEGER PRIMARY KEY, \"MaterialNumber\" TEXT)");
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"StockReportItems\" (\"Id\", \"MaterialNumber\") VALUES (1, 'MAT-OLD')");
    }

    private static async Task StartInitializerAsync(Dictionary<string, string?> settings)
    {
        await using ServiceProvider services = BuildServices(settings);
        IHostedService initializer = services.GetServices<IHostedService>()
            .OfType<PlannerDatabaseInitializerHostedService>()
            .Single();
        await initializer.StartAsync(CancellationToken.None);
    }

    private static async Task DeleteDatabaseAsync(Dictionary<string, string?> settings)
    {
        await using ServiceProvider services = BuildServices(settings);
        await using PlannerDbContext dbContext = await CreateDbContextAsync(services);
        await dbContext.Database.EnsureDeletedAsync();
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddStockReportFeature(configuration);
        return services.BuildServiceProvider();
    }

    private static Task<PlannerDbContext> CreateDbContextAsync(ServiceProvider services)
    {
        return services.GetRequiredService<IDbContextFactory<PlannerDbContext>>()
            .CreateDbContextAsync();
    }

    private static Dictionary<string, string?> SqliteSettings()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-schema-{Guid.NewGuid():N}.db");
        return new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Sqlite",
            ["Persistence:ConnectionString"] = $"Data Source={dbPath}"
        };
    }

    private static Dictionary<string, string?> PostgresSettings()
    {
        NpgsqlConnectionStringBuilder builder = new(
            Environment.GetEnvironmentVariable(PostgresFactAttribute.ConnectionStringVariable))
        {
            Database = $"scp_schema_{Guid.NewGuid():N}"
        };
        return new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Postgres",
            ["Persistence:ConnectionString"] = builder.ConnectionString
        };
    }
}
