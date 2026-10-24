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
            Assert.Equal(worksheet.GetCellString(item.SourceRowNumber, 1), item.MaterialDocumentNumber);
            Assert.Matches("^[0-9]{10}$", item.MaterialDocumentNumber);
            Assert.Equal(worksheet.GetCellString(item.SourceRowNumber, 2), item.MaterialDocumentItem);
            Assert.Matches("^[0-9]{4}$", item.MaterialDocumentItem);
            Assert.InRange(item.MaterialDocumentYear, 2020, 2026);
            Assert.Equal(worksheet.GetCellString(item.SourceRowNumber, 4), item.MaterialNumber);
            Assert.Matches("^[0-9]{12}$", item.MaterialNumber);
            Assert.False(string.IsNullOrWhiteSpace(item.MaterialDescription));
            Assert.True(item.MaterialQuantity > 0m);
            Assert.True(item.MaterialValue > 0m);
        }
    }

    [Theory]
    [InlineData(StockReportHeaderLanguage.English)]
    [InlineData(StockReportHeaderLanguage.Ukrainian)]
    public void Generate_HeaderLanguage_AutoMapsAllSevenFields(StockReportHeaderLanguage headerLanguage)
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

        Assert.Equal(7, inspection.SuggestedMapping.Count);
        Assert.Equal(1, inspection.SuggestedMapping[StockReportField.MaterialDocumentNumber]);
        Assert.Equal(2, inspection.SuggestedMapping[StockReportField.MaterialDocumentItem]);
        Assert.Equal(3, inspection.SuggestedMapping[StockReportField.MaterialDocumentYear]);
        Assert.Equal(4, inspection.SuggestedMapping[StockReportField.MaterialNumber]);
        Assert.Equal(5, inspection.SuggestedMapping[StockReportField.MaterialDescription]);
        Assert.Equal(6, inspection.SuggestedMapping[StockReportField.MaterialQuantity]);
        Assert.Equal(7, inspection.SuggestedMapping[StockReportField.MaterialValue]);
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
    }
}
