using System.Globalization;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.PlanPeriod;

public enum PlanPeriodError
{
    None,
    YearRequired,
    YearNotFourDigits,
    YearOutOfRange,
    MonthRequired,
    MonthUnknown,
    PeriodTooEarly
}

public readonly record struct PlanPeriodField(int Value, PlanPeriodError Error)
{
    public bool IsValid => Error == PlanPeriodError.None;
}

public sealed class PlanPeriodInput
{
    public const int YearsAroundCurrent = 5;

    public PlanPeriodInput(int currentYear, CultureInfo culture, PlanTargetMonth? earliest = null)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CurrentYear = currentYear;
        Culture = culture;
        Earliest = earliest;
        MinYear = Math.Max(
            Math.Max(PlanTargetMonth.MinYear, currentYear - YearsAroundCurrent),
            earliest?.Year ?? PlanTargetMonth.MinYear);
        MaxYear = Math.Min(PlanTargetMonth.MaxYear, currentYear + YearsAroundCurrent);
        Years = [.. Enumerable.Range(MinYear, MaxYear - MinYear + 1).Select(FormatYear)];
        MonthNames = [.. Enumerable.Range(1, 12).Select(FormatMonth)];
    }

    public int CurrentYear { get; }

    public PlanTargetMonth? Earliest { get; }

    public CultureInfo Culture { get; }

    public int MinYear { get; }

    public int MaxYear { get; }

    public IReadOnlyList<string> Years { get; }

    public IReadOnlyList<string> MonthNames { get; }

    public static string FormatYear(int year)
    {
        return year.ToString("D4", CultureInfo.InvariantCulture);
    }

    public string FormatMonth(int month)
    {
        string name = Culture.DateTimeFormat.GetMonthName(month);
        return name.Length == 0 ? name : char.ToUpper(name[0], Culture) + name[1..];
    }

    public bool IsBeforeEarliest(PlanTargetMonth period)
    {
        return Earliest is { } earliest && period.CompareTo(earliest) < 0;
    }

    public PlanPeriodField ParseYear(string? text)
    {
        string value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return new PlanPeriodField(0, PlanPeriodError.YearRequired);
        }

        if (value.Length != 4 || !value.All(char.IsAsciiDigit))
        {
            return new PlanPeriodField(0, PlanPeriodError.YearNotFourDigits);
        }

        int year = int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        return year >= MinYear && year <= MaxYear
            ? new PlanPeriodField(year, PlanPeriodError.None)
            : new PlanPeriodField(0, PlanPeriodError.YearOutOfRange);
    }

    public PlanPeriodField ParseMonth(string? text)
    {
        string value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return new PlanPeriodField(0, PlanPeriodError.MonthRequired);
        }

        if (value.All(char.IsAsciiDigit))
        {
            return value.Length <= 2
                && int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture) is int number
                and >= 1 and <= 12
                ? new PlanPeriodField(number, PlanPeriodError.None)
                : new PlanPeriodField(0, PlanPeriodError.MonthUnknown);
        }

        for (int month = 1; month <= 12; month++)
        {
            string name = Culture.DateTimeFormat.GetMonthName(month);
            if (Culture.CompareInfo.Compare(value, name, CompareOptions.IgnoreCase) == 0)
            {
                return new PlanPeriodField(month, PlanPeriodError.None);
            }
        }

        return new PlanPeriodField(0, PlanPeriodError.MonthUnknown);
    }
}
