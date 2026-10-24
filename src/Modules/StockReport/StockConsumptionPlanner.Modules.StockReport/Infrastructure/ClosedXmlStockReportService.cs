using System.Globalization;
using ClosedXML.Excel;
using StockConsumptionPlanner.Modules.StockReport.Application;
using StockConsumptionPlanner.Modules.StockReport.Contracts;

namespace StockConsumptionPlanner.Modules.StockReport.Infrastructure;

public sealed class ClosedXmlStockReportService : IStockReportExcelInspector, IStockReportExcelParser
{
    public StockReportInspectionDto Inspect(
        Stream excelStream,
        string fileName,
        string? sheetName = null,
        int headerRowNumber = 1,
        int? dataStartRowNumber = null)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        int resolvedHeaderRow = Math.Max(1, headerRowNumber);
        int resolvedDataStartRow = Math.Max(resolvedHeaderRow + 1, dataStartRowNumber ?? (resolvedHeaderRow + 1));

        using XLWorkbook workbook = new(excelStream);
        List<string> sheetNames = workbook.Worksheets.Select(ws => ws.Name).ToList();
        if (sheetNames.Count == 0)
        {
            throw new InvalidOperationException("The uploaded Excel workbook contains no worksheets.");
        }

        IXLWorksheet worksheet = ResolveWorksheet(workbook, sheetName);
        IXLRange? usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return new StockReportInspectionDto(
                fileName,
                sheetNames,
                worksheet.Name,
                resolvedHeaderRow,
                resolvedDataStartRow,
                0,
                [],
                new Dictionary<StockReportField, int>());
        }

        int firstCol = usedRange.FirstColumn().ColumnNumber();
        int lastCol = usedRange.LastColumn().ColumnNumber();
        int lastRow = usedRange.LastRow().RowNumber();

        List<DetectedExcelColumnDto> columns = new();
        for (int colIndex = firstCol; colIndex <= lastCol; colIndex++)
        {
            IXLCell headerCell = worksheet.Cell(resolvedHeaderRow, colIndex);
            string headerText = ReadCellDisplayString(headerCell);
            string colLetter = XLHelper.GetColumnLetterFromNumber(colIndex);

            List<string> samples = new();
            for (int rowIndex = resolvedDataStartRow; rowIndex <= lastRow && samples.Count < 3; rowIndex++)
            {
                string sampleValue = ReadCellDisplayString(worksheet.Cell(rowIndex, colIndex));
                if (!string.IsNullOrWhiteSpace(sampleValue))
                {
                    samples.Add(sampleValue);
                }
            }

            if (!string.IsNullOrWhiteSpace(headerText) || samples.Count > 0)
            {
                columns.Add(new DetectedExcelColumnDto(
                    colIndex,
                    colLetter,
                    string.IsNullOrWhiteSpace(headerText) ? $"Column {colLetter}" : headerText,
                    samples));
            }
        }

        Dictionary<StockReportField, int> suggestedMapping = SuggestMapping(columns);

        return new StockReportInspectionDto(
            fileName,
            sheetNames,
            worksheet.Name,
            resolvedHeaderRow,
            resolvedDataStartRow,
            lastRow,
            columns,
            suggestedMapping);
    }

    public StockReportImportResultDto Parse(
        Stream excelStream,
        string fileName,
        StockReportColumnMappingDto mapping)
    {
        ArgumentNullException.ThrowIfNull(excelStream);
        ArgumentNullException.ThrowIfNull(mapping);

        ValidateRequiredMappings(mapping);

        using XLWorkbook workbook = new(excelStream);
        IXLWorksheet worksheet = ResolveWorksheet(workbook, mapping.SheetName);
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

            string docNumber = ReadStringPreservingFormat(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialDocumentNumber]));

            string docItem = ReadStringPreservingFormat(
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

            string materialNumber = ReadStringPreservingFormat(
                worksheet.Cell(rowNumber, mapping.FieldColumnMap[StockReportField.MaterialNumber]));

            string materialDescription = ReadCellDisplayString(
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

    public static Dictionary<StockReportField, int> SuggestMapping(IReadOnlyList<DetectedExcelColumnDto> columns)
    {
        Dictionary<StockReportField, int> result = new();
        HashSet<int> assignedColumns = new();

        foreach (StockReportFieldMetadata metadata in StockReportFieldCatalog.All)
        {
            DetectedExcelColumnDto? exactMatch = columns.FirstOrDefault(col =>
            {
                if (assignedColumns.Contains(col.ColumnIndex))
                {
                    return false;
                }

                string normalizedHeader = StockReportFieldCatalog.NormalizeHeaderToken(col.HeaderText);
                return metadata.Aliases.Any(alias =>
                    string.Equals(normalizedHeader, alias, StringComparison.OrdinalIgnoreCase));
            });

            if (exactMatch is not null)
            {
                result[metadata.Field] = exactMatch.ColumnIndex;
                assignedColumns.Add(exactMatch.ColumnIndex);
            }
        }

        foreach (StockReportFieldMetadata metadata in StockReportFieldCatalog.All)
        {
            if (result.ContainsKey(metadata.Field))
            {
                continue;
            }

            DetectedExcelColumnDto? partialMatch = columns.FirstOrDefault(col =>
            {
                if (assignedColumns.Contains(col.ColumnIndex))
                {
                    return false;
                }

                string normalizedHeader = StockReportFieldCatalog.NormalizeHeaderToken(col.HeaderText);
                if (string.IsNullOrEmpty(normalizedHeader))
                {
                    return false;
                }

                return metadata.Aliases.Any(alias =>
                    alias.Length >= 3 &&
                    normalizedHeader.Contains(alias, StringComparison.OrdinalIgnoreCase));
            });

            if (partialMatch is not null)
            {
                result[metadata.Field] = partialMatch.ColumnIndex;
                assignedColumns.Add(partialMatch.ColumnIndex);
            }
        }

        return result;
    }

    private static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, string? requestedSheetName)
    {
        if (!string.IsNullOrWhiteSpace(requestedSheetName))
        {
            IXLWorksheet? match = workbook.Worksheets.FirstOrDefault(ws =>
                string.Equals(ws.Name, requestedSheetName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return workbook.Worksheets.First();
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
            if (!cell.IsEmpty() && !string.IsNullOrWhiteSpace(ReadCellDisplayString(cell)))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadStringPreservingFormat(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.Text)
        {
            return cell.GetString().Trim();
        }

        string formatted = cell.GetFormattedString().Trim();
        if (!string.IsNullOrEmpty(formatted))
        {
            return formatted;
        }

        return cell.Value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private static string ReadCellDisplayString(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        string formatted = cell.GetFormattedString().Trim();
        if (!string.IsNullOrEmpty(formatted))
        {
            return formatted;
        }

        return cell.Value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private static bool TryParseYear(IXLCell cell, out int year, out string? rawValue)
    {
        rawValue = ReadCellDisplayString(cell);
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
        rawValue = ReadCellDisplayString(cell);
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
