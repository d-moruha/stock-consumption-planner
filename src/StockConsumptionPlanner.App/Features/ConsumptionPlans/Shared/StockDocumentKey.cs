namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

/// <summary>
/// SAP compound key of a material movement document line. Plans link to stock through this key
/// because stock row ids are regenerated on every import.
/// </summary>
public readonly record struct StockDocumentKey(
    int MaterialDocumentYear,
    string MaterialDocumentNumber,
    string MaterialDocumentItem)
{
    public static StockDocumentKey Create(int materialDocumentYear, string? materialDocumentNumber, string? materialDocumentItem)
    {
        return new StockDocumentKey(
            materialDocumentYear,
            materialDocumentNumber?.Trim() ?? string.Empty,
            materialDocumentItem?.Trim() ?? string.Empty);
    }

    public bool IsComplete =>
        MaterialDocumentYear > 0 &&
        !string.IsNullOrEmpty(MaterialDocumentNumber) &&
        !string.IsNullOrEmpty(MaterialDocumentItem);

    public override string ToString() => $"{MaterialDocumentYear}/{MaterialDocumentNumber}/{MaterialDocumentItem}";
}
