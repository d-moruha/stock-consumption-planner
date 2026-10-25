using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.PurgeOrphans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class PurgeOrphanedPlansTests
{
    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Purge_DeletesOnlyPlansOrphanedForSixMonthsOrMore()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2),
            Stock("0049000002", 10m, sourceRow: 3),
            Stock("0049000003", 10m, sourceRow: 4));
        int oldOrphan = await CreatePlanAsync(host, "0049000001");
        int newerOrphan = await CreatePlanAsync(host, "0049000002");
        int linked = await CreatePlanAsync(host, "0049000003");

        DateTimeOffset firstOrphanedAt = host.Clock.UtcNow.AddHours(1);
        host.Clock.UtcNow = firstOrphanedAt;
        await host.ImportAsync(Stock("0049000002", 10m), Stock("0049000003", 10m, sourceRow: 3));

        host.Clock.Advance(TimeSpan.FromDays(1));
        await host.ImportAsync(Stock("0049000003", 10m));

        PurgeOrphanedPlansHandler purge = host.Get<PurgeOrphanedPlansHandler>();

        host.Clock.UtcNow = firstOrphanedAt.AddMonths(6).AddSeconds(-1);
        Assert.Equal(0, await purge.PurgeAsync());

        host.Clock.UtcNow = firstOrphanedAt.AddMonths(6);
        Assert.Equal(1, await purge.PurgeAsync());
        Assert.Equal(0, await purge.PurgeAsync());

        GetPlansHandler plans = host.Get<GetPlansHandler>();
        Assert.Null(await plans.GetByIdAsync(oldOrphan));
        Assert.NotNull(await plans.GetByIdAsync(newerOrphan));
        Assert.NotNull(await plans.GetByIdAsync(linked));

        host.Clock.UtcNow = firstOrphanedAt.AddYears(5);
        Assert.Equal(1, await purge.PurgeAsync());
        Assert.NotNull(await plans.GetByIdAsync(linked));
    }

    private static async Task<int> CreatePlanAsync(ConsumptionPlanTestHost host, string documentNumber)
    {
        ConsumptionPlanResult<ConsumptionPlanDto> result = await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, documentNumber, "0001", 2026, 12, 1m));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.Id;
    }
}
