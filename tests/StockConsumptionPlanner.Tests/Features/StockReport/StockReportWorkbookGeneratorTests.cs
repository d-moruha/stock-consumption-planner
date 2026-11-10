using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockReportGenerator;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportWorkbookGeneratorTests
{
    private readonly ImportStockReportHandler _parser = new();
    private readonly InspectStockReportHandler _inspector = new();

    [Fact]
    public void Generate_FixedSeed_ImportsAllRowsAndRepeatsByteForByte()
    {
        StockReportGeneratorOptions options = new()
        {
            RowCount = 25,
            Seed = 42,
            HeaderLanguage = StockReportHeaderLanguage.English,
            SheetName = "Stock"
        };

        using MemoryStream first = new();
        StockReportWorkbookGenerator.Generate(first, options);
        byte[] firstBytes = first.ToArray();

        using MemoryStream second = new();
        StockReportWorkbookGenerator.Generate(second, options);
        Assert.Equal(firstBytes, second.ToArray());

        using MemoryStream parseStream = new(firstBytes, writable: false);
        StockReportImportResultDto result = _parser.Parse(parseStream, "generated.xlsx", IdentityMapping());

        Assert.Equal("Stock", result.SheetName);
        Assert.Equal(25, result.TotalRowsProcessed);
        Assert.Equal(25, result.ValidRowCount);
        Assert.Equal(0, result.ErrorRowCount);
        Assert.Empty(result.Errors);
        Assert.Equal(25, result.Items.Count);

        using MemoryStream readStream = new(firstBytes, writable: false);
        OpenXmlWorksheetData worksheet = ExcelWorksheetHelper.ReadWorksheet(readStream, "Stock");

        foreach (StockReportItemDto item in result.Items)
        {
            Assert.InRange(item.SourceRowNumber, 2, 26);
            Assert.Equal(CellText(worksheet, item, StockReportField.MaterialDocumentNumber), item.MaterialDocumentNumber);
            Assert.Matches("^[0-9]{10}$", item.MaterialDocumentNumber);
            Assert.Equal(CellText(worksheet, item, StockReportField.MaterialDocumentItem), item.MaterialDocumentItem);
            Assert.Matches("^[0-9]{4}$", item.MaterialDocumentItem);
            Assert.InRange(item.MaterialDocumentYear, 2020, 2026);
            Assert.Equal(CellText(worksheet, item, StockReportField.MaterialNumber), item.MaterialNumber);
            Assert.Matches("^[0-9]{12}$", item.MaterialNumber);
            Assert.False(string.IsNullOrWhiteSpace(item.MaterialDescription));
            Assert.True(item.TotalStockQuantity > 0m);
            Assert.True(item.TotalStockValue > 0m);
            Assert.Equal("UAH", item.LocalCurrency);
            Assert.Matches("^45[0-9]{8}$", item.PurchaseOrder!);
            Assert.Matches("^[0-9]{5}$", item.PurchaseOrderItem!);
            Assert.Matches("^(37|48)[0-9]{8}$", item.PurchaseRequisition!);
            Assert.Matches("^[0-9]{10}$", item.Vendor!);
            Assert.Equal(item.MaterialDocumentYear, item.GoodsReceiptDate!.Value.Year);
            Assert.Equal(item.SpecialStockIndicator == "Q", item.WbsElement is not null);
        }
    }

    [Theory]
    [InlineData(StockReportHeaderLanguage.English)]
    [InlineData(StockReportHeaderLanguage.Ukrainian)]
    public void Generate_HeaderLanguage_AutoMapsEveryField(StockReportHeaderLanguage headerLanguage)
    {
        StockReportGeneratorOptions options = new()
        {
            RowCount = 1,
            Seed = 7,
            HeaderLanguage = headerLanguage,
            SheetName = "Stock"
        };

        using MemoryStream stream = new();
        StockReportWorkbookGenerator.Generate(stream, options);

        StockReportInspectionDto inspection = _inspector.Inspect(stream, "generated.xlsx", "Stock");

        Assert.Equal(IdentityMapping().FieldColumnMap, inspection.SuggestedMapping);
    }

    [Fact]
    public void Parse_RowsZero_Throws()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockReportGeneratorArguments.Parse(["--rows", "0", "--output", "stock.xlsx"]));

        Assert.Equal("rows", exception.ParamName);
    }

    private static StockReportColumnMappingDto IdentityMapping()
    {
        return new StockReportColumnMappingDto
        {
            SheetName = "Stock",
            HeaderRowNumber = 1,
            DataStartRowNumber = 2,
            FieldColumnMap = StockReportFieldCatalog.All
                .Select((metadata, index) => (metadata.Field, Column: index + 1))
                .ToDictionary(pair => pair.Field, pair => pair.Column)
        };
    }

    private static string CellText(OpenXmlWorksheetData worksheet, StockReportItemDto item, StockReportField field)
    {
        return worksheet.GetCellString(item.SourceRowNumber, IdentityMapping().FieldColumnMap[field]);
    }
}
