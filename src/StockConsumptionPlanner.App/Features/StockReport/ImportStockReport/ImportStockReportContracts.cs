using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public sealed class StockReportColumnMappingDto
{
    public string? SheetName { get; set; }

    public int HeaderRowNumber { get; set; } = 1;

    public int DataStartRowNumber { get; set; } = 2;

    public Dictionary<StockReportField, int> FieldColumnMap { get; set; } = new();
}

public sealed record StockReportItemDto(
    int SourceRowNumber,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int MaterialDocumentYear,
    string MaterialNumber,
    string MaterialDescription,
    decimal TotalStockQuantity,
    decimal TotalStockValue)
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
}

public sealed record StockReportRowErrorDto(
    int RowNumber,
    string Field,
    string? RawValue,
    string Message);

public sealed record StockReportImportResultDto(
    string SourceFileName,
    string SheetName,
    DateTimeOffset ImportedAtUtc,
    int TotalRowsProcessed,
    int ValidRowCount,
    int ErrorRowCount,
    decimal TotalQuantity,
    decimal TotalValue,
    IReadOnlyList<int> IgnoredColumnIndices,
    IReadOnlyList<StockReportItemDto> Items,
    IReadOnlyList<StockReportRowErrorDto> Errors);
