using System.Globalization;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.PlanPeriod;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans.PlanPeriod;

public sealed class PlanPeriodInputTests
{
    private static readonly PlanPeriodInput Ukrainian =
        new(2026, new CultureInfo(PlannerCultures.Ukrainian));

    private static readonly PlanPeriodInput English =
        new(2026, new CultureInfo(PlannerCultures.English));

    [Fact]
    public void Years_SpanFiveYearsAroundCurrentYear()
    {
        Assert.Equal(2021, Ukrainian.MinYear);
        Assert.Equal(2031, Ukrainian.MaxYear);
        Assert.Equal(
            [
                "2021", "2022", "2023", "2024", "2025", "2026",
                "2027", "2028", "2029", "2030", "2031"
            ],
            Ukrainian.Years);
    }

    [Fact]
    public void Years_StayWithinPlanTargetMonthLimits()
    {
        PlanPeriodInput input = new(2098, CultureInfo.InvariantCulture);

        Assert.Equal(2093, input.MinYear);
        Assert.Equal(2100, input.MaxYear);
    }

    [Fact]
    public void Earliest_RaisesMinYearAndRejectsEarlierPeriods()
    {
        PlanPeriodInput input = new(
            2026,
            CultureInfo.InvariantCulture,
            new PlanTargetMonth(2026, 12));

        Assert.Equal(2026, input.MinYear);
        Assert.Equal(2031, input.MaxYear);
        Assert.Equal(PlanPeriodError.YearOutOfRange, input.ParseYear("2025").Error);
        Assert.True(input.IsBeforeEarliest(new PlanTargetMonth(2026, 11)));
        Assert.False(input.IsBeforeEarliest(new PlanTargetMonth(2026, 12)));
        Assert.False(input.IsBeforeEarliest(new PlanTargetMonth(2027, 1)));
        Assert.False(Ukrainian.IsBeforeEarliest(new PlanTargetMonth(2021, 1)));
    }

    [Fact]
    public void MonthNames_AreLocalizedAndCapitalized()
    {
        Assert.Equal(
            [
                "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
            ],
            Ukrainian.MonthNames);
        Assert.Equal("January", English.MonthNames[0]);
        Assert.Equal("December", English.MonthNames[11]);
    }

    [Theory]
    [InlineData("2026", 2026)]
    [InlineData(" 2031 ", 2031)]
    [InlineData("2021", 2021)]
    public void ParseYear_AcceptsFourDigitYearInRange(string text, int expected)
    {
        Assert.Equal(Valid(expected), Ukrainian.ParseYear(text));
    }

    [Theory]
    [InlineData(null, PlanPeriodError.YearRequired)]
    [InlineData("", PlanPeriodError.YearRequired)]
    [InlineData("   ", PlanPeriodError.YearRequired)]
    [InlineData("26", PlanPeriodError.YearNotFourDigits)]
    [InlineData("20266", PlanPeriodError.YearNotFourDigits)]
    [InlineData("20x6", PlanPeriodError.YearNotFourDigits)]
    [InlineData("-2026", PlanPeriodError.YearNotFourDigits)]
    [InlineData("2 026", PlanPeriodError.YearNotFourDigits)]
    [InlineData("２０２６", PlanPeriodError.YearNotFourDigits)]
    [InlineData("2020", PlanPeriodError.YearOutOfRange)]
    [InlineData("2032", PlanPeriodError.YearOutOfRange)]
    public void ParseYear_RejectsInvalidInput(string? text, PlanPeriodError expected)
    {
        PlanPeriodField result = Ukrainian.ParseYear(text);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("01", 1)]
    [InlineData(" 9 ", 9)]
    [InlineData("12", 12)]
    [InlineData("Листопад", 11)]
    [InlineData("листопад", 11)]
    [InlineData("ТРАВЕНЬ", 5)]
    [InlineData(" січень ", 1)]
    public void ParseMonth_AcceptsNumberOrUkrainianName(string text, int expected)
    {
        Assert.Equal(Valid(expected), Ukrainian.ParseMonth(text));
    }

    [Theory]
    [InlineData("march", 3)]
    [InlineData("December", 12)]
    [InlineData("7", 7)]
    public void ParseMonth_AcceptsNumberOrEnglishName(string text, int expected)
    {
        Assert.Equal(Valid(expected), English.ParseMonth(text));
    }

    [Theory]
    [InlineData(null, PlanPeriodError.MonthRequired)]
    [InlineData("", PlanPeriodError.MonthRequired)]
    [InlineData("  ", PlanPeriodError.MonthRequired)]
    [InlineData("0", PlanPeriodError.MonthUnknown)]
    [InlineData("13", PlanPeriodError.MonthUnknown)]
    [InlineData("001", PlanPeriodError.MonthUnknown)]
    [InlineData("1.5", PlanPeriodError.MonthUnknown)]
    [InlineData("-1", PlanPeriodError.MonthUnknown)]
    [InlineData("лист", PlanPeriodError.MonthUnknown)]
    [InlineData("листопада", PlanPeriodError.MonthUnknown)]
    [InlineData("November", PlanPeriodError.MonthUnknown)]
    public void ParseMonth_RejectsInvalidUkrainianInput(string? text, PlanPeriodError expected)
    {
        PlanPeriodField result = Ukrainian.ParseMonth(text);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData("листопад")]
    [InlineData("Nov")]
    public void ParseMonth_RejectsNamesFromOtherCulturesOrAbbreviations(string text)
    {
        Assert.Equal(PlanPeriodError.MonthUnknown, English.ParseMonth(text).Error);
    }

    private static PlanPeriodField Valid(int value)
    {
        return new PlanPeriodField(value, PlanPeriodError.None);
    }
}
