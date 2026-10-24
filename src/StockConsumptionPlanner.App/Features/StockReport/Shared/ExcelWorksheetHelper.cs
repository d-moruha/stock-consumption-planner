using System.Globalization;
using ClosedXML.Excel;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public static class ExcelWorksheetHelper
{
    public static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, string? requestedSheetName)
    {
        if (!string.IsNullOrWhiteSpace(requestedSheetName))
        {
            IXLWorksheet? match = workbook.Worksheets.FirstOrDefault(ws =>
                string.Equals(ws.Name, requestedSheetName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return workbook.Worksheets.First();
    }

    public static string ReadStringPreservingFormat(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.Text)
        {
            return cell.GetString().Trim();
        }

        string formatted = cell.GetFormattedString().Trim();
        if (!string.IsNullOrEmpty(formatted))
        {
            return formatted;
        }

        return cell.Value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    public static string ReadCellDisplayString(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        string formatted = cell.GetFormattedString().Trim();
        if (!string.IsNullOrEmpty(formatted))
        {
            return formatted;
        }

        return cell.Value.ToString(CultureInfo.InvariantCulture).Trim();
    }
}
