namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed class ConsumptionPlan
{
    public const decimal MaxPlannedQuantity = 99_999_999_999_999.9999m;

    public const int QuantityScale = 4;

    private ConsumptionPlan()
    {
    }

    public int Id { get; private set; }

    public int MaterialDocumentYear { get; private set; }

    public string MaterialDocumentNumber { get; private set; } = string.Empty;

    public string MaterialDocumentItem { get; private set; } = string.Empty;

    public int TargetYear { get; private set; }

    public int TargetMonth { get; private set; }

    public decimal PlannedQuantity { get; private set; }

    public string MaterialNumber { get; private set; } = string.Empty;

    public string MaterialDescription { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary><c>null</c> while an Active stock row with the same key exists.</summary>
    public DateTimeOffset? OrphanedAtUtc { get; private set; }

    public int Version { get; private set; }

    public StockDocumentKey Key => new(MaterialDocumentYear, MaterialDocumentNumber, MaterialDocumentItem);

    public PlanTargetMonth Target => new(TargetYear, TargetMonth);

    public bool IsOrphaned => OrphanedAtUtc.HasValue;

    public static ConsumptionPlan Create(
        StockDocumentKey key,
        PlanTargetMonth target,
        decimal plannedQuantity,
        string materialNumber,
        string materialDescription,
        DateTimeOffset nowUtc)
    {
        return new ConsumptionPlan
        {
            MaterialDocumentYear = key.MaterialDocumentYear,
            MaterialDocumentNumber = key.MaterialDocumentNumber,
            MaterialDocumentItem = key.MaterialDocumentItem,
            TargetYear = target.Year,
            TargetMonth = target.Month,
            PlannedQuantity = plannedQuantity,
            MaterialNumber = materialNumber,
            MaterialDescription = materialDescription,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1
        };
    }

    public void UpdateQuantity(
        decimal plannedQuantity,
        string materialNumber,
        string materialDescription,
        DateTimeOffset nowUtc)
    {
        PlannedQuantity = plannedQuantity;
        MaterialNumber = materialNumber;
        MaterialDescription = materialDescription;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public static string? ValidatePlannedQuantity(decimal plannedQuantity)
    {
        if (plannedQuantity <= 0m)
        {
            return "Planned quantity must be greater than zero.";
        }

        if (plannedQuantity > MaxPlannedQuantity)
        {
            return "Planned quantity is too large.";
        }

        if (decimal.Round(plannedQuantity, QuantityScale) != plannedQuantity)
        {
            return $"Planned quantity may have at most {QuantityScale} decimal places.";
        }

        return null;
    }
}
