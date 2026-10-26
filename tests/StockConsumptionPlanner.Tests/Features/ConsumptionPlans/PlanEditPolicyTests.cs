using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

/// <summary>Europe/Kyiv is UTC+2 in winter and UTC+3 in summer; times below are UTC.</summary>
public sealed class PlanEditPolicyTests
{
    private static readonly PlanTargetMonth December2026 = new(2026, 12);

    // 2026-12-01 00:00 Kyiv (+02) − 14 days = 2026-11-17 00:00 Kyiv.
    private static readonly DateTimeOffset December2026LockUtc = new(2026, 11, 16, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EditableUntil_CreatedLongBeforeMonth_UsesFourteenDaysBeforeMonth()
    {
        PlanEditPolicy policy = CreatePolicy(Utc(2026, 10, 25, 10));

        DateTimeOffset editableUntil = policy.GetEditableUntilUtc(Utc(2026, 10, 25, 10), December2026);

        Assert.Equal(December2026LockUtc, editableUntil);
    }

    [Fact]
    public void EditableUntil_CreatedCloseToMonth_UsesSevenDaysAfterCreation()
    {
        PlanEditPolicy policy = CreatePolicy(Utc(2026, 11, 20, 8));

        Assert.Equal(Utc(2026, 11, 27, 8), policy.GetEditableUntilUtc(Utc(2026, 11, 20, 8), December2026));
    }

    [Fact]
    public void EditableUntil_CreatedInsideTargetMonth_StillGetsSevenDays()
    {
        PlanEditPolicy policy = CreatePolicy(Utc(2026, 12, 5, 9));

        Assert.Equal(Utc(2026, 12, 12, 9), policy.GetEditableUntilUtc(Utc(2026, 12, 5, 9), December2026));
    }

    [Fact]
    public void IsWithinEditPeriod_SevenDayBoundary_IsExclusive()
    {
        DateTimeOffset createdAt = Utc(2026, 11, 20, 8);
        MutableTimeProvider clock = new(createdAt.AddDays(7).AddTicks(-1));
        PlanEditPolicy policy = CreatePolicy(clock);

        Assert.True(policy.IsWithinEditPeriod(createdAt, December2026));

        clock.UtcNow = createdAt.AddDays(7);
        Assert.False(policy.IsWithinEditPeriod(createdAt, December2026));
    }

    [Fact]
    public void IsWithinEditPeriod_FourteenDayBoundary_IsExclusive()
    {
        DateTimeOffset createdAt = Utc(2026, 10, 1, 12);
        MutableTimeProvider clock = new(December2026LockUtc.AddSeconds(-1));
        PlanEditPolicy policy = CreatePolicy(clock);

        Assert.True(policy.IsWithinEditPeriod(createdAt, December2026));

        clock.UtcNow = December2026LockUtc;
        Assert.False(policy.IsWithinEditPeriod(createdAt, December2026));
    }

    [Fact]
    public void LockBeforeMonth_IsComputedOnLocalCalendarAcrossDaylightSavingChange()
    {
        // DST starts 2027-03-28 in Kyiv: April 1 is +03, March 18 is still +02.
        PlanEditPolicy policy = CreatePolicy(Utc(2027, 1, 1, 0));
        PlanTargetMonth april2027 = new(2027, 4);

        Assert.Equal(Utc(2027, 3, 31, 21), policy.GetMonthStartUtc(april2027));
        Assert.Equal(Utc(2027, 3, 17, 22), policy.GetLockBeforeMonthUtc(april2027));
    }

    [Theory]
    [InlineData(2027, 1, 2026, 12, 17)]
    [InlineData(2027, 3, 2027, 2, 14)]
    [InlineData(2028, 3, 2028, 2, 15)]
    public void LockBeforeMonth_HandlesYearRolloverAndLeapYears(
        int targetYear,
        int targetMonth,
        int expectedYear,
        int expectedMonth,
        int expectedDayUtc)
    {
        PlanEditPolicy policy = CreatePolicy(Utc(2026, 1, 1, 0));

        DateTimeOffset lockUtc = policy.GetLockBeforeMonthUtc(new PlanTargetMonth(targetYear, targetMonth));

        Assert.Equal(Utc(expectedYear, expectedMonth, expectedDayUtc, 22), lockUtc);
    }

    [Fact]
    public void CanCreate_AllowsCurrentAndFutureMonthsOnly()
    {
        // 2026-10-31 21:59:59Z is 23:59:59 on October 31 in Kyiv (+02 after DST ended on October 25).
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 10, 31, 21, 59, 59, TimeSpan.Zero));
        PlanEditPolicy policy = CreatePolicy(clock);

        Assert.False(policy.CanCreate(new PlanTargetMonth(2026, 9)));
        Assert.True(policy.CanCreate(new PlanTargetMonth(2026, 10)));
        Assert.True(policy.CanCreate(new PlanTargetMonth(2026, 11)));
        Assert.False(policy.CanCreate(new PlanTargetMonth(2026, 13)));

        clock.UtcNow = Utc(2026, 10, 31, 22);
        Assert.False(policy.CanCreate(new PlanTargetMonth(2026, 10)));
        Assert.Equal(new PlanTargetMonth(2026, 11), policy.CurrentMonth);
    }

    [Fact]
    public void ResolveTimeZone_UnknownId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PlanEditPolicy.ResolveTimeZone("Mars/Olympus_Mons"));
    }

    [Theory]
    [InlineData("2026-12", true, 2026, 12)]
    [InlineData(" 2027-01 ", true, 2027, 1)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("1999-05", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    public void PlanTargetMonth_TryParse(string text, bool expected, int year, int month)
    {
        Assert.Equal(expected, PlanTargetMonth.TryParse(text, out PlanTargetMonth parsed));
        if (expected)
        {
            Assert.Equal(new PlanTargetMonth(year, month), parsed);
        }
    }

    [Fact]
    public void PlanTargetMonth_Next_RollsOverYear()
    {
        Assert.Equal(new PlanTargetMonth(2027, 1), new PlanTargetMonth(2026, 12).Next());
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1.23456", false)]
    [InlineData("1.2345", true)]
    [InlineData("250", true)]
    public void ValidatePlannedQuantity(string quantity, bool expectedValid)
    {
        string? error = ConsumptionPlan.ValidatePlannedQuantity(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expectedValid, error is null);
    }

    private static PlanEditPolicy CreatePolicy(DateTimeOffset utcNow) => CreatePolicy(new MutableTimeProvider(utcNow));

    private static PlanEditPolicy CreatePolicy(TimeProvider clock) => new(clock, new ConsumptionPlanOptions());

    private static DateTimeOffset Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);
}
