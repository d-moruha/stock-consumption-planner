namespace StockConsumptionPlanner.Modules.StockReport.Contracts;

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

public sealed record CurrentStockReportDto(
    bool HasReport,
    string? SourceFileName,
    string? SheetName,
    DateTimeOffset? ImportedAtUtc,
    int TotalRows,
    decimal TotalQuantity,
    decimal TotalValue,
    IReadOnlyList<StockReportItemDto> Items);
