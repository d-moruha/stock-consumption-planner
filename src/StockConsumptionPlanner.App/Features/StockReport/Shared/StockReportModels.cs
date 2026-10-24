namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public sealed record StockReportItem(
    int SourceRowNumber,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int MaterialDocumentYear,
    string MaterialNumber,
    string MaterialDescription,
    decimal MaterialQuantity,
    decimal MaterialValue);

public sealed record StockReportBatch(
    string SourceFileName,
    string SheetName,
    DateTimeOffset ImportedAtUtc,
    IReadOnlyList<StockReportItem> Items)
{
    public int TotalRows => Items.Count;

    public decimal TotalQuantity => Items.Sum(item => item.MaterialQuantity);

    public decimal TotalValue => Items.Sum(item => item.MaterialValue);
}
