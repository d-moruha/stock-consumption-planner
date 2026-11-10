namespace StockConsumptionPlanner.App.Persistence;

public sealed class PlannerSchemaVersion
{
    public const string TableName = "__PlannerSchemaVersion";

    public string Hash { get; set; } = string.Empty;
}
