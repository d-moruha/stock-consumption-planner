using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public sealed record OpenXmlCellData(
    string DisplayText,
    double? NumericValue);

public sealed class OpenXmlWorksheetData
{
    private readonly Dictionary<int, Dictionary<int, OpenXmlCellData>> _rows;

    public OpenXmlWorksheetData(
        string sheetName,
        IReadOnlyList<string> allSheetNames,
        Dictionary<int, Dictionary<int, OpenXmlCellData>> rows,
        int firstColumn,
        int lastColumn,
        int lastRow)
    {
        SheetName = sheetName;
        AllSheetNames = allSheetNames;
        _rows = rows;
        FirstColumn = firstColumn;
        LastColumn = lastColumn;
        LastRow = lastRow;
    }

    public string SheetName { get; }

    public IReadOnlyList<string> AllSheetNames { get; }

    public int FirstColumn { get; }

    public int LastColumn { get; }

    public int LastRow { get; }

    public bool HasData => LastRow > 0 && LastColumn >= FirstColumn && FirstColumn > 0;

    public OpenXmlCellData? GetCell(int rowNumber, int columnIndex)
    {
        if (_rows.TryGetValue(rowNumber, out Dictionary<int, OpenXmlCellData>? rowCells) &&
            rowCells.TryGetValue(columnIndex, out OpenXmlCellData? cell))
        {
            return cell;
        }

        return null;
    }

    public string GetCellString(int rowNumber, int columnIndex)
    {
        OpenXmlCellData? cell = GetCell(rowNumber, columnIndex);
        return cell?.DisplayText ?? string.Empty;
    }
}

public static class ExcelWorksheetHelper
{
    public static OpenXmlWorksheetData ReadWorksheet(Stream excelStream, string? requestedSheetName)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        using SpreadsheetDocument document = SpreadsheetDocument.Open(excelStream, false);
        WorkbookPart? workbookPart = document.WorkbookPart;
        Sheets? sheetsElement = workbookPart?.Workbook?.Sheets;

        if (workbookPart is null || sheetsElement is null)
        {
            throw new InvalidOperationException("The uploaded Excel workbook contains no worksheets.");
        }

        List<Sheet> sheets = sheetsElement.Elements<Sheet>()
            .Where(s => !string.IsNullOrWhiteSpace(s.Name?.Value) && !string.IsNullOrWhiteSpace(s.Id?.Value))
            .ToList();

        if (sheets.Count == 0)
        {
            throw new InvalidOperationException("The uploaded Excel workbook contains no worksheets.");
        }

        List<string> allSheetNames = sheets.Select(s => s.Name!.Value!).ToList();

        Sheet selectedSheet = ResolveSheet(sheets, requestedSheetName);
        string activeSheetName = selectedSheet.Name!.Value!;
        string relationshipId = selectedSheet.Id!.Value!;

