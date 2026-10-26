using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class ConsumptionPlanEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _baseFactory;

    public ConsumptionPlanEndpointsIntegrationTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task CrudFlow_EnforcesRulesAndReturnsProblemDetails()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-plans-api-{Guid.NewGuid():N}.db");
        MutableTimeProvider clock = new(October25);
        try
        {
            await using WebApplicationFactory<Program> factory = CreateFactory(dbPath, clock);
            using HttpClient client = factory.CreateClient();

            IStockReportRepository repository = factory.Services.GetRequiredService<IStockReportRepository>();
            await repository.SaveCurrentAsync(new StockReportBatch(
                "plans.xlsx",
                "Sheet1",
                October25,
                [Stock("0049000001", 10m), Stock("0049000002", 20m, sourceRow: 3)]));

            HttpResponseMessage created = await client.PostAsJsonAsync(
                "/api/consumption-plans",
                new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2026, 12, 4m));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            ConsumptionPlanDto? plan = await created.Content.ReadFromJsonAsync<ConsumptionPlanDto>();
            Assert.NotNull(plan);
            Assert.Equal($"/api/consumption-plans/{plan.Id}", created.Headers.Location?.ToString());

            HttpResponseMessage duplicate = await client.PostAsJsonAsync(
                "/api/consumption-plans",
                new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2026, 12, 1m));
            await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, ConsumptionPlanErrorCode.DuplicatePlan);

            HttpResponseMessage aboveStock = await client.PostAsJsonAsync(
                "/api/consumption-plans",
                new CreateConsumptionPlanRequest(2026, "0049000002", "0001", 2026, 12, 21m));
            await AssertProblemAsync(aboveStock, HttpStatusCode.UnprocessableEntity, ConsumptionPlanErrorCode.QuantityExceedsStock);

            HttpResponseMessage updated = await client.PutAsJsonAsync(
                $"/api/consumption-plans/{plan.Id}",
                new UpdateConsumptionPlanRequest(5m, plan.Version));
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            ConsumptionPlanDto? updatedPlan = await updated.Content.ReadFromJsonAsync<ConsumptionPlanDto>();
            Assert.Equal(5m, updatedPlan?.PlannedQuantity);
            Assert.Equal(2, updatedPlan?.Version);

            HttpResponseMessage stale = await client.PutAsJsonAsync(
                $"/api/consumption-plans/{plan.Id}",
                new UpdateConsumptionPlanRequest(6m, plan.Version));
            await AssertProblemAsync(stale, HttpStatusCode.Conflict, ConsumptionPlanErrorCode.StaleVersion);

            PagedResult<ConsumptionPlanDto>? list = await client.GetFromJsonAsync<PagedResult<ConsumptionPlanDto>>(
                "/api/consumption-plans?year=2026&month=12&status=linked");
            Assert.Equal(1, list?.TotalCount);

            HttpResponseMessage badStatus = await client.GetAsync("/api/consumption-plans?status=bogus");
            await AssertProblemAsync(badStatus, HttpStatusCode.BadRequest, ConsumptionPlanErrorCode.Validation);

            PlanningSheetDto? sheet = await client.GetFromJsonAsync<PlanningSheetDto>(
                "/api/consumption-plans/planning-sheet?year=2026&month=12");
            Assert.NotNull(sheet);
            Assert.Equal(2, sheet.Rows.TotalCount);
            Assert.Equal(5m, sheet.Rows.Items.Single(r => r.MaterialDocumentNumber == "0049000001").Plan?.PlannedQuantity);

            clock.UtcNow = new DateTimeOffset(2026, 11, 16, 22, 0, 0, TimeSpan.Zero);
            HttpResponseMessage closed = await client.PutAsJsonAsync(
                $"/api/consumption-plans/{plan.Id}",
                new UpdateConsumptionPlanRequest(6m, 2));
            await AssertProblemAsync(closed, HttpStatusCode.UnprocessableEntity, ConsumptionPlanErrorCode.EditPeriodClosed);

            clock.UtcNow = October25;
            HttpResponseMessage deleted = await client.DeleteAsync($"/api/consumption-plans/{plan.Id}?version=2");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

            HttpResponseMessage missing = await client.GetAsync($"/api/consumption-plans/{plan.Id}");
            await AssertProblemAsync(missing, HttpStatusCode.NotFound, ConsumptionPlanErrorCode.NotFound);
        }
        finally
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            {
                // Ignore cleanup lock races in test teardown.
            }
        }
    }

    private WebApplicationFactory<Program> CreateFactory(string sqliteDbPath, TimeProvider clock)
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
            builder.ConfigureTestServices(services => services.AddSingleton(clock));
        });
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        ConsumptionPlanErrorCode expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode.ToString(), problem.RootElement.GetProperty("code").GetString());
    }
}
