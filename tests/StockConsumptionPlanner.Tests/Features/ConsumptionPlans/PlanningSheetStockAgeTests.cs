using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class PlanningSheetStockAgeTests
{
    // 2026-11-11 00:30 in Kyiv (UTC+2).
    private static readonly DateTimeOffset KyivJustAfterMidnight =
        new(2026, 11, 10, 22, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Rows_CarryStockDates_AndAgeInDaysUpToKyivToday()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(KyivJustAfterMidnight);
        await host.ImportAsync(
            Dated("0049000001", 2, new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 28)),
            Dated("0049000002", 3, new DateOnly(2026, 11, 11)),
            Dated("0049000003", 4, null, new DateOnly(2026, 9, 15)));

        PlanningSheetDto sheet = await SheetAsync(host, PlanningSheetSort.SourceRow);

        Assert.Equal(new DateOnly(2026, 11, 11), host.Get<PlanEditPolicy>().Today);
        (DateOnly? Received, DateOnly? Created, int? AgeDays)[] expected =
        [
            (new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 1), 10),
            (null, new DateOnly(2026, 11, 11), 0),
            (new DateOnly(2026, 9, 15), null, null)
        ];
        Assert.Equal(
            expected,
            sheet.Rows.Items.Select(r => (r.GoodsReceiptDate, r.StockCreationDate, r.StockAgeDays)));

        host.Clock.Advance(TimeSpan.FromDays(1));
        sheet = await SheetAsync(host, PlanningSheetSort.SourceRow);
        int?[] nextDayAges = [11, 1, null];
        Assert.Equal(nextDayAges, sheet.Rows.Items.Select(r => r.StockAgeDays));
    }

    [Theory]
    [InlineData(PlanningSheetSort.SourceRow, new[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(PlanningSheetSort.StockAgeAscending, new[] { 3, 6, 4, 1, 2, 5 })]
    [InlineData(PlanningSheetSort.StockAgeDescending, new[] { 1, 4, 3, 6, 2, 5 })]
    public async Task Sort_OrdersWholeSearchResultBeforePaging_WithUndatedStockLast(
        PlanningSheetSort sort,
        int[] expectedDocuments)
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Dated(Matching(1), 2, new DateOnly(2026, 3, 1)),
            Dated(Matching(2), 3, null),
            Dated(Matching(3), 4, new DateOnly(2026, 10, 1)),
            Dated(Matching(4), 5, new DateOnly(2026, 6, 1)),
            Dated(Matching(5), 6, null),
            Dated(Matching(6), 7, new DateOnly(2026, 10, 1)),
            Dated("0051000001", 8, new DateOnly(2020, 1, 1)),
            Dated("0051000002", 9, new DateOnly(2026, 10, 20)));
        await CreatePlanAsync(host, Matching(4), 2027, 1);
        await CreatePlanAsync(host, Matching(4), 2026, 12);

        List<PlanningSheetRowDto> rows = [];
        for (int page = 1; page <= 3; page++)
        {
            PlanningSheetDto sheet = await SheetAsync(host, sort, "0049", page, pageSize: 2);
            Assert.Equal(6, sheet.Rows.TotalCount);
            Assert.Equal(2, sheet.Rows.Items.Count);
            rows.AddRange(sheet.Rows.Items);
        }

        Assert.Equal(expectedDocuments.Select(Matching), rows.Select(r => r.MaterialDocumentNumber));
        Assert.Equal(
            [(2026, 12), (2027, 1)],
            rows.Single(r => r.MaterialDocumentNumber == Matching(4)).Plans
                .Select(p => (p.TargetYear, p.TargetMonth)));
    }

    [Fact]
    public async Task UnknownSort_IsRejected()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);

        ConsumptionPlanResult<PlanningSheetDto> result = await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12, Sort: (PlanningSheetSort)99));

        Assert.Equal(ConsumptionPlanErrorCode.Validation, result.Error?.Code);
    }

    private static string Matching(int number) => $"004900000{number}";

    private static StockReportItem Dated(
        string documentNumber,
        int sourceRow,
        DateOnly? created,
        DateOnly? received = null)
    {
        return Stock(documentNumber, 10m, sourceRow: sourceRow) with
        {
            StockCreationDate = created,
            GoodsReceiptDate = received
        };
    }

    private static async Task<PlanningSheetDto> SheetAsync(
        ConsumptionPlanTestHost host,
        PlanningSheetSort sort,
        string? search = null,
        int? page = null,
        int? pageSize = null)
    {
        ConsumptionPlanResult<PlanningSheetDto> result = await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12, search, page, pageSize, sort));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static async Task CreatePlanAsync(
        ConsumptionPlanTestHost host,
        string documentNumber,
        int targetYear,
        int targetMonth)
    {
        ConsumptionPlanResult<ConsumptionPlanDto> result = await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, documentNumber, "0001", targetYear, targetMonth, 1m));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
