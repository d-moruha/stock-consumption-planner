using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;

public sealed record DetectedExcelColumnDto(
    int ColumnIndex,
    string ColumnLetter,
    string HeaderText,
    IReadOnlyList<string> SampleValues);

public sealed record StockReportInspectionDto(
    string FileName,
    IReadOnlyList<string> SheetNames,
    string ActiveSheetName,
    int HeaderRowNumber,
    int DataStartRowNumber,
    int TotalPopulatedRows,
    IReadOnlyList<DetectedExcelColumnDto> Columns,
    IReadOnlyDictionary<StockReportField, int> SuggestedMapping);
