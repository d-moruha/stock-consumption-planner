namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed class ConsumptionPlanOptions
{
    public const string SectionName = "ConsumptionPlans";

    /// <summary>Main edit rule: a plan stays editable for this many days after it was created.</summary>
    public int EditDaysAfterCreation { get; set; } = 7;

    /// <summary>Fallback edit rule: a plan stays editable until this many days before its target month starts.</summary>
    public int LockDaysBeforeTargetMonth { get; set; } = 14;

    /// <summary>IANA time zone in which target months start (the 1st at 00:00).</summary>
    public string TimeZoneId { get; set; } = "Europe/Kyiv";

    public int OrphanRetentionMonths { get; set; } = 6;

    /// <summary>How often orphaned plans are purged. Zero or negative disables the background purge.</summary>
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromDays(1);

    public void Validate()
    {
        if (EditDaysAfterCreation < 0)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(EditDaysAfterCreation)} must not be negative.");
        }

        if (LockDaysBeforeTargetMonth < 0)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(LockDaysBeforeTargetMonth)} must not be negative.");
        }

        if (OrphanRetentionMonths < 1)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(OrphanRetentionMonths)} must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(TimeZoneId)} is required.");
        }
    }
}
