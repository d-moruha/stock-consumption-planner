using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _sqliteDbPath;

    public StockReportEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _sqliteDbPath = Path.Combine(Path.GetTempPath(), $"scp-test-{Guid.NewGuid():N}.db");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={_sqliteDbPath};Pooling=False"
                });
            });
        });
    }

    [Fact]
    public async Task Healthz_ReturnsHealthyWithDefaultSqliteProvider()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", payload);
        Assert.Contains("Sqlite", payload);
    }

    [Fact]
    public async Task Inspect_Import_And_GetCurrent_EndToEndFlow_PersistsToSqliteWithCustomMappingAndTypoKey()
    {
        using HttpClient client = _factory.CreateClient();
        using MemoryStream excelStream = ClosedXmlStockReportServiceTests.CreateUkrainianWorkbookWithPreamble();
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
        Assert.True(File.Exists(_sqliteDbPath));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_sqliteDbPath))
            {
                File.Delete(_sqliteDbPath);
            }
        }
        catch
        {
            // Ignore cleanup lock races in test teardown.
        }
    }
}
