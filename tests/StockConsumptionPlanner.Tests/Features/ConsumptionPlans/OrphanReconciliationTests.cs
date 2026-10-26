using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Persistence;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class OrphanReconciliationTests
{
    private static readonly DateTimeOffset October25 = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Import_WithoutPlanKey_OrphansPlan_AndKeepsOriginalTimestampOnLaterImports()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m), Stock("0049000002", 10m, sourceRow: 3));
        int orphanId = await CreatePlanAsync(host, "0049000001");
        int linkedId = await CreatePlanAsync(host, "0049000002");

        host.Clock.Advance(TimeSpan.FromHours(1));
        DateTimeOffset firstMissingImport = host.Clock.UtcNow;
        await host.ImportAsync(Stock("0049000002", 8m));

        ConsumptionPlanDto orphan = await GetAsync(host, orphanId);
        ConsumptionPlanDto linked = await GetAsync(host, linkedId);
        Assert.True(orphan.IsOrphaned);
        Assert.Equal(firstMissingImport, orphan.OrphanedAtUtc);
        Assert.Equal(firstMissingImport.AddMonths(6), orphan.PurgeDueAtUtc);
        Assert.Null(orphan.CurrentStockQuantity);
        Assert.False(orphan.IsEditable);
        Assert.False(linked.IsOrphaned);
        Assert.Equal(8m, linked.CurrentStockQuantity);

        host.Clock.Advance(TimeSpan.FromDays(3));
        await host.ImportAsync(Stock("0049000002", 7m));

        Assert.Equal(firstMissingImport, (await GetAsync(host, orphanId)).OrphanedAtUtc);
    }

    [Fact]
    public async Task Import_WhereOrphanedKeyReappears_RelinksPlan_AndFlagsQuantityAboveNewStock()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m));
        int planId = await CreatePlanAsync(host, "0049000001", quantity: 6m);

        await host.ImportAsync(Stock("0049000099", 1m));
        Assert.True((await GetAsync(host, planId)).IsOrphaned);

        await host.ImportAsync(Stock("0049000001", 4m));

        ConsumptionPlanDto relinked = await GetAsync(host, planId);
        Assert.False(relinked.IsOrphaned);
        Assert.Null(relinked.OrphanedAtUtc);
        Assert.Equal(4m, relinked.CurrentStockQuantity);
        Assert.True(relinked.ExceedsStock);
        Assert.Equal(6m, relinked.PlannedQuantity);
    }

    [Fact]
    public async Task EmptyImport_OrphansEveryPlan()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(October25);
        await host.ImportAsync(Stock("0049000001", 10m), Stock("0049000002", 10m, sourceRow: 3));
        await CreatePlanAsync(host, "0049000001");
        await CreatePlanAsync(host, "0049000002");

        await host.ImportAsync();

        PagedResult<ConsumptionPlanDto> orphaned = await host.Get<GetPlansHandler>()
            .ListAsync(new GetPlansQuery(Status: PlanLinkFilter.Orphaned));
        PagedResult<ConsumptionPlanDto> linked = await host.Get<GetPlansHandler>()
            .ListAsync(new GetPlansQuery(Status: PlanLinkFilter.Linked));
        Assert.Equal(2, orphaned.TotalCount);
        Assert.Equal(0, linked.TotalCount);
    }

    [Fact]
    public async Task FailingPromotionParticipant_RollsBackPromotionAndOrphanMarking()
    {
        await using ConsumptionPlanTestHost host = await StartAsync(
            October25,
            services => services.AddSingleton<IStockPromotionParticipant, ThrowAfterFirstPromotionParticipant>());
        await host.ImportAsync(Stock("0049000001", 10m));
        int planId = await CreatePlanAsync(host, "0049000001");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ImportAsync(Stock("0049000002", 10m)));

        StockReportBatch? current = await host.Repository.GetCurrentAsync();
        Assert.NotNull(current);
        Assert.Equal("0049000001", Assert.Single(current.Items).MaterialDocumentNumber);
        Assert.False((await GetAsync(host, planId)).IsOrphaned);
    }

    private static async Task<int> CreatePlanAsync(ConsumptionPlanTestHost host, string documentNumber, decimal quantity = 1m)
    {
        ConsumptionPlanResult<ConsumptionPlanDto> result = await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, documentNumber, "0001", 2026, 12, quantity));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.Id;
    }

    private static async Task<ConsumptionPlanDto> GetAsync(ConsumptionPlanTestHost host, int id)
    {
        ConsumptionPlanDto? plan = await host.Get<GetPlansHandler>().GetByIdAsync(id);
        Assert.NotNull(plan);
        return plan;
    }

    private sealed class ThrowAfterFirstPromotionParticipant : IStockPromotionParticipant
    {
        private int _calls;

        public Task OnStockPromotedAsync(PlannerDbContext dbContext, int activeBatchId, CancellationToken cancellationToken)
        {
            return Interlocked.Increment(ref _calls) > 1
                ? throw new InvalidOperationException("Simulated failure after orphan marking.")
                : Task.CompletedTask;
        }
    }
}