        if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart ||
            worksheetPart.Worksheet is null)
        {
            return new OpenXmlWorksheetData(activeSheetName, allSheetNames, new(), 0, 0, 0);
        }

        string[] sharedStrings = ReadSharedStrings(workbookPart.SharedStringTablePart);
        SheetData? sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return new OpenXmlWorksheetData(activeSheetName, allSheetNames, new(), 0, 0, 0);
        }

        Dictionary<int, Dictionary<int, OpenXmlCellData>> rows = new();
        int minCol = int.MaxValue;
        int maxCol = 0;
        int maxRow = 0;

        int sequentialRowNumber = 0;
        foreach (Row row in sheetData.Elements<Row>())
        {
            sequentialRowNumber = row.RowIndex?.Value is uint explicitRow && explicitRow > 0
                ? (int)explicitRow
                : sequentialRowNumber + 1;

            Dictionary<int, OpenXmlCellData>? rowCells = null;
            int sequentialColIndex = 0;

            foreach (Cell cell in row.Elements<Cell>())
            {
                int colIndex = ResolveColumnIndex(cell.CellReference?.Value, sequentialColIndex + 1);
                sequentialColIndex = colIndex;

                OpenXmlCellData cellData = ExtractCellData(cell, sharedStrings);
                if (string.IsNullOrWhiteSpace(cellData.DisplayText) && cellData.NumericValue is null)
                {
                    continue;
                }

                rowCells ??= new Dictionary<int, OpenXmlCellData>();
                rowCells[colIndex] = cellData;

                if (colIndex < minCol)
                {
                    minCol = colIndex;
                }

                if (colIndex > maxCol)
                {
                    maxCol = colIndex;
                }
            }

            if (rowCells is not null && rowCells.Count > 0)
            {
                rows[sequentialRowNumber] = rowCells;
                if (sequentialRowNumber > maxRow)
                {
                    maxRow = sequentialRowNumber;
                }
            }
        }

        if (rows.Count == 0 || maxCol == 0)
        {
            return new OpenXmlWorksheetData(activeSheetName, allSheetNames, new(), 0, 0, 0);
        }

        return new OpenXmlWorksheetData(
            activeSheetName,
            allSheetNames,
            rows,
            minCol,
            maxCol,
            maxRow);
    }

    public static string GetColumnLetterFromNumber(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);

        StringBuilder builder = new();
        int dividend = columnIndex;

        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            builder.Insert(0, (char)('A' + modulo));
            dividend = (dividend - modulo) / 26;
        }

        return builder.ToString();
    }

    public static int ResolveColumnIndex(string? cellReference, int fallbackColumnIndex)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return fallbackColumnIndex;
        }

        int columnIndex = 0;
        foreach (char ch in cellReference)
        {
            if (char.IsAsciiLetter(ch))
            {
                columnIndex = (columnIndex * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else if (char.IsAsciiDigit(ch))
            {
                break;
            }
        }

        return columnIndex > 0 ? columnIndex : fallbackColumnIndex;
    }

    private static Sheet ResolveSheet(IReadOnlyList<Sheet> sheets, string? requestedSheetName)
    {
        if (!string.IsNullOrWhiteSpace(requestedSheetName))
        {
            Sheet? match = sheets.FirstOrDefault(s =>
                string.Equals(s.Name?.Value, requestedSheetName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return sheets[0];
    }

    private static string[] ReadSharedStrings(SharedStringTablePart? sharedStringPart)
    {
        if (sharedStringPart?.SharedStringTable is null)
        {
            return [];
        }

        return sharedStringPart.SharedStringTable
            .Elements<SharedStringItem>()
            .Select(item => item.InnerText ?? string.Empty)
            .ToArray();
    }

    private static OpenXmlCellData ExtractCellData(Cell cell, string[] sharedStrings)
    {
        CellValues? dataType = cell.DataType?.Value;

        if (dataType == CellValues.SharedString)
        {
            string rawIndex = cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty;
            if (int.TryParse(rawIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sstIndex) &&
                sstIndex >= 0 && sstIndex < sharedStrings.Length)
            {
                return new OpenXmlCellData(sharedStrings[sstIndex].Trim(), null);
            }

            return new OpenXmlCellData(string.Empty, null);
        }

        if (dataType == CellValues.InlineString)
        {
            string inlineText = cell.InlineString?.InnerText ?? cell.InnerText ?? string.Empty;
            return new OpenXmlCellData(inlineText.Trim(), null);
        }

        if (dataType == CellValues.String)
        {
            string strText = cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty;
            return new OpenXmlCellData(strText.Trim(), null);
        }

        if (dataType == CellValues.Boolean)
        {
            string rawBool = (cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty).Trim();
            string normalizedBool = rawBool switch
            {
                "1" => "TRUE",
                "0" => "FALSE",
                _ => rawBool
            };
            return new OpenXmlCellData(normalizedBool, null);
        }

        string rawValue = (cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(rawValue))
        {
            return new OpenXmlCellData(string.Empty, null);
        }

        if ((dataType is null || dataType == CellValues.Number) &&
            double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double numericValue))
        {
            return new OpenXmlCellData(rawValue, numericValue);
        }

        return new OpenXmlCellData(rawValue, null);
    }
}
