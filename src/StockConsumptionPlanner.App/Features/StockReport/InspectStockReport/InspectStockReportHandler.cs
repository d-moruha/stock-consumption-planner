using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;

public interface IStockReportExcelInspector
{
    StockReportInspectionDto Inspect(
        Stream excelStream,
        string fileName,
        string? sheetName = null,
        int headerRowNumber = 1,
        int? dataStartRowNumber = null);
}

public sealed class InspectStockReportHandler : IStockReportExcelInspector
{
    public StockReportInspectionDto Inspect(
        Stream excelStream,
        string fileName,
        string? sheetName = null,
        int headerRowNumber = 1,
        int? dataStartRowNumber = null)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        int resolvedHeaderRow = Math.Max(1, headerRowNumber);
        int resolvedDataStartRow = Math.Max(resolvedHeaderRow + 1, dataStartRowNumber ?? (resolvedHeaderRow + 1));

        OpenXmlWorksheetData worksheet = ExcelWorksheetHelper.ReadWorksheet(excelStream, sheetName);
        if (!worksheet.HasData)
        {
            return new StockReportInspectionDto(
                fileName,
                worksheet.AllSheetNames,
                worksheet.SheetName,
                resolvedHeaderRow,
                resolvedDataStartRow,
                0,
                [],
                new Dictionary<StockReportField, int>());
        }

        int firstCol = worksheet.FirstColumn;
        int lastCol = worksheet.LastColumn;
        int lastRow = worksheet.LastRow;

        List<DetectedExcelColumnDto> columns = new();
        for (int colIndex = firstCol; colIndex <= lastCol; colIndex++)
        {
            string headerText = worksheet.GetCellString(resolvedHeaderRow, colIndex);
            string colLetter = ExcelWorksheetHelper.GetColumnLetterFromNumber(colIndex);

            List<string> samples = new();
            for (int rowIndex = resolvedDataStartRow; rowIndex <= lastRow && samples.Count < 3; rowIndex++)
            {
                string sampleValue = worksheet.GetCellString(rowIndex, colIndex);
                if (!string.IsNullOrWhiteSpace(sampleValue))
                {
                    samples.Add(sampleValue);
                }
            }

            if (!string.IsNullOrWhiteSpace(headerText) || samples.Count > 0)
            {
                columns.Add(new DetectedExcelColumnDto(
                    colIndex,
                    colLetter,
                    string.IsNullOrWhiteSpace(headerText) ? $"Column {colLetter}" : headerText,
                    samples));
            }
        }

        Dictionary<StockReportField, int> suggestedMapping = SuggestMapping(columns);

        return new StockReportInspectionDto(
            fileName,
            worksheet.AllSheetNames,
            worksheet.SheetName,
            resolvedHeaderRow,
            resolvedDataStartRow,
            lastRow,
            columns,
            suggestedMapping);
    }

    public static Dictionary<StockReportField, int> SuggestMapping(IReadOnlyList<DetectedExcelColumnDto> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        return StockReportHeaderMatcher.SuggestMapping(
            columns.Select(col => (col.ColumnIndex, col.HeaderText)).ToList());
    }
}
