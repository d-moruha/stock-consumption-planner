using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

namespace StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;

public sealed record CurrentStockReportDto(
    bool HasReport,
    string? SourceFileName,
    string? SheetName,
    DateTimeOffset? ImportedAtUtc,
    int TotalRows,
    decimal TotalQuantity,
    decimal TotalValue,
    IReadOnlyList<StockReportItemDto> Items);
