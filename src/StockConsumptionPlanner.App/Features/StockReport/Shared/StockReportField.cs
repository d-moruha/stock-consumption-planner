using System.Text.Json.Serialization;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockReportField
{
    MaterialDocumentNumber = 0,
    MaterialDocumentItem = 1,
    MaterialDocumentYear = 2,
    WbsElement = 3,
    BatchNumber = 4,
    MaterialNumber = 5,
    MaterialDescription = 6,
    StorageLocation = 7,
    MovementType = 8,
    TotalStockQuantity = 9,
    BaseUnitOfMeasure = 10,
    TotalStockValue = 11,
    LocalCurrency = 12,
    SpecialStockIndicator = 13,
    CrossPlantMaterialStatus = 14,
    PlantSpecificMaterialStatus = 15,
    ClientLevelDeletionFlag = 16,
    PlantLevelDeletionFlag = 17,
    StockCreationDate = 18,
    StockType = 19,
    StockTypeDescription = 20,
    PurchaseOrder = 21,
    PurchaseOrderItem = 22,
    PurchaseRequisition = 23,
    PurchaseRequisitionItem = 24,
    GoodsReceiptDate = 25,
    Vendor = 26,
    VendorName = 27
}

public enum StockReportFieldType
{
    Text = 0,
    Year = 1,
    Decimal = 2,
    Date = 3,
    Flag = 4
}
