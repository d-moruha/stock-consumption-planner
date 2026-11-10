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
    decimal TotalStockQuantity,
    decimal TotalStockValue,
    StockLifecycleStatus LifecycleStatus = StockLifecycleStatus.Active)
{
    public string? WbsElement { get; init; }

    public string? BatchNumber { get; init; }

    public string? StorageLocation { get; init; }

    public string? MovementType { get; init; }

    public string? BaseUnitOfMeasure { get; init; }

    public string? LocalCurrency { get; init; }

    public string? SpecialStockIndicator { get; init; }

    public string? CrossPlantMaterialStatus { get; init; }

    public string? PlantSpecificMaterialStatus { get; init; }

    public bool ClientLevelDeletionFlag { get; init; }

    public bool PlantLevelDeletionFlag { get; init; }

    public DateOnly? StockCreationDate { get; init; }

    public string? StockType { get; init; }

    public string? StockTypeDescription { get; init; }

    public string? PurchaseOrder { get; init; }

    public string? PurchaseOrderItem { get; init; }

    public string? PurchaseRequisition { get; init; }

    public string? PurchaseRequisitionItem { get; init; }

    public DateOnly? GoodsReceiptDate { get; init; }

    public string? Vendor { get; init; }

    public string? VendorName { get; init; }

    public object? GetValue(StockReportField field)
    {
        return field switch
        {
            StockReportField.MaterialDocumentNumber => MaterialDocumentNumber,
            StockReportField.MaterialDocumentItem => MaterialDocumentItem,
            StockReportField.MaterialDocumentYear => MaterialDocumentYear,
            StockReportField.WbsElement => WbsElement,
            StockReportField.BatchNumber => BatchNumber,
            StockReportField.MaterialNumber => MaterialNumber,
            StockReportField.MaterialDescription => MaterialDescription,
            StockReportField.StorageLocation => StorageLocation,
            StockReportField.MovementType => MovementType,
            StockReportField.TotalStockQuantity => TotalStockQuantity,
            StockReportField.BaseUnitOfMeasure => BaseUnitOfMeasure,
            StockReportField.TotalStockValue => TotalStockValue,
            StockReportField.LocalCurrency => LocalCurrency,
            StockReportField.SpecialStockIndicator => SpecialStockIndicator,
            StockReportField.CrossPlantMaterialStatus => CrossPlantMaterialStatus,
            StockReportField.PlantSpecificMaterialStatus => PlantSpecificMaterialStatus,
            StockReportField.ClientLevelDeletionFlag => ClientLevelDeletionFlag,
            StockReportField.PlantLevelDeletionFlag => PlantLevelDeletionFlag,
            StockReportField.StockCreationDate => StockCreationDate,
            StockReportField.StockType => StockType,
            StockReportField.StockTypeDescription => StockTypeDescription,
            StockReportField.PurchaseOrder => PurchaseOrder,
            StockReportField.PurchaseOrderItem => PurchaseOrderItem,
            StockReportField.PurchaseRequisition => PurchaseRequisition,
            StockReportField.PurchaseRequisitionItem => PurchaseRequisitionItem,
            StockReportField.GoodsReceiptDate => GoodsReceiptDate,
            StockReportField.Vendor => Vendor,
            StockReportField.VendorName => VendorName,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }
}

public sealed record StockReportBatch(
    string SourceFileName,
    string SheetName,
    DateTimeOffset ImportedAtUtc,
    IReadOnlyList<StockReportItem> Items,
    StockLifecycleStatus LifecycleStatus = StockLifecycleStatus.Active)
{
    public int TotalRows => Items.Count;

    public decimal TotalQuantity => Items.Sum(item => item.TotalStockQuantity);

    public decimal TotalValue => Items.Sum(item => item.TotalStockValue);
}
