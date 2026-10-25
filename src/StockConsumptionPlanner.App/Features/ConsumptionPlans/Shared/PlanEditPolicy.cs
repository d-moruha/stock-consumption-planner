namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

/// <summary>
/// Decides when plans may be created, updated, and deleted.
/// A plan is editable until the later of (created + <see cref="ConsumptionPlanOptions.EditDaysAfterCreation"/> days)
/// and (target month start − <see cref="ConsumptionPlanOptions.LockDaysBeforeTargetMonth"/> days).
/// A plan may be created for the current or any future month.
/// </summary>
public sealed class PlanEditPolicy
{
    private readonly TimeProvider _timeProvider;
    private readonly ConsumptionPlanOptions _options;

    public PlanEditPolicy(TimeProvider timeProvider, ConsumptionPlanOptions options)
        : this(timeProvider, options, ResolveTimeZone(options.TimeZoneId))
    {
    }

    public PlanEditPolicy(TimeProvider timeProvider, ConsumptionPlanOptions options, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeZone);

        _timeProvider = timeProvider;
        _options = options;
        TimeZone = timeZone;
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public PlanTargetMonth CurrentMonth
    {
        get
        {
            DateTimeOffset local = ToBusinessTime(UtcNow);
            return new PlanTargetMonth(local.Year, local.Month);
        }
    }

    public DateTimeOffset GetMonthStartUtc(PlanTargetMonth month)
    {
        return LocalMidnightToUtc(new DateTime(month.Year, month.Month, 1));
    }

    public DateTimeOffset GetLockBeforeMonthUtc(PlanTargetMonth month)
    {
        DateTime localMonthStart = new(month.Year, month.Month, 1);
        return LocalMidnightToUtc(localMonthStart.AddDays(-_options.LockDaysBeforeTargetMonth));
    }

    public DateTimeOffset GetEditableUntilUtc(DateTimeOffset createdAtUtc, PlanTargetMonth month)
    {
        DateTimeOffset afterCreation = createdAtUtc.ToUniversalTime().AddDays(_options.EditDaysAfterCreation);
        DateTimeOffset beforeMonth = GetLockBeforeMonthUtc(month);
        return afterCreation > beforeMonth ? afterCreation : beforeMonth;
    }

    public bool IsWithinEditPeriod(DateTimeOffset createdAtUtc, PlanTargetMonth month)
    {
        return UtcNow < GetEditableUntilUtc(createdAtUtc, month);
    }

    public bool CanCreate(PlanTargetMonth month)
    {
        return month.IsValid && UtcNow < GetMonthStartUtc(month.Next());
    }

    public DateTimeOffset ToBusinessTime(DateTimeOffset value)
    {
        return TimeZoneInfo.ConvertTime(value, TimeZone);
    }

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        string[] candidates = string.Equals(timeZoneId, "Europe/Kyiv", StringComparison.OrdinalIgnoreCase)
            ? ["Europe/Kyiv", "Europe/Kiev"]
            : [timeZoneId];

        foreach (string candidate in candidates)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out TimeZoneInfo? timeZone))
            {
                return timeZone;
            }
        }

        throw new InvalidOperationException(
            $"Time zone '{timeZoneId}' configured in {ConsumptionPlanOptions.SectionName}:{nameof(ConsumptionPlanOptions.TimeZoneId)} was not found.");
    }

    private DateTimeOffset LocalMidnightToUtc(DateTime localDate)
    {
        DateTime local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZone), TimeSpan.Zero);
    }
}
