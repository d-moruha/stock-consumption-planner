using ClosedXML.Excel;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class ClosedXmlStockReportServiceTests
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
        using MemoryStream stream = new();
        using (XLWorkbook wb = new())
        {
            IXLWorksheet ws = wb.Worksheets.Add("Sheet1");
            ws.Cell(1, 1).Value = "MaterialDocumentNumber";
            ws.Cell(1, 2).Value = "MaterialDocumentItem";
            ws.Cell(1, 3).Value = "MaterialDccoumentYear";
            ws.Cell(1, 4).Value = "MaterialNumber";
            ws.Cell(1, 5).Value = "MaterialDescription";
            ws.Cell(1, 6).Value = "MaterialQuantity";
            ws.Cell(1, 7).Value = "MaterialValue";

            ws.Cell(2, 1).Value = "4900012345";
            ws.Cell(2, 2).Value = "0001";
            ws.Cell(2, 3).Value = 2026;
            ws.Cell(2, 4).Value = "MAT-100";
            ws.Cell(2, 5).Value = "Кабель силовий";
            ws.Cell(2, 6).Value = 10.5;
            ws.Cell(2, 7).Value = 1250.75;

            wb.SaveAs(stream);
        }

        stream.Position = 0;
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
        using MemoryStream stream = new();
        using (XLWorkbook wb = new())
        {
            IXLWorksheet ws = wb.Worksheets.Add("Sheet1");
            ws.Cell(1, 1).Value = "MaterialDocumentNumber";
            ws.Cell(1, 2).Value = "MaterialDocumentItem";
            ws.Cell(1, 3).Value = "MaterialDocumentYear";
            ws.Cell(1, 4).Value = "MaterialNumber";
            ws.Cell(1, 5).Value = "MaterialDescription";
            ws.Cell(1, 6).Value = "MaterialQuantity";
            ws.Cell(1, 7).Value = "MaterialValue";

            ws.Cell(2, 1).Value = "50001";
            ws.Cell(2, 2).Value = "0001";
            ws.Cell(2, 3).Value = "2026";
            ws.Cell(2, 4).Value = "MAT-01";
            ws.Cell(2, 5).Value = "Valid item";
            ws.Cell(2, 6).Value = "10,25";
            ws.Cell(2, 7).Value = "100.50";

            ws.Cell(3, 1).Value = "50002";
            ws.Cell(3, 2).Value = "0002";
            ws.Cell(3, 3).Value = "bad-year";
            ws.Cell(3, 4).Value = "MAT-02";
            ws.Cell(3, 5).Value = "Invalid row";
            ws.Cell(3, 6).Value = "not-a-number";
            ws.Cell(3, 7).Value = "200,00";

            wb.SaveAs(stream);
        }

        stream.Position = 0;

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
        using XLWorkbook wb = new();
        wb.Worksheets.Add("Інфо");
        IXLWorksheet ws = wb.Worksheets.Add("Залишки");

        ws.Cell(1, 1).Value = "Звіт по поточним залишкам ТМЦ на складі";
        ws.Cell(2, 1).Value = "Дата формування: 24.10.2026";

        ws.Cell(4, 1).Value = "Склад (Ігнорувати)";
        ws.Cell(4, 2).Value = "Документ матеріалу";
        ws.Cell(4, 3).Value = "Позиція документа";
        ws.Cell(4, 4).Value = "Рік документа";
        ws.Cell(4, 5).Value = "Внутрішній коментар";
        ws.Cell(4, 6).Value = "Номенклатура";
        ws.Cell(4, 7).Value = "Найменування матеріалу";
        ws.Cell(4, 8).Value = "Кількість";
        ws.Cell(4, 9).Value = "Вартість";

        ws.Cell(5, 1).Value = "СКЛАД-01";
        ws.Cell(5, 2).SetValue("0049001234");
        ws.Cell(5, 3).SetValue("0001");
        ws.Cell(5, 4).SetValue(2026);
        ws.Cell(5, 5).Value = "Примітка 1 — не імпортувати";
        ws.Cell(5, 6).SetValue("000000105500");
        ws.Cell(5, 7).Value = "Труба сталева ДУ-50";
        ws.Cell(5, 8).SetValue("12,500");
        ws.Cell(5, 9).SetValue("15 400,75");

        ws.Cell(6, 1).Value = "СКЛАД-02";
        ws.Cell(6, 2).SetValue("0049001235");
        ws.Cell(6, 3).SetValue("0010");
        ws.Cell(6, 4).SetValue("2025");
        ws.Cell(6, 5).Value = "Примітка 2 — не імпортувати";
        ws.Cell(6, 6).SetValue("000000208800");
        ws.Cell(6, 7).Value = "Фланець плоский ПН16";
        ws.Cell(6, 8).SetValue(3.25);
        ws.Cell(6, 9).SetValue("4599.25");

        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
