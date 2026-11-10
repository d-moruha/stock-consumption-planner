using StockConsumptionPlanner.App.Features.ConsumptionPlans;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class StockBalanceShareTests
{
    [Fact]
    public void NoPlans_FillsTheBarWithTheUnplannedPart()
    {
        StockBalanceShare share = Assert.IsType<StockBalanceShare>(
            StockBalanceShare.TryCreate(10m, 10m));

        Assert.Equal(new StockBalanceShare(0m, 100m, 0m), share);
        Assert.Equal("100%", share.UnplannedWidth);
    }

    [Fact]
    public void PartialPlans_SplitTheBarBetweenPlannedAndUnplanned()
    {
        StockBalanceShare share = Assert.IsType<StockBalanceShare>(
            StockBalanceShare.TryCreate(10m, 3m));

        Assert.Equal(new StockBalanceShare(70m, 30m, 0m), share);
        Assert.Equal("70%", share.PlannedWidth);
        Assert.Equal("30%", share.UnplannedWidth);
    }

    [Fact]
    public void FullyPlanned_LeavesNoUnplannedPart()
    {
        StockBalanceShare share = Assert.IsType<StockBalanceShare>(
            StockBalanceShare.TryCreate(8m, 0m));

        Assert.Equal(new StockBalanceShare(100m, 0m, 0m), share);
    }

    [Fact]
    public void PlansOverStock_ShowTheExcessBeyondTheStock()
    {
        StockBalanceShare share = Assert.IsType<StockBalanceShare>(
            StockBalanceShare.TryCreate(10m, -2m));

        Assert.Equal(new StockBalanceShare(83.33m, 0m, 16.67m), share);
        Assert.Equal("83.33%", share.PlannedWidth);
        Assert.Equal("16.67%", share.OverWidth);
    }

    [Fact]
    public void MissingQuantity_HasNoBar()
    {
        Assert.Null(StockBalanceShare.TryCreate(null, null));
        Assert.Null(StockBalanceShare.TryCreate(null, 0m));
        Assert.Null(StockBalanceShare.TryCreate(0m, 0m));
    }

    [Fact]
    public void ZeroStockWithPlans_IsEntirelyOverStock()
    {
        StockBalanceShare share = Assert.IsType<StockBalanceShare>(
            StockBalanceShare.TryCreate(0m, -4m));

        Assert.Equal(new StockBalanceShare(0m, 0m, 100m), share);
    }
}
