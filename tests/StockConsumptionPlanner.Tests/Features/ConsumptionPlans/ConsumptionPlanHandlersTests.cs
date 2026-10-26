using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.DeletePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;
using StockConsumptionPlanner.App.Persistence;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class ConsumptionPlanHandlersTests
{
    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    // 14 days before 2026-12-01 00:00 Kyiv.
    private static readonly DateTimeOffset December2026LockUtc = new(2026, 11, 16, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_ForActiveStock_PersistsPlanWithSnapshotAndEditPeriod()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));

        ConsumptionPlanResult<ConsumptionPlanDto> result = await CreateAsync(host, "0049000001", 2026, 12, 4.5m);

        Assert.True(result.IsSuccess, result.Error?.Message);
        ConsumptionPlanDto plan = result.Value!;
        Assert.Equal(4.5m, plan.PlannedQuantity);
        Assert.Equal("MAT-0049000001", plan.MaterialNumber);
        Assert.Equal("Material 0049000001", plan.MaterialDescription);
        Assert.Equal(10m, plan.CurrentStockQuantity);
        Assert.False(plan.ExceedsStock);
        Assert.False(plan.IsOrphaned);
        Assert.Equal(October25, plan.CreatedAtUtc);
        Assert.Equal(December2026LockUtc, plan.EditableUntilUtc);
        Assert.True(plan.IsEditable);
        Assert.True(plan.CanDelete);
        Assert.Equal(1, plan.Version);
    }

    [Fact]
    public async Task Create_TrimsKeyPartsBeforeMatchingStock()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));

        ConsumptionPlanResult<ConsumptionPlanDto> result = await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, " 0049000001 ", " 0001", 2026, 12, 1m));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("0049000001", result.Value!.MaterialDocumentNumber);
        Assert.Equal("0001", result.Value.MaterialDocumentItem);
    }

    [Fact]
    public async Task Create_SecondPlanForSameStockAndMonth_ReturnsDuplicate_ButOtherMonthIsAllowed()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        Assert.True((await CreateAsync(host, "0049000001", 2026, 12, 1m)).IsSuccess);

        ConsumptionPlanResult<ConsumptionPlanDto> duplicate = await CreateAsync(host, "0049000001", 2026, 12, 2m);
        ConsumptionPlanResult<ConsumptionPlanDto> otherMonth = await CreateAsync(host, "0049000001", 2027, 1, 2m);

        Assert.Equal(ConsumptionPlanErrorCode.DuplicatePlan, duplicate.Error?.Code);
        Assert.True(otherMonth.IsSuccess, otherMonth.Error?.Message);
    }

    [Fact]
    public async Task UniqueIndex_RejectsDuplicateKeyAndMonthAtDatabaseLevel()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        IDbContextFactory<PlannerDbContext> dbFactory = host.Get<IDbContextFactory<PlannerDbContext>>();
        StockDocumentKey key = new(2026, "0049000001", "0001");

        await using PlannerDbContext db = await dbFactory.CreateDbContextAsync();
        db.ConsumptionPlans.Add(ConsumptionPlan.Create(key, new PlanTargetMonth(2026, 12), 1m, "M", "D", October25));
        db.ConsumptionPlans.Add(ConsumptionPlan.Create(key, new PlanTargetMonth(2026, 12), 2m, "M", "D", October25));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("10.0001", ConsumptionPlanErrorCode.QuantityExceedsStock)]
    [InlineData("0", ConsumptionPlanErrorCode.Validation)]
    public async Task Create_InvalidQuantity_IsRejected(string quantity, ConsumptionPlanErrorCode expected)
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));

        ConsumptionPlanResult<ConsumptionPlanDto> result = await CreateAsync(
            host,
            "0049000001",
            2026,
            12,
            decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, result.Error?.Code);
    }

    [Fact]
    public async Task Create_QuantityEqualToStock_IsAllowed()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));

        Assert.True((await CreateAsync(host, "0049000001", 2026, 12, 10m)).IsSuccess);
    }

    [Fact]
    public async Task Create_StockMissingOrDuplicatedInCurrentReport_IsRejected()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Stock("0049000001", 10m),
            Stock("0049000002", 5m, sourceRow: 3),
            Stock("0049000002", 7m, sourceRow: 4));

        ConsumptionPlanResult<ConsumptionPlanDto> missing = await CreateAsync(host, "0049009999", 2026, 12, 1m);
        ConsumptionPlanResult<ConsumptionPlanDto> ambiguous = await CreateAsync(host, "0049000002", 2026, 12, 1m);

        Assert.Equal(ConsumptionPlanErrorCode.StockNotActive, missing.Error?.Code);
        Assert.Equal(ConsumptionPlanErrorCode.AmbiguousStock, ambiguous.Error?.Code);
    }

    [Fact]
    public async Task Create_ForEndedMonth_IsRejected_CurrentMonthIsAllowed()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));

        ConsumptionPlanResult<ConsumptionPlanDto> past = await CreateAsync(host, "0049000001", 2026, 9, 1m);
        ConsumptionPlanResult<ConsumptionPlanDto> current = await CreateAsync(host, "0049000001", 2026, 10, 1m);

        Assert.Equal(ConsumptionPlanErrorCode.TargetMonthInPast, past.Error?.Code);
        Assert.True(current.IsSuccess, current.Error?.Message);
    }

    [Fact]
    public async Task Update_DuringFirstSevenDays_IsAllowed_EvenInsideFourteenDaysOfMonth()
    {
        DateTimeOffset createdAt = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
        await using ConsumptionPlanTestHost host = await StartAsync(createdAt);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanDto plan = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;

        host.Clock.UtcNow = createdAt.AddDays(7).AddMinutes(-1);
        ConsumptionPlanResult<ConsumptionPlanDto> updated = await UpdateAsync(host, plan, 3m);
        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(3m, updated.Value!.PlannedQuantity);
        Assert.Equal(2, updated.Value.Version);
        Assert.Equal(createdAt, updated.Value.CreatedAtUtc);

        host.Clock.UtcNow = createdAt.AddDays(7);
        ConsumptionPlanResult<ConsumptionPlanDto> closed = await UpdateAsync(host, updated.Value, 4m);
        Assert.Equal(ConsumptionPlanErrorCode.EditPeriodClosed, closed.Error?.Code);
    }

    [Fact]
    public async Task Update_AfterFirstSevenDays_IsAllowedUntilFourteenDaysBeforeMonth()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanDto plan = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;

        host.Clock.UtcNow = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        ConsumptionPlanResult<ConsumptionPlanDto> updated = await UpdateAsync(host, plan, 2m);
        Assert.True(updated.IsSuccess, updated.Error?.Message);

        host.Clock.UtcNow = December2026LockUtc;
        ConsumptionPlanResult<ConsumptionPlanDto> closed = await UpdateAsync(host, updated.Value!, 3m);
        Assert.Equal(ConsumptionPlanErrorCode.EditPeriodClosed, closed.Error?.Code);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsStaleVersion()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanDto plan = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;
        Assert.True((await UpdateAsync(host, plan, 2m)).IsSuccess);

        ConsumptionPlanResult<ConsumptionPlanDto> stale = await UpdateAsync(host, plan, 3m);

        Assert.Equal(ConsumptionPlanErrorCode.StaleVersion, stale.Error?.Code);
    }

    [Fact]
    public async Task Update_AboveCurrentStock_IsRejected()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanDto plan = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;

        ConsumptionPlanResult<ConsumptionPlanDto> result = await UpdateAsync(host, plan, 11m);

        Assert.Equal(ConsumptionPlanErrorCode.QuantityExceedsStock, result.Error?.Code);
    }

    [Fact]
    public async Task OrphanedPlan_IsReadOnly_ButCanBeDeletedWithinEditPeriod()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanDto plan = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;
        await host.ImportAsync(Stock("0049000002", 10m));

        ConsumptionPlanResult<ConsumptionPlanDto> update = await UpdateAsync(host, plan, 2m);
        ConsumptionPlanResult<bool> delete = await host.Get<DeletePlanHandler>().HandleAsync(plan.Id, plan.Version);

        Assert.Equal(ConsumptionPlanErrorCode.PlanOrphaned, update.Error?.Code);
        Assert.True(delete.IsSuccess, delete.Error?.Message);
    }

    [Fact]
    public async Task Delete_FollowsEditPeriodAndVersion()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m), Stock("0049000002", 10m, sourceRow: 3));
        ConsumptionPlanDto first = (await CreateAsync(host, "0049000001", 2026, 12, 1m)).Value!;
        ConsumptionPlanDto second = (await CreateAsync(host, "0049000002", 2026, 12, 1m)).Value!;
        DeletePlanHandler handler = host.Get<DeletePlanHandler>();

        Assert.Equal(ConsumptionPlanErrorCode.StaleVersion, (await handler.HandleAsync(first.Id, 99)).Error?.Code);
        Assert.True((await handler.HandleAsync(first.Id, first.Version)).IsSuccess);
        Assert.Equal(ConsumptionPlanErrorCode.NotFound, (await handler.HandleAsync(first.Id, first.Version)).Error?.Code);

        host.Clock.UtcNow = December2026LockUtc;
        Assert.Equal(ConsumptionPlanErrorCode.EditPeriodClosed, (await handler.HandleAsync(second.Id, second.Version)).Error?.Code);
    }

    [Fact]
    public async Task PlanningSheet_ListsActiveStockWithPlansOfRequestedMonthOnly()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2),
            Stock("0049000002", 20m, sourceRow: 3),
            Stock("0049000003", 30m, sourceRow: 4));
        await CreateAsync(host, "0049000002", 2026, 12, 5m);
        await CreateAsync(host, "0049000003", 2027, 1, 6m);

        ConsumptionPlanResult<PlanningSheetDto> result = await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12));

        Assert.True(result.IsSuccess, result.Error?.Message);
        PlanningSheetDto sheet = result.Value!;
        Assert.True(sheet.CanCreate);
        Assert.Equal(December2026LockUtc, sheet.LockBeforeMonthUtc);
        Assert.Equal(3, sheet.Rows.TotalCount);
        Assert.Equal(["0049000001", "0049000002", "0049000003"], sheet.Rows.Items.Select(r => r.MaterialDocumentNumber));
        Assert.Null(sheet.Rows.Items[0].Plan);
        Assert.Equal(5m, sheet.Rows.Items[1].Plan?.PlannedQuantity);
        Assert.Null(sheet.Rows.Items[2].Plan);

        ConsumptionPlanResult<PlanningSheetDto> paged = await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12, Search: "0049000002", Page: 1, PageSize: 1));
        Assert.Equal(1, paged.Value!.Rows.TotalCount);
        Assert.Equal("0049000002", Assert.Single(paged.Value.Rows.Items).MaterialDocumentNumber);
    }

    private static Task<ConsumptionPlanResult<ConsumptionPlanDto>> CreateAsync(
        ConsumptionPlanTestHost host,
        string documentNumber,
        int targetYear,
        int targetMonth,
        decimal quantity)
    {
        return host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, documentNumber, "0001", targetYear, targetMonth, quantity));
    }

    private static Task<ConsumptionPlanResult<ConsumptionPlanDto>> UpdateAsync(
        ConsumptionPlanTestHost host,
        ConsumptionPlanDto plan,
        decimal quantity)
    {
        return host.Get<UpdatePlanHandler>().HandleAsync(plan.Id, new UpdateConsumptionPlanRequest(quantity, plan.Version));
    }
}
