using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class OpenXmlStockReportServiceTests
{
    private readonly InspectStockReportHandler _inspector = new();
    private readonly ImportStockReportHandler _parser = new();

    [Fact]
    public void Inspect_UkrainianHeadersWithCustomHeaderRow_AutoSuggestsAllSevenFields()
    {
        using MemoryStream stream = CreateUkrainianWorkbookWithPreamble();

        StockReportInspectionDto inspection = _inspector.Inspect(
            stream,
            "zvit_zalushki.xlsx",
            sheetName: "Залишки",
            headerRowNumber: 4,
            dataStartRowNumber: 5);

        Assert.Equal("zvit_zalushki.xlsx", inspection.FileName);
        Assert.Equal("Залишки", inspection.ActiveSheetName);
        Assert.Contains("Залишки", inspection.SheetNames);
        Assert.Equal(4, inspection.HeaderRowNumber);
        Assert.Equal(5, inspection.DataStartRowNumber);
        Assert.Equal(9, inspection.Columns.Count);

        Assert.Equal(2, inspection.SuggestedMapping[StockReportField.MaterialDocumentNumber]);
        Assert.Equal(3, inspection.SuggestedMapping[StockReportField.MaterialDocumentItem]);
        Assert.Equal(4, inspection.SuggestedMapping[StockReportField.MaterialDocumentYear]);
        Assert.Equal(6, inspection.SuggestedMapping[StockReportField.MaterialNumber]);
        Assert.Equal(7, inspection.SuggestedMapping[StockReportField.MaterialDescription]);
        Assert.Equal(8, inspection.SuggestedMapping[StockReportField.MaterialQuantity]);
        Assert.Equal(9, inspection.SuggestedMapping[StockReportField.MaterialValue]);
    }

    [Fact]
    public void Inspect_MaterialDccoumentYearTypoHeader_AutoSuggestsMaterialDocumentYear()
    {
        using MemoryStream stream = CreateSingleSheetWorkbook(
            "Sheet1",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] =
                [
                    (1, "MaterialDocumentNumber"),
                    (2, "MaterialDocumentItem"),
                    (3, "MaterialDccoumentYear"),
                    (4, "MaterialNumber"),
                    (5, "MaterialDescription"),
                    (6, "MaterialQuantity"),
                    (7, "MaterialValue")
                ],
                [2] =
                [
                    (1, "4900012345"),
                    (2, "0001"),
                    (3, 2026),
                    (4, "MAT-100"),
                    (5, "Кабель силовий"),
                    (6, 10.5),
                    (7, 1250.75)
                ]
            });

        StockReportInspectionDto inspection = _inspector.Inspect(stream, "english_typo.xlsx");

        Assert.Equal(3, inspection.SuggestedMapping[StockReportField.MaterialDocumentYear]);
    }

    [Fact]
    public void Parse_MapsOnlyConfiguredColumns_IgnoresUnmappedColumns_PreservesLeadingZerosAndDecimalSeparators()
    {
        using MemoryStream stream = CreateUkrainianWorkbookWithPreamble();

        StockReportColumnMappingDto mapping = new()
        {
            SheetName = "Залишки",
            HeaderRowNumber = 4,
            DataStartRowNumber = 5,
            FieldColumnMap = new Dictionary<StockReportField, int>
            {
                [StockReportField.MaterialDocumentNumber] = 2,
                [StockReportField.MaterialDocumentItem] = 3,
                [StockReportField.MaterialDocumentYear] = 4,
                [StockReportField.MaterialNumber] = 6,
                [StockReportField.MaterialDescription] = 7,
                [StockReportField.MaterialQuantity] = 8,
                [StockReportField.MaterialValue] = 9
            }
        };

        StockReportImportResultDto result = _parser.Parse(stream, "zvit_zalushki.xlsx", mapping);

        Assert.Equal(2, result.TotalRowsProcessed);
        Assert.Equal(2, result.ValidRowCount);
        Assert.Equal(0, result.ErrorRowCount);
        Assert.Empty(result.Errors);

        Assert.Contains(1, result.IgnoredColumnIndices);
        Assert.Contains(5, result.IgnoredColumnIndices);
        Assert.Equal(2, result.IgnoredColumnIndices.Count);

        StockReportItemDto first = result.Items[0];
        Assert.Equal(5, first.SourceRowNumber);
        Assert.Equal("0049001234", first.MaterialDocumentNumber);
        Assert.Equal("0001", first.MaterialDocumentItem);
        Assert.Equal(2026, first.MaterialDocumentYear);
        Assert.Equal("000000105500", first.MaterialNumber);
        Assert.Equal("Труба сталева ДУ-50", first.MaterialDescription);
        Assert.Equal(12.500m, first.MaterialQuantity);
        Assert.Equal(15400.75m, first.MaterialValue);

        StockReportItemDto second = result.Items[1];
        Assert.Equal(6, second.SourceRowNumber);
        Assert.Equal("0049001235", second.MaterialDocumentNumber);
        Assert.Equal("0010", second.MaterialDocumentItem);
        Assert.Equal(2025, second.MaterialDocumentYear);
        Assert.Equal("000000208800", second.MaterialNumber);
        Assert.Equal("Фланець плоский ПН16", second.MaterialDescription);
        Assert.Equal(3.250m, second.MaterialQuantity);
        Assert.Equal(4599.25m, second.MaterialValue);

        Assert.Equal(15.750m, result.TotalQuantity);
        Assert.Equal(20000.00m, result.TotalValue);
    }

    [Fact]
    public void Parse_RowWithMalformedNumbers_CollectsRowErrorAndKeepsValidRows()
    {
        using MemoryStream stream = CreateSingleSheetWorkbook(
            "Sheet1",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] =
                [
                    (1, "MaterialDocumentNumber"),
                    (2, "MaterialDocumentItem"),
                    (3, "MaterialDocumentYear"),
                    (4, "MaterialNumber"),
                    (5, "MaterialDescription"),
                    (6, "MaterialQuantity"),
                    (7, "MaterialValue")
                ],
                [2] =
                [
                    (1, "50001"),
                    (2, "0001"),
                    (3, "2026"),
                    (4, "MAT-01"),
                    (5, "Valid item"),
                    (6, "10,25"),
                    (7, "100.50")
                ],
                [3] =
                [
                    (1, "50002"),
                    (2, "0002"),
                    (3, "bad-year"),
                    (4, "MAT-02"),
                    (5, "Invalid row"),
                    (6, "not-a-number"),
                    (7, "200,00")
                ]
            });

        StockReportColumnMappingDto mapping = new()
        {
            HeaderRowNumber = 1,
            DataStartRowNumber = 2,
            FieldColumnMap = new Dictionary<StockReportField, int>
            {
                [StockReportField.MaterialDocumentNumber] = 1,
                [StockReportField.MaterialDocumentItem] = 2,
                [StockReportField.MaterialDocumentYear] = 3,
                [StockReportField.MaterialNumber] = 4,
                [StockReportField.MaterialDescription] = 5,
                [StockReportField.MaterialQuantity] = 6,
                [StockReportField.MaterialValue] = 7
            }
        };

        StockReportImportResultDto result = _parser.Parse(stream, "errors.xlsx", mapping);

        Assert.Equal(2, result.TotalRowsProcessed);
        Assert.Equal(1, result.ValidRowCount);
        Assert.Equal(1, result.ErrorRowCount);
        Assert.Equal(2, result.Errors.Count);
        Assert.All(result.Errors, err => Assert.Equal(3, err.RowNumber));
    }

    public static MemoryStream CreateUkrainianWorkbookWithPreamble()
    {
        MemoryStream stream = new();
        using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());

            SharedStringTablePart sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sstPart.SharedStringTable = new SharedStringTable();
            Dictionary<string, int> sharedStringIndexes = new(StringComparer.Ordinal);

            WorksheetPart infoPart = workbookPart.AddNewPart<WorksheetPart>();
            infoPart.Worksheet = new Worksheet(new SheetData());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(infoPart),
                SheetId = 1U,
                Name = "Інфо"
            });

            WorksheetPart stockPart = workbookPart.AddNewPart<WorksheetPart>();
            SheetData sheetData = new();
            stockPart.Worksheet = new Worksheet(sheetData);
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(stockPart),
                SheetId = 2U,
                Name = "Залишки"
            });

            Dictionary<int, IReadOnlyList<(int Column, object Value)>> rows = new()
            {
                [1] = [(1, "Звіт по поточним залишкам ТМЦ на складі")],
                [2] = [(1, "Дата формування: 24.10.2026")],
                [4] =
                [
                    (1, "Склад (Ігнорувати)"),
                    (2, "Документ матеріалу"),
                    (3, "Позиція документа"),
                    (4, "Рік документа"),
                    (5, "Внутрішній коментар"),
                    (6, "Номенклатура"),
                    (7, "Найменування матеріалу"),
                    (8, "Кількість"),
                    (9, "Вартість")
                ],
                [5] =
                [
                    (1, "СКЛАД-01"),
                    (2, "0049001234"),
                    (3, "0001"),
                    (4, 2026),
                    (5, "Примітка 1 — не імпортувати"),
                    (6, "000000105500"),
                    (7, "Труба сталева ДУ-50"),
                    (8, "12,500"),
                    (9, "15 400,75")
                ],
                [6] =
                [
                    (1, "СКЛАД-02"),
                    (2, "0049001235"),
                    (3, "0010"),
                    (4, "2025"),
                    (5, "Примітка 2 — не імпортувати"),
                    (6, "000000208800"),
                    (7, "Фланець плоский ПН16"),
                    (8, 3.25),
                    (9, "4599.25")
                ]
            };

            PopulateSheetData(sheetData, sstPart.SharedStringTable, sharedStringIndexes, rows);
            sstPart.SharedStringTable.Save();
            stockPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream CreateSingleSheetWorkbook(
        string sheetName,
        IReadOnlyDictionary<int, IReadOnlyList<(int Column, object Value)>> rows)
    {
        MemoryStream stream = new();
        using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());

            SharedStringTablePart sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sstPart.SharedStringTable = new SharedStringTable();
            Dictionary<string, int> sharedStringIndexes = new(StringComparer.Ordinal);

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            SheetData sheetData = new();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = sheetName
            });

            PopulateSheetData(sheetData, sstPart.SharedStringTable, sharedStringIndexes, rows);
            sstPart.SharedStringTable.Save();
            worksheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static void PopulateSheetData(
        SheetData sheetData,
        SharedStringTable sharedStringTable,
        Dictionary<string, int> sharedStringIndexes,
        IReadOnlyDictionary<int, IReadOnlyList<(int Column, object Value)>> rows)
    {
        foreach (KeyValuePair<int, IReadOnlyList<(int Column, object Value)>> rowEntry in rows.OrderBy(r => r.Key))
        {
            int rowNumber = rowEntry.Key;
            Row row = new() { RowIndex = (uint)rowNumber };

            foreach ((int columnIndex, object value) in rowEntry.Value.OrderBy(c => c.Column))
            {
                string cellRef = $"{ExcelWorksheetHelper.GetColumnLetterFromNumber(columnIndex)}{rowNumber}";
                Cell cell = new() { CellReference = cellRef };

                switch (value)
                {
                    case int intVal:
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(intVal.ToString(CultureInfo.InvariantCulture));
                        break;

                    case double doubleVal:
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(doubleVal.ToString(CultureInfo.InvariantCulture));
                        break;

                    case decimal decimalVal:
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(decimalVal.ToString(CultureInfo.InvariantCulture));
                        break;

                    default:
                        string text = value.ToString() ?? string.Empty;
                        if (!sharedStringIndexes.TryGetValue(text, out int sstIndex))
                        {
                            sstIndex = sharedStringIndexes.Count;
                            sharedStringIndexes[text] = sstIndex;
                            sharedStringTable.AppendChild(new SharedStringItem(new Text(text)));
                        }

                        cell.DataType = CellValues.SharedString;
                        cell.CellValue = new CellValue(sstIndex.ToString(CultureInfo.InvariantCulture));
                        break;
                }

                row.Append(cell);
            }

            sheetData.Append(row);
        }
    }
}
