using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class SapStockReportImportTests
{
    private readonly InspectStockReportHandler _inspector = new();
    private readonly ImportStockReportHandler _parser = new();

    [Fact]
    public void InspectAndParse_SapExport_ReadsEveryColumnWithTypes()
    {
        using MemoryStream inspectStream = CreateSapWorkbook();
        StockReportInspectionDto inspection = _inspector.Inspect(inspectStream, "mb51.xlsx");
        Assert.Equal(StockReportFieldCatalog.All.Count, inspection.SuggestedMapping.Count);

        using MemoryStream parseStream = CreateSapWorkbook();
        StockReportImportResultDto result = _parser.Parse(parseStream, "mb51.xlsx", MappingFrom(inspection));

        Assert.Empty(result.Errors);
        StockReportItemDto item = Assert.Single(result.Items);
        Assert.Equal("4900001234", item.MaterialDocumentNumber);
        Assert.Equal("0001", item.MaterialDocumentItem);
        Assert.Equal(2026, item.MaterialDocumentYear);
        Assert.Equal("UA-01-0001.01", item.WbsElement);
        Assert.Equal("0000012345", item.BatchNumber);
        Assert.Equal("000000000010000123", item.MaterialNumber);
        Assert.Equal("Труба сталева ДУ-50", item.MaterialDescription);
        Assert.Equal("1000", item.StorageLocation);
        Assert.Equal("101", item.MovementType);
        Assert.Equal(1234.5m, item.TotalStockQuantity);
        Assert.Equal("М", item.BaseUnitOfMeasure);
        Assert.Equal(15400.75m, item.TotalStockValue);
        Assert.Equal("UAH", item.LocalCurrency);
        Assert.Equal("Q", item.SpecialStockIndicator);
        Assert.Equal("Z1", item.CrossPlantMaterialStatus);
        Assert.Null(item.PlantSpecificMaterialStatus);
        Assert.True(item.ClientLevelDeletionFlag);
        Assert.False(item.PlantLevelDeletionFlag);
        Assert.Equal(new DateOnly(2026, 10, 24), item.StockCreationDate);
        Assert.Equal("F", item.StockType);
        Assert.Equal("Вільний для використання", item.StockTypeDescription);
        Assert.Equal("4500000123", item.PurchaseOrder);
        Assert.Equal("00010", item.PurchaseOrderItem);
        Assert.Equal("3700001234", item.PurchaseRequisition);
        Assert.Equal("00020", item.PurchaseRequisitionItem);
        Assert.Equal(new DateOnly(2026, 10, 20), item.GoodsReceiptDate);
        Assert.Equal("0000100234", item.Vendor);
        Assert.Equal("ТОВ «Металопрокат»", item.VendorName);
    }

    [Fact]
    public void Parse_InvalidDateFlagAndOverlongText_CollectsRowErrors()
    {
        using MemoryStream stream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
            "Sheet1",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] = Headers(),
                [2] = RequiredValues("4900000001").Concat(
                [
                    (9, "32.13.2026"),
                    (10, "maybe"),
                    (11, new string('W', StockReportFieldCatalog.CodeMaxLength + 1))
                ]).ToList(),
                [3] = RequiredValues("4900000002").Concat(
                [
                    (9, "00.00.0000"),
                    (10, "X")
                ]).ToList()
            });

        StockReportImportResultDto result = _parser.Parse(stream, "errors.xlsx", OptionalMapping());

        Assert.Equal(3, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(2, error.RowNumber));
        Assert.Equal(
            ["ClientLevelDeletionFlag", "StockCreationDate", "WbsElement"],
            result.Errors.Select(error => error.Field).Order().ToArray());

        StockReportItemDto valid = Assert.Single(result.Items);
        Assert.Equal("4900000002", valid.MaterialDocumentNumber);
        Assert.Null(valid.StockCreationDate);
        Assert.True(valid.ClientLevelDeletionFlag);
        Assert.Null(valid.WbsElement);
    }

    [Fact]
    public void Parse_OptionalFieldsUnmapped_ImportsRequiredFieldsOnly()
    {
        using MemoryStream stream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
            "Sheet1",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] = Headers(),
                [2] = RequiredValues("4900000001")
            });

        StockReportImportResultDto result = _parser.Parse(stream, "required.xlsx", RequiredMapping());

        StockReportItemDto item = Assert.Single(result.Items);
        Assert.Null(item.WbsElement);
        Assert.Null(item.PurchaseOrder);
        Assert.Null(item.GoodsReceiptDate);
        Assert.False(item.PlantLevelDeletionFlag);
    }

    [Fact]
    public void Parse_RequiredFieldUnmapped_Throws()
    {
        using MemoryStream stream = OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
            "Sheet1",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] = Headers(),
                [2] = RequiredValues("4900000001")
            });

        StockReportColumnMappingDto mapping = RequiredMapping();
        mapping.FieldColumnMap.Remove(StockReportField.TotalStockValue);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _parser.Parse(stream, "required.xlsx", mapping));
        Assert.Contains("TotalStockValue", exception.Message);
    }

    private static MemoryStream CreateSapWorkbook()
    {
        object[] values =
        [
            "4900001234", "0001", 2026, "UA-01-0001.01", "0000012345", "000000000010000123",
            "Труба сталева ДУ-50", "1000", "101", "1 234,500", "М", "15 400,75", "UAH", "Q", "Z1", "",
            "X", "", "24.10.2026", "F", "Вільний для використання", "4500000123", "00010", "3700001234",
            "00020", new DateTime(2026, 10, 20).ToOADate(), "0000100234", "ТОВ «Металопрокат»"
        ];

        return OpenXmlStockReportServiceTests.CreateSingleSheetWorkbook(
            "MB51",
            new Dictionary<int, IReadOnlyList<(int Column, object Value)>>
            {
                [1] = StockReportHeaderMatcherTests.SapUkrainianHeaders
                    .Select((header, index) => (index + 1, (object)header))
                    .ToList(),
                [2] = values
                    .Select((value, index) => (index + 1, value))
                    .Where(cell => cell.value is not "")
                    .ToList()
            });
    }

    private static StockReportColumnMappingDto MappingFrom(StockReportInspectionDto inspection)
    {
        return new StockReportColumnMappingDto
        {
            HeaderRowNumber = inspection.HeaderRowNumber,
            DataStartRowNumber = inspection.DataStartRowNumber,
            FieldColumnMap = new Dictionary<StockReportField, int>(inspection.SuggestedMapping)
        };
    }

    private static List<(int Column, object Value)> Headers()
    {
        return
        [
            (1, "MaterialDocumentNumber"),
            (2, "MaterialDocumentItem"),
            (3, "MaterialDocumentYear"),
            (4, "MaterialNumber"),
            (5, "MaterialDescription"),
            (6, "TotalStockQuantity"),
            (7, "TotalStockValue"),
            (9, "StockCreationDate"),
            (10, "ClientLevelDeletionFlag"),
            (11, "WbsElement")
        ];
    }

    private static List<(int Column, object Value)> RequiredValues(string documentNumber)
    {
        return
        [
            (1, documentNumber),
            (2, "0001"),
            (3, 2026),
            (4, "MAT-1"),
            (5, "Material"),
            (6, 1.5),
            (7, 10.25)
        ];
    }

    private static StockReportColumnMappingDto RequiredMapping()
    {
        return new StockReportColumnMappingDto
        {
            FieldColumnMap = new Dictionary<StockReportField, int>
            {
                [StockReportField.MaterialDocumentNumber] = 1,
                [StockReportField.MaterialDocumentItem] = 2,
                [StockReportField.MaterialDocumentYear] = 3,
                [StockReportField.MaterialNumber] = 4,
                [StockReportField.MaterialDescription] = 5,
                [StockReportField.TotalStockQuantity] = 6,
                [StockReportField.TotalStockValue] = 7
            }
        };
    }

    private static StockReportColumnMappingDto OptionalMapping()
    {
        StockReportColumnMappingDto mapping = RequiredMapping();
        mapping.FieldColumnMap[StockReportField.StockCreationDate] = 9;
        mapping.FieldColumnMap[StockReportField.ClientLevelDeletionFlag] = 10;
        mapping.FieldColumnMap[StockReportField.WbsElement] = 11;
        return mapping;
    }
}
