using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Localization;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class PlanningSheetStockValueTests
{
    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnplannedQuantity_SubtractsAllPlans_AndIsNullWhenQuantityIsMissing()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2),
            Stock("0049000002", 20m, sourceRow: 3),
            Stock("0049000003", 8m, sourceRow: 4),
            Stock("0049000004", 12m, sourceRow: 5));
        await CreatePlanAsync(host, "0049000001", 2026, 12, 4m);
        await CreatePlanAsync(host, "0049000001", 2027, 1, 3m);
        await CreatePlanAsync(host, "0049000003", 2026, 12, 5m);
        await CreatePlanAsync(host, "0049000003", 2027, 2, 6m);
        await CreatePlanAsync(host, "0049000004", 2026, 12, 2m);
        await host.SetStockFiguresAsync("0049000004", null, 40m);

        PlanningSheetDto sheet = await SheetAsync(host, PlanningSheetSort.SourceRow);

        (string Document, decimal? Stock, decimal? Unplanned, decimal? Value)[] expected =
        [
            ("0049000001", 10m, 3m, 100m),
            ("0049000002", 20m, 20m, 200m),
            ("0049000003", 8m, -3m, 80m),
            ("0049000004", null, null, 40m)
        ];
        Assert.Equal(
            expected,
            sheet.Rows.Items.Select(r => (
                r.MaterialDocumentNumber,
                r.CurrentStockQuantity,
                r.UnplannedQuantity,
                r.TotalStockValue)));
        Assert.Equal(4m, sheet.Rows.Items[0].Plan?.PlannedQuantity);
        Assert.Equal(2, sheet.Rows.Items[0].Plans.Count);
        Assert.Single(sheet.Rows.Items[3].Plans);
    }

    [Fact]
    public async Task DefaultSort_IsStockValueDescending_AndSearchExcludesOtherDocuments()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await SeedValuesAsync();

        PlanningSheetDto sheet = await SheetAsync(host, sort: null, search: "0049");

        Assert.Equal(5, sheet.Rows.TotalCount);
        Assert.Equal(
            [Matching(1), Matching(4), Matching(2), Matching(3), Matching(5)],
            sheet.Rows.Items.Select(r => r.MaterialDocumentNumber));
    }

    [Theory]
    [InlineData(PlanningSheetSort.StockValueAscending, new[] { 2, 3, 1, 4, 5 })]
    [InlineData(PlanningSheetSort.StockValueDescending, new[] { 1, 4, 2, 3, 5 })]
    public async Task Sort_OrdersSearchResultBeforePaging_WithNullValuesLast(
        PlanningSheetSort sort,
        int[] expectedDocuments)
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await SeedValuesAsync();
        await CreatePlanAsync(host, Matching(4), 2026, 12, 1m);

        List<PlanningSheetRowDto> rows = [];
        for (int page = 1; page <= 3; page++)
        {
            PlanningSheetDto sheet = await SheetAsync(host, sort, "0049", page, pageSize: 2);
            Assert.Equal(5, sheet.Rows.TotalCount);
            Assert.Equal(page == 3 ? 1 : 2, sheet.Rows.Items.Count);
            rows.AddRange(sheet.Rows.Items);
        }

        Assert.Equal(
            expectedDocuments.Select(Matching),
            rows.Select(r => r.MaterialDocumentNumber));
        Assert.Equal(
            [(2026, 12)],
            rows.Single(r => r.MaterialDocumentNumber == Matching(4)).Plans
                .Select(p => (p.TargetYear, p.TargetMonth)));
    }

    private static string Matching(int number) => $"004900000{number}";

    private static async Task<ConsumptionPlanTestHost> SeedValuesAsync()
    {
        ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Stock(Matching(1), 10m, sourceRow: 2),
            Stock(Matching(2), 10m, sourceRow: 3),
            Stock(Matching(3), 10m, sourceRow: 4),
            Stock(Matching(4), 10m, sourceRow: 5),
            Stock(Matching(5), 10m, sourceRow: 6),
            Stock("0051000001", 10m, sourceRow: 7));
        (string Document, decimal? Value)[] values =
        [
            (Matching(1), 500m),
            (Matching(2), 10m),
            (Matching(3), 10m),
            (Matching(4), 500m),
            (Matching(5), null),
            ("0051000001", 9999m)
        ];
        foreach ((string document, decimal? value) in values)
        {
            await host.SetStockFiguresAsync(document, 10m, value);
        }

        return host;
    }

    private static async Task<PlanningSheetDto> SheetAsync(
        ConsumptionPlanTestHost host,
        PlanningSheetSort? sort = null,
        string? search = null,
        int? page = null,
        int? pageSize = null)
    {
        PlanningSheetQuery query = sort is PlanningSheetSort selected
            ? new PlanningSheetQuery(2026, 12, search, page, pageSize, selected)
            : new PlanningSheetQuery(2026, 12, search, page, pageSize);
        ConsumptionPlanResult<PlanningSheetDto> result = await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(query);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static async Task CreatePlanAsync(
        ConsumptionPlanTestHost host,
        string documentNumber,
        int targetYear,
        int targetMonth,
        decimal quantity)
    {
        CreatePlanHandler handler = host.Get<CreatePlanHandler>();
        ConsumptionPlanResult<ConsumptionPlanDto> result = await handler.HandleAsync(
            new CreateConsumptionPlanRequest(
                2026,
                documentNumber,
                "0001",
                targetYear,
                targetMonth,
                quantity));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
