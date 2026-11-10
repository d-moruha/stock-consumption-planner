using System.Globalization;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans;

/// <summary>
/// How a stock quantity splits into the planned part, the unplanned part, and any planned excess.
/// Widths are percentages of one bar and sum to 100 when a bar is shown.
/// </summary>
public readonly record struct StockBalanceShare(
    decimal PlannedPercent,
    decimal UnplannedPercent,
    decimal OverPercent)
{
    public string PlannedWidth => Width(PlannedPercent);

    public string UnplannedWidth => Width(UnplannedPercent);

    public string OverWidth => Width(OverPercent);

    public static StockBalanceShare? TryCreate(decimal? stockQuantity, decimal? unplannedQuantity)
    {
        if (stockQuantity is not decimal stock || unplannedQuantity is not decimal unplanned)
        {
            return null;
        }

        if (stock <= 0m)
        {
            return unplanned < 0m ? new StockBalanceShare(0m, 0m, 100m) : null;
        }

        if (unplanned >= stock)
        {
            return new StockBalanceShare(0m, 100m, 0m);
        }

        if (unplanned >= 0m)
        {
            decimal plannedPercent = Percent(stock - unplanned, stock);
            return new StockBalanceShare(plannedPercent, 100m - plannedPercent, 0m);
        }

        decimal overPercent = Percent(-unplanned, stock - unplanned);
        return new StockBalanceShare(100m - overPercent, 0m, overPercent);
    }

    private static decimal Percent(decimal part, decimal whole)
    {
        decimal percent = decimal.Round(part / whole * 100m, 2, MidpointRounding.AwayFromZero);
        if (percent < 0m)
        {
            return 0m;
        }

        return percent > 100m ? 100m : percent;
    }

    private static string Width(decimal percent)
    {
        return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
