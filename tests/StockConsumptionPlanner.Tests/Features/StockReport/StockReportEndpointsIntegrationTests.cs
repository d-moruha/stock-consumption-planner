using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public StockReportEndpointsIntegrationTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task Healthz_ReturnsHealthyWithDefaultSqliteProvider()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            await using WebApplicationFactory<Program> factory = CreateSqliteFactory(dbPath);
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/healthz");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string payload = await response.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", payload);
            Assert.Contains("Sqlite", payload);
        }
        finally
        {
            DeleteTempFile(dbPath);
        }
    }

    [Fact]
    public async Task Inspect_Import_And_GetCurrent_EndToEndFlow_PersistsActiveRecordsToSqlite()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            await using WebApplicationFactory<Program> factory = CreateSqliteFactory(dbPath);
            using HttpClient client = factory.CreateClient();

            CurrentStockReportDto? emptyCurrent = await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(emptyCurrent);
            Assert.False(emptyCurrent.HasReport);
            Assert.Equal(0, emptyCurrent.TotalRows);
            Assert.Empty(emptyCurrent.Items);

            using MemoryStream excelStream = OpenXmlStockReportServiceTests.CreateUkrainianWorkbookWithPreamble();
            byte[] fileBytes = excelStream.ToArray();

            using (MultipartFormDataContent inspectForm = new())
            {
                ByteArrayContent fileContent = new(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                inspectForm.Add(fileContent, "file", "zvit_zalushki.xlsx");
                inspectForm.Add(new StringContent("Залишки"), "sheetName");
                inspectForm.Add(new StringContent("4"), "headerRowNumber");

                HttpResponseMessage inspectResponse = await client.PostAsync("/api/stock-reports/inspect", inspectForm);
                Assert.Equal(HttpStatusCode.OK, inspectResponse.StatusCode);

                StockReportInspectionDto? inspection = await inspectResponse.Content
                    .ReadFromJsonAsync<StockReportInspectionDto>();

                Assert.NotNull(inspection);
                Assert.Equal("Залишки", inspection.ActiveSheetName);
                Assert.Equal(4, inspection.HeaderRowNumber);
                Assert.Equal(5, inspection.DataStartRowNumber);
                Assert.Equal(7, inspection.SuggestedMapping.Count);
            }

            string mappingJson = """
            {
              "sheetName": "Залишки",
              "headerRowNumber": 4,
              "dataStartRowNumber": 5,
              "fieldColumnMap": {
                "MaterialDocumentNumber": "B",
                "MaterialDocumentItem": 3,
                "MaterialDccoumentYear": 4,
                "MaterialNumber": 6,
                "MaterialDescription": 7,
                "MaterialQuantity": 8,
                "MaterialValue": "I"
              }
            }
            """;

            using (MultipartFormDataContent importForm = new())
            {
                ByteArrayContent fileContent = new(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                importForm.Add(fileContent, "file", "zvit_zalushki.xlsx");
                importForm.Add(new StringContent(mappingJson), "mapping");

                HttpResponseMessage importResponse = await client.PostAsync("/api/stock-reports/import", importForm);
                Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

                StockReportImportResultDto? importResult = await importResponse.Content
                    .ReadFromJsonAsync<StockReportImportResultDto>();

                Assert.NotNull(importResult);
                Assert.Equal(2, importResult.ValidRowCount);
                Assert.Equal(0, importResult.ErrorRowCount);
                Assert.Equal(15.750m, importResult.TotalQuantity);
                Assert.Equal(20000.00m, importResult.TotalValue);
                Assert.Contains(1, importResult.IgnoredColumnIndices);
                Assert.Contains(5, importResult.IgnoredColumnIndices);
            }

            CurrentStockReportDto? current = await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(current);
            Assert.True(current.HasReport);
            Assert.Equal("zvit_zalushki.xlsx", current.SourceFileName);
            Assert.Equal("Залишки", current.SheetName);
            Assert.Equal(2, current.TotalRows);
            Assert.Equal(15.750m, current.TotalQuantity);
            Assert.Equal(20000.00m, current.TotalValue);
            Assert.Equal("0049001234", current.Items[0].MaterialDocumentNumber);
            Assert.Equal("0001", current.Items[0].MaterialDocumentItem);

            IDbContextFactory<PlannerDbContext> dbFactory =
                factory.Services.GetRequiredService<IDbContextFactory<PlannerDbContext>>();
            await using PlannerDbContext dbContext = await dbFactory.CreateDbContextAsync();

            StockReportBatchEntity dbBatch = await dbContext.StockReportBatches.SingleAsync();
            Assert.Equal(StockLifecycleStatus.Active, dbBatch.LifecycleStatus);
            Assert.Equal("zvit_zalushki.xlsx", dbBatch.SourceFileName);

            List<StockReportItemEntity> dbItems = await dbContext.StockReportItems
                .OrderBy(i => i.SourceRowNumber)
                .ToListAsync();
            Assert.Equal(2, dbItems.Count);
            Assert.All(dbItems, item => Assert.Equal(StockLifecycleStatus.Active, item.LifecycleStatus));
            Assert.Equal("0049001234", dbItems[0].MaterialDocumentNumber);
            Assert.Equal("0001", dbItems[0].MaterialDocumentItem);
            Assert.Equal(12.500m, dbItems[0].MaterialQuantity);
            Assert.Equal(15400.75m, dbItems[0].MaterialValue);
        }
        finally
        {
            DeleteTempFile(dbPath);
        }
    }

    [Fact]
    public async Task Import_SecondReportWithOverlappingCompoundDocumentKeys_ReplacesFirstReportInDbAndApi()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            await using WebApplicationFactory<Program> factory = CreateSqliteFactory(dbPath);
            using HttpClient client = factory.CreateClient();

            string standardMappingJson = """
            {
              "sheetName": "Sheet1",
              "headerRowNumber": 1,
              "dataStartRowNumber": 2,
              "fieldColumnMap": {
                "MaterialDocumentNumber": 1,
                "MaterialDocumentItem": 2,
                "MaterialDocumentYear": 3,
                "MaterialNumber": 4,
                "MaterialDescription": 5,
                "MaterialQuantity": 6,
                "MaterialValue": 7
              }
            }
            """;

            using MemoryStream report1Stream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
                "Sheet1",
                new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
                {
                    [1] =
                    [
                        (1, "MaterialDocumentNumber"),
                        (2, "MaterialDocumentItem"),
                        (3, "MaterialDocumentYear"),
                        (4, "MaterialNumber"),
                        (5, "MaterialDescription"),
                        (6, "MaterialQuantity"),
                        (7, "MaterialValue")
                    ],
                    [2] =
                    [
                        (1, "0049001234"),
                        (2, "0001"),
                        (3, 2026),
                        (4, "MAT-100"),
                        (5, "Steel Pipe"),
                        (6, 100.00m),
                        (7, 1000.00m)
                    ],
                    [3] =
                    [
                        (1, "0049001235"),
                        (2, "0002"),
                        (3, 2026),
                        (4, "MAT-200"),
                        (5, "Copper Wire"),
                        (6, 50.00m),
                        (7, 500.00m)
                    ]
                });

            await PostImportAsync(client, report1Stream.ToArray(), "report1.xlsx", standardMappingJson);

            // Report 2 has the exact same compound key (2026, 0049001234, 0001) with updated quantity (65.5) + a new document.
            using MemoryStream report2Stream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
                "Sheet1",
                new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
                {
                    [1] =
                    [
                        (1, "MaterialDocumentNumber"),
                        (2, "MaterialDocumentItem"),
                        (3, "MaterialDocumentYear"),
                        (4, "MaterialNumber"),
                        (5, "MaterialDescription"),
                        (6, "MaterialQuantity"),
                        (7, "MaterialValue")
                    ],
                    [2] =
                    [
                        (1, "0049001234"),
                        (2, "0001"),
                        (3, 2026),
                        (4, "MAT-100"),
                        (5, "Steel Pipe"),
                        (6, 65.50m),
                        (7, 655.00m)
                    ],
                    [3] =
                    [
                        (1, "0049009999"),
                        (2, "0001"),
                        (3, 2026),
                        (4, "MAT-999"),
                        (5, "Valve Assembly"),
                        (6, 10.00m),
                        (7, 245.00m)
                    ]
                });

            await PostImportAsync(client, report2Stream.ToArray(), "report2.xlsx", standardMappingJson);

            CurrentStockReportDto? current = await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(current);
            Assert.True(current.HasReport);
            Assert.Equal("report2.xlsx", current.SourceFileName);
            Assert.Equal(2, current.TotalRows);
            Assert.Equal(75.50m, current.TotalQuantity);
            Assert.Equal(900.00m, current.TotalValue);
            Assert.Equal(65.50m, current.Items.Single(i => i.MaterialDocumentNumber == "0049001234").MaterialQuantity);

            IDbContextFactory<PlannerDbContext> dbFactory =
                factory.Services.GetRequiredService<IDbContextFactory<PlannerDbContext>>();
            await using PlannerDbContext dbContext = await dbFactory.CreateDbContextAsync();

            Assert.Equal(1, await dbContext.StockReportBatches.CountAsync());
            Assert.Equal(2, await dbContext.StockReportItems.CountAsync());
            Assert.All(
                await dbContext.StockReportItems.ToListAsync(),
                item => Assert.Equal(StockLifecycleStatus.Active, item.LifecycleStatus));
        }
        finally
        {
            DeleteTempFile(dbPath);
        }
    }

    [Fact]
    public async Task StagingIsolation_GetCurrentEndpoint_ReturnsActiveReportWhileNextReportIsStaging()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            await using WebApplicationFactory<Program> factory = CreateSqliteFactory(dbPath);
            using HttpClient client = factory.CreateClient();

            EfCoreStockReportRepository repository = Assert.IsType<EfCoreStockReportRepository>(
                factory.Services.GetRequiredService<IStockReportRepository>());

            StockReportBatch activeReport = new(
                "active_report1.xlsx",
                "Sheet1",
                DateTimeOffset.UtcNow,
                [
                    new StockReportItem(2, "0049000100", "0001", 2026, "MAT-ACTIVE", "Active Item", 42.0m, 420.0m)
                ]);
            await repository.SaveCurrentAsync(activeReport);

            StockReportBatch stagingReport = new(
                "staging_report2.xlsx",
                "Sheet1",
                DateTimeOffset.UtcNow,
                [
                    new StockReportItem(2, "0049000200", "0001", 2026, "MAT-STAGING", "Staging Item", 999.0m, 9990.0m),
                    new StockReportItem(3, "0049000201", "0002", 2026, "MAT-STAGING-2", "Staging Item 2", 1.0m, 10.0m)
                ]);

            int stagedBatchId = await repository.StageBatchInChunksAsync(stagingReport, chunkSize: 1);

            // While Report 2 is in Staging, GET /api/stock-reports/current must still return Report 1 (Active).
            CurrentStockReportDto? currentDuringStaging =
                await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(currentDuringStaging);
            Assert.Equal("active_report1.xlsx", currentDuringStaging.SourceFileName);
            Assert.Single(currentDuringStaging.Items);
            Assert.Equal("0049000100", currentDuringStaging.Items[0].MaterialDocumentNumber);

            // Now promote Report 2 to Active -> GET /api/stock-reports/current immediately switches to Report 2.
            await repository.PromoteStagedBatchAsync(stagedBatchId);

            CurrentStockReportDto? currentAfterPromotion =
                await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(currentAfterPromotion);
            Assert.Equal("staging_report2.xlsx", currentAfterPromotion.SourceFileName);
            Assert.Equal(2, currentAfterPromotion.TotalRows);
            Assert.Equal(1000.0m, currentAfterPromotion.TotalQuantity);
        }
        finally
        {
            DeleteTempFile(dbPath);
        }
    }

    [Fact]
    public async Task ImportEndpoint_MultiChunk2100RowsWorkbook_PersistsAllRowsAndPurgesPreviousBatch()
    {
        string dbPath = CreateTempSqlitePath();
        try
        {
            await using WebApplicationFactory<Program> factory = CreateSqliteFactory(dbPath);
            using HttpClient client = factory.CreateClient();

            Dictionary<int, IReadOnlyList<(int Column, object Value)>> rows = new()
            {
                [1] =
                [
                    (1, "MaterialDocumentNumber"),
                    (2, "MaterialDocumentItem"),
                    (3, "MaterialDocumentYear"),
                    (4, "MaterialNumber"),
                    (5, "MaterialDescription"),
                    (6, "MaterialQuantity"),
                    (7, "MaterialValue")
                ]
            };

            const int rowCount = 2100;
            for (int i = 1; i <= rowCount; i++)
            {
                rows[i + 1] =
                [
                    (1, $"0049{i:D6}"),
                    (2, "0001"),
                    (3, 2026),
                    (4, $"MAT-{i:D5}"),
                    (5, $"Bulk Material {i}"),
                    (6, 2.0m),
                    (7, 10.0m)
                ];
            }

            using MemoryStream bulkStream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook("BulkSheet", rows);

            string mappingJson = """
            {
              "sheetName": "BulkSheet",
              "headerRowNumber": 1,
              "dataStartRowNumber": 2,
              "fieldColumnMap": {
                "MaterialDocumentNumber": 1,
                "MaterialDocumentItem": 2,
                "MaterialDocumentYear": 3,
                "MaterialNumber": 4,
                "MaterialDescription": 5,
                "MaterialQuantity": 6,
                "MaterialValue": 7
              }
            }
            """;

            await PostImportAsync(client, bulkStream.ToArray(), "bulk_2100.xlsx", mappingJson);

            CurrentStockReportDto? current = await client.GetFromJsonAsync<CurrentStockReportDto>("/api/stock-reports/current");
            Assert.NotNull(current);
            Assert.True(current.HasReport);
            Assert.Equal("bulk_2100.xlsx", current.SourceFileName);
            Assert.Equal(rowCount, current.TotalRows);
            Assert.Equal(4200.0m, current.TotalQuantity);
            Assert.Equal(21000.0m, current.TotalValue);
        }
        finally
        {
            DeleteTempFile(dbPath);
        }
    }

    private WebApplicationFactory<Program> CreateSqliteFactory(string sqliteDbPath)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={sqliteDbPath};Pooling=False"
                });
            });
        });
    }

    private static async Task PostImportAsync(
        HttpClient client,
        byte[] fileBytes,
        string fileName,
        string mappingJson)
    {
        using MultipartFormDataContent importForm = new();
        ByteArrayContent fileContent = new(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        importForm.Add(fileContent, "file", fileName);
        importForm.Add(new StringContent(mappingJson), "mapping");

        HttpResponseMessage response = await client.PostAsync("/api/stock-reports/import", importForm);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateTempSqlitePath()
    {
        return Path.Combine(Path.GetTempPath(), $"scp-api-test-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup lock races in test teardown.
        }
    }
}
