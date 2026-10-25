using System.Globalization;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public readonly record struct PlanTargetMonth(int Year, int Month) : IComparable<PlanTargetMonth>
{
    public const int MinYear = 2000;

    public const int MaxYear = 2100;

    public bool IsValid => Year is >= MinYear and <= MaxYear && Month is >= 1 and <= 12;

    public PlanTargetMonth Next() => Month == 12 ? new PlanTargetMonth(Year + 1, 1) : new PlanTargetMonth(Year, Month + 1);

    public int CompareTo(PlanTargetMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool TryParse(string? text, out PlanTargetMonth month)
    {
        month = default;
        if (DateTime.TryParseExact(
                text?.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
        {
            month = new PlanTargetMonth(parsed.Year, parsed.Month);
            return month.IsValid;
        }

        return false;
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
