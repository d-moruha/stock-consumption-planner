using StockConsumptionPlanner.Modules.StockReport.Contracts;

namespace StockConsumptionPlanner.Modules.StockReport.Application;

public interface IStockReportExcelInspector
{
    StockReportInspectionDto Inspect(
        Stream excelStream,
        string fileName,
        string? sheetName = null,
        int headerRowNumber = 1,
        int? dataStartRowNumber = null);
}

public interface IStockReportExcelParser
{
    StockReportImportResultDto Parse(
        Stream excelStream,
        string fileName,
        StockReportColumnMappingDto mapping);
}
