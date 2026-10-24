using System.Globalization;
using ClosedXML.Excel;
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

        using XLWorkbook workbook = new(excelStream);
        IXLWorksheet worksheet = ExcelWorksheetHelper.ResolveWorksheet(workbook, mapping.SheetName);
        IXLRange? usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return new StockReportImportResultDto(
                fileName,
                worksheet.Name,
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

        int firstCol = usedRange.FirstColumn().ColumnNumber();
        int lastCol = usedRange.LastColumn().ColumnNumber();
        int lastRow = usedRange.LastRow().RowNumber();

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
            int errorCountBeforeRow = errors.Count;

            string docNumber = ExcelWorksheetHelper.ReadStringPreservingFormat(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialDocumentNumber]));

            string docItem = ExcelWorksheetHelper.ReadStringPreservingFormat(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialDocumentItem]));

            IXLCell yearCell = worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialDocumentYear]);
            int docYear = 0;
            if (!TryParseYear(yearCell, out docYear, out string? rawYear))
            {
                errors.Add(new StockReportRowErrorDto(
                    rowNumber,
                    nameof(StockReportField.MaterialDocumentYear),
                    rawYear,
                    $"Invalid MaterialDocumentYear '{rawYear}'. Expected a 4-digit year."));
            }

            string materialNumber = ExcelWorksheetHelper.ReadStringPreservingFormat(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialNumber]));

            string materialDescription = ExcelWorksheetHelper.ReadCellDisplayString(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialDescription]));

            if (string.IsNullOrWhiteSpace(docNumber))
            {
                errors.Add(new StockReportRowErrorDto(
                    rowNumber,
                    nameof(StockReportField.MaterialDocumentNumber),
                    docNumber,
                    "MaterialDocumentNumber is required."));
            }

            if (string.IsNullOrWhiteSpace(materialNumber))
            {
                errors.Add(new StockReportRowErrorDto(
                    rowNumber,
                    nameof(StockReportField.MaterialNumber),
                    materialNumber,
                    "MaterialNumber is required."));
            }

            IXLCell qtyCell = worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialQuantity]);
            decimal quantity = 0m;
            if (!TryParseDecimal(qtyCell, out quantity, out string? rawQty))
            {
                errors.Add(new StockReportRowErrorDto(
                    rowNumber,
                    nameof(StockReportField.MaterialQuantity),
                    rawQty,
                    $"Invalid MaterialQuantity '{rawQty}'. Expected a valid decimal number."));
            }

            IXLCell valCell = worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialValue]);
            decimal value = 0m;
            if (!TryParseDecimal(valCell, out value, out string? rawVal))
            {
                errors.Add(new StockReportRowErrorDto(
                    rowNumber,
                    nameof(StockReportField.MaterialValue),
                    rawVal,
                    $"Invalid MaterialValue '{rawVal}'. Expected a valid decimal number."));
            }

            if (errors.Count == errorCountBeforeRow)
            {
                items.Add(new StockReportItemDto(
                    rowNumber,
                    docNumber,
                    docItem,
                    docYear,
                    materialNumber,
                    materialDescription,
                    quantity,
                    value));
            }
        }

        int errorRowCount = errors.Select(e => e.RowNumber).Distinct().Count();
        decimal totalQuantity = items.Sum(i => i.MaterialQuantity);
        decimal totalValue = items.Sum(i => i.MaterialValue);

        return new StockReportImportResultDto(
            fileName,
            worksheet.Name,
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

        foreach (StockReportFieldMetadata metadata in StockReportFieldCatalog.All)
        {
            if (!mapping.FieldColumnMap.TryGetValue(metadata.Field, out int colIndex) || colIndex <= 0)
            {
                throw new ArgumentException($"Missing or invalid column mapping for required field '{metadata.Key}'.");
            }
        }
    }

    private static bool IsMappedRowBlank(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<StockReportField, int> fieldColumnMap)
    {
        foreach (int colIndex in fieldColumnMap.Values)
        {
            IXLCell cell = worksheet.Cell(rowNumber, colIndex);
            if (!cell.IsEmpty() && !string.IsNullOrWhiteSpace(ExcelWorksheetHelper.ReadCellDisplayString(cell)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseYear(IXLCell cell, out int year, out string? rawValue)
    {
        rawValue = ExcelWorksheetHelper.ReadCellDisplayString(cell);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(rawValue))
        {
            year = 0;
            return false;
        }

        if (cell.DataType == XLDataType.Number)
        {
            double numeric = cell.GetDouble();
            int candidate = (int)Math.Round(numeric);
            if (candidate is >= 1900 and <= 2999)
            {
                year = candidate;
                return true;
            }
        }

        if (int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
            parsed is >= 1900 and <= 2999)
        {
            year = parsed;
            return true;
        }

        year = 0;
        return false;
    }

    private static bool TryParseDecimal(IXLCell cell, out decimal result, out string? rawValue)
    {
        rawValue = ExcelWorksheetHelper.ReadCellDisplayString(cell);
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(rawValue))
        {
            result = 0m;
            return false;
        }

        if (cell.DataType == XLDataType.Number)
        {
            result = Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);
            return true;
        }

        return TryParseFlexibleDecimalString(rawValue, out result);
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
