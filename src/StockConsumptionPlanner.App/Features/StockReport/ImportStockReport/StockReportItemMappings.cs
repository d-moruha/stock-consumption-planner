using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public static class StockReportItemMappings
{
    public static StockReportBatch ToBatch(this StockReportImportResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new StockReportBatch(
            result.SourceFileName,
            result.SheetName,
            result.ImportedAtUtc,
            result.Items.Select(ToDomain).ToList());
    }

    public static StockReportItem ToDomain(this StockReportItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new StockReportItem(
            dto.SourceRowNumber,
            dto.MaterialDocumentNumber,
            dto.MaterialDocumentItem,
            dto.MaterialDocumentYear,
            dto.MaterialNumber,
            dto.MaterialDescription,
            dto.TotalStockQuantity,
            dto.TotalStockValue)
        {
            WbsElement = dto.WbsElement,
            BatchNumber = dto.BatchNumber,
            StorageLocation = dto.StorageLocation,
            MovementType = dto.MovementType,
            BaseUnitOfMeasure = dto.BaseUnitOfMeasure,
            LocalCurrency = dto.LocalCurrency,
            SpecialStockIndicator = dto.SpecialStockIndicator,
            CrossPlantMaterialStatus = dto.CrossPlantMaterialStatus,
            PlantSpecificMaterialStatus = dto.PlantSpecificMaterialStatus,
            ClientLevelDeletionFlag = dto.ClientLevelDeletionFlag,
            PlantLevelDeletionFlag = dto.PlantLevelDeletionFlag,
            StockCreationDate = dto.StockCreationDate,
            StockType = dto.StockType,
            StockTypeDescription = dto.StockTypeDescription,
            PurchaseOrder = dto.PurchaseOrder,
            PurchaseOrderItem = dto.PurchaseOrderItem,
            PurchaseRequisition = dto.PurchaseRequisition,
            PurchaseRequisitionItem = dto.PurchaseRequisitionItem,
            GoodsReceiptDate = dto.GoodsReceiptDate,
            Vendor = dto.Vendor,
            VendorName = dto.VendorName
        };
    }

    public static StockReportItemDto ToDto(this StockReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new StockReportItemDto(
            item.SourceRowNumber,
            item.MaterialDocumentNumber,
            item.MaterialDocumentItem,
            item.MaterialDocumentYear,
            item.MaterialNumber,
            item.MaterialDescription,
            item.TotalStockQuantity,
            item.TotalStockValue)
        {
            WbsElement = item.WbsElement,
            BatchNumber = item.BatchNumber,
            StorageLocation = item.StorageLocation,
            MovementType = item.MovementType,
            BaseUnitOfMeasure = item.BaseUnitOfMeasure,
            LocalCurrency = item.LocalCurrency,
            SpecialStockIndicator = item.SpecialStockIndicator,
            CrossPlantMaterialStatus = item.CrossPlantMaterialStatus,
            PlantSpecificMaterialStatus = item.PlantSpecificMaterialStatus,
            ClientLevelDeletionFlag = item.ClientLevelDeletionFlag,
            PlantLevelDeletionFlag = item.PlantLevelDeletionFlag,
            StockCreationDate = item.StockCreationDate,
            StockType = item.StockType,
            StockTypeDescription = item.StockTypeDescription,
            PurchaseOrder = item.PurchaseOrder,
            PurchaseOrderItem = item.PurchaseOrderItem,
            PurchaseRequisition = item.PurchaseRequisition,
            PurchaseRequisitionItem = item.PurchaseRequisitionItem,
            GoodsReceiptDate = item.GoodsReceiptDate,
            Vendor = item.Vendor,
            VendorName = item.VendorName
        };
    }
}
