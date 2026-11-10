using System.Globalization;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public interface IStockReportExcelParser
{
    StockReportImportResultDto Parse(
        Stream excelStream,
        string fileName,
        StockReportColumnMappingDto mapping);
}

public sealed class ImportStockReportHandler : IStockReportExcelParser
{
    public StockReportImportResultDto Parse(
        Stream excelStream,
        string fileName,
        StockReportColumnMappingDto mapping)
    {
        ArgumentNullException.ThrowIfNull(excelStream);
        ArgumentNullException.ThrowIfNull(mapping);

        ValidateRequiredMappings(mapping);

        OpenXmlWorksheetData worksheet = ExcelWorksheetHelper.ReadWorksheet(excelStream, mapping.SheetName);
        if (!worksheet.HasData)
        {
            return new StockReportImportResultDto(
                fileName,
                worksheet.SheetName,
                DateTimeOffset.UtcNow,
                0,
                0,
                0,
                0m,
                0m,
                [],
                [],
                []);
        }

        int firstCol = worksheet.FirstColumn;
        int lastCol = worksheet.LastColumn;
        int lastRow = worksheet.LastRow;

        int headerRow = Math.Max(1, mapping.HeaderRowNumber);
        int startRow = Math.Max(headerRow + 1, mapping.DataStartRowNumber);

        HashSet<int> mappedColumns = mapping.FieldColumnMap.Values.ToHashSet();
        List<int> ignoredColumns = Enumerable.Range(firstCol, Math.Max(0, lastCol - firstCol + 1))
            .Where(col => !mappedColumns.Contains(col))
            .ToList();

        List<StockReportItemDto> items = new();
        List<StockReportRowErrorDto> errors = new();
        int totalRowsProcessed = 0;

        for (int rowNumber = startRow; rowNumber <= lastRow; rowNumber++)
        {
            if (IsMappedRowBlank(worksheet, rowNumber, mapping.FieldColumnMap))
            {
                continue;
            }

            totalRowsProcessed++;
            StockReportRowReader row = new(worksheet, rowNumber, mapping.FieldColumnMap);
            StockReportItemDto item = ReadItem(row);

            if (row.Errors.Count == 0)
            {
                items.Add(item);
            }
            else
            {
                errors.AddRange(row.Errors);
            }
        }

        int errorRowCount = errors.Select(e => e.RowNumber).Distinct().Count();
        decimal totalQuantity = items.Sum(i => i.TotalStockQuantity);
        decimal totalValue = items.Sum(i => i.TotalStockValue);

        return new StockReportImportResultDto(
            fileName,
            worksheet.SheetName,
            DateTimeOffset.UtcNow,
            totalRowsProcessed,
            items.Count,
            errorRowCount,
            totalQuantity,
            totalValue,
            ignoredColumns,
            items,
            errors);
    }

    private static void ValidateRequiredMappings(StockReportColumnMappingDto mapping)
    {
        if (mapping.FieldColumnMap is null || mapping.FieldColumnMap.Count == 0)
        {
            throw new ArgumentException("FieldColumnMap must specify column mappings for the target fields.");
        }

        foreach (StockReportFieldMetadata metadata in StockReportFieldCatalog.Required)
        {
            if (!mapping.FieldColumnMap.ContainsKey(metadata.Field))
            {
                throw new ArgumentException($"Missing column mapping for required field '{metadata.Key}'.");
            }
        }

        foreach (KeyValuePair<StockReportField, int> pair in mapping.FieldColumnMap)
        {
            if (pair.Value <= 0)
            {
                throw new ArgumentException($"Invalid column mapping for field '{pair.Key}'.");
            }
        }
    }

    private static StockReportItemDto ReadItem(StockReportRowReader row)
    {
        return new StockReportItemDto(
            row.RowNumber,
            row.ReadRequiredText(StockReportField.MaterialDocumentNumber),
            row.ReadText(StockReportField.MaterialDocumentItem),
            row.ReadYear(StockReportField.MaterialDocumentYear),
            row.ReadRequiredText(StockReportField.MaterialNumber),
            row.ReadText(StockReportField.MaterialDescription),
            row.ReadDecimal(StockReportField.TotalStockQuantity),
            row.ReadDecimal(StockReportField.TotalStockValue))
        {
            WbsElement = row.ReadOptionalText(StockReportField.WbsElement),
            BatchNumber = row.ReadOptionalText(StockReportField.BatchNumber),
            StorageLocation = row.ReadOptionalText(StockReportField.StorageLocation),
            MovementType = row.ReadOptionalText(StockReportField.MovementType),
            BaseUnitOfMeasure = row.ReadOptionalText(StockReportField.BaseUnitOfMeasure),
            LocalCurrency = row.ReadOptionalText(StockReportField.LocalCurrency),
            SpecialStockIndicator = row.ReadOptionalText(StockReportField.SpecialStockIndicator),
            CrossPlantMaterialStatus = row.ReadOptionalText(StockReportField.CrossPlantMaterialStatus),
            PlantSpecificMaterialStatus = row.ReadOptionalText(StockReportField.PlantSpecificMaterialStatus),
            ClientLevelDeletionFlag = row.ReadFlag(StockReportField.ClientLevelDeletionFlag),
            PlantLevelDeletionFlag = row.ReadFlag(StockReportField.PlantLevelDeletionFlag),
            StockCreationDate = row.ReadDate(StockReportField.StockCreationDate),
            StockType = row.ReadOptionalText(StockReportField.StockType),
            StockTypeDescription = row.ReadOptionalText(StockReportField.StockTypeDescription),
            PurchaseOrder = row.ReadOptionalText(StockReportField.PurchaseOrder),
            PurchaseOrderItem = row.ReadOptionalText(StockReportField.PurchaseOrderItem),
            PurchaseRequisition = row.ReadOptionalText(StockReportField.PurchaseRequisition),
            PurchaseRequisitionItem = row.ReadOptionalText(StockReportField.PurchaseRequisitionItem),
            GoodsReceiptDate = row.ReadDate(StockReportField.GoodsReceiptDate),
            Vendor = row.ReadOptionalText(StockReportField.Vendor),
            VendorName = row.ReadOptionalText(StockReportField.VendorName)
        };
    }

    private static bool IsMappedRowBlank(
        OpenXmlWorksheetData worksheet,
        int rowNumber,
        IReadOnlyDictionary<StockReportField, int> fieldColumnMap)
    {
        foreach (int colIndex in fieldColumnMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.GetCellString(rowNumber, colIndex)))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParseFlexibleDecimalString(string? rawInput, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return false;
        }

        string cleaned = rawInput
            .Trim()
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        int lastComma = cleaned.LastIndexOf(',');
        int lastDot = cleaned.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
            {
                cleaned = cleaned.Replace(".", string.Empty, StringComparison.Ordinal)
                    .Replace(',', '.');
            }
            else
            {
                cleaned = cleaned.Replace(",", string.Empty, StringComparison.Ordinal);
            }
        }
        else if (lastComma >= 0)
        {
            cleaned = cleaned.Replace(',', '.');
        }

        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}
