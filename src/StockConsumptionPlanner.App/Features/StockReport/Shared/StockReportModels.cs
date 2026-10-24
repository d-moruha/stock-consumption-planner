using System.Text.Json.Serialization;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockLifecycleStatus
{
    Staging = 0,
    Active = 1,
    Superseded = 2
}

public sealed record StockReportItem(
    int SourceRowNumber,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int MaterialDocumentYear,
    string MaterialNumber,
    string MaterialDescription,
    decimal MaterialQuantity,
    decimal MaterialValue,
    StockLifecycleStatus LifecycleStatus = StockLifecycleStatus.Active);

public sealed record StockReportBatch(
    string SourceFileName,
    string SheetName,
    DateTimeOffset ImportedAtUtc,
    IReadOnlyList<StockReportItem> Items,
    StockLifecycleStatus LifecycleStatus = StockLifecycleStatus.Active)
{
    public int TotalRows => Items.Count;

    public decimal TotalQuantity => Items.Sum(item => item.MaterialQuantity);

    public decimal TotalValue => Items.Sum(item => item.MaterialValue);
}
