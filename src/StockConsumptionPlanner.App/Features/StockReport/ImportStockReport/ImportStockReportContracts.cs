using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public sealed class StockReportColumnMappingDto
{
    public string? SheetName { get; set; }

    public int HeaderRowNumber { get; set; } = 1;

    public int DataStartRowNumber { get; set; } = 2;

    public Dictionary<StockReportField, int> FieldColumnMap { get; set; } = new();
}

public sealed record StockReportItemDto(
    int SourceRowNumber,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int MaterialDocumentYear,
    string MaterialNumber,
    string MaterialDescription,
    decimal MaterialQuantity,
    decimal MaterialValue);

public sealed record StockReportRowErrorDto(
    int RowNumber,
    string Field,
    string? RawValue,
    string Message);

public sealed record StockReportImportResultDto(
    string SourceFileName,
    string SheetName,
    DateTimeOffset ImportedAtUtc,
    int TotalRowsProcessed,
    int ValidRowCount,
    int ErrorRowCount,
    decimal TotalQuantity,
    decimal TotalValue,
    IReadOnlyList<int> IgnoredColumnIndices,
    IReadOnlyList<StockReportItemDto> Items,
    IReadOnlyList<StockReportRowErrorDto> Errors);
