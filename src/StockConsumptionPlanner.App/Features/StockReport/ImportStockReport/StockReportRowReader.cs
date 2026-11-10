using System.Globalization;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public sealed class StockReportRowReader(
    OpenXmlWorksheetData worksheet,
    int rowNumber,
    IReadOnlyDictionary<StockReportField, int> fieldColumnMap)
{
    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy H:mm",
        "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyyMMdd", "dd/MM/yyyy", "d/M/yyyy"
    ];

    private static readonly string[] TrueFlags = ["x", "1", "true", "yes", "y", "так", "т"];

    private static readonly string[] FalseFlags = ["0", "false", "no", "n", "ні", "-"];

    private readonly List<StockReportRowErrorDto> _errors = new();

    public int RowNumber => rowNumber;

    public IReadOnlyList<StockReportRowErrorDto> Errors => _errors;

    public string ReadText(StockReportField field)
    {
        if (!fieldColumnMap.TryGetValue(field, out int columnIndex))
        {
            return string.Empty;
        }

        string value = worksheet.GetCellString(rowNumber, columnIndex).Trim();
        int? maxLength = StockReportFieldCatalog.GetMetadata(field).MaxLength;
        if (maxLength is int limit && value.Length > limit)
        {
            AddError(field, value, $"{field} must be at most {limit} characters.");
        }

        return value;
    }

    public string ReadRequiredText(StockReportField field)
    {
        string value = ReadText(field);
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(field, value, $"{field} is required.");
        }

        return value;
    }

    public string? ReadOptionalText(StockReportField field)
    {
        string value = ReadText(field);
        return value.Length == 0 ? null : value;
    }

    public int ReadYear(StockReportField field)
    {
        OpenXmlCellData? cell = GetCell(field);
        string rawValue = cell?.DisplayText ?? string.Empty;

        if (cell?.NumericValue is double numeric)
        {
            int candidate = (int)Math.Round(numeric);
            if (candidate is >= 1900 and <= 2999)
            {
                return candidate;
            }
        }

        if (int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
            parsed is >= 1900 and <= 2999)
        {
            return parsed;
        }

        AddError(field, rawValue, $"Invalid {field} '{rawValue}'. Expected a 4-digit year.");
        return 0;
    }

    public decimal ReadDecimal(StockReportField field)
    {
        OpenXmlCellData? cell = GetCell(field);
        string rawValue = cell?.DisplayText ?? string.Empty;

        if (cell?.NumericValue is double numeric)
        {
            return Convert.ToDecimal(numeric, CultureInfo.InvariantCulture);
        }

        if (ImportStockReportHandler.TryParseFlexibleDecimalString(rawValue, out decimal parsed))
        {
            return parsed;
        }

        AddError(field, rawValue, $"Invalid {field} '{rawValue}'. Expected a valid decimal number.");
        return 0m;
    }

    public DateOnly? ReadDate(StockReportField field)
    {
        OpenXmlCellData? cell = GetCell(field);
        string rawValue = cell?.DisplayText.Trim() ?? string.Empty;
        if (cell is null || rawValue.Length == 0 || IsSapInitialDate(rawValue))
        {
            return null;
        }

        if (TryParseDate(cell, rawValue, out DateOnly date))
        {
            return date;
        }

        AddError(field, rawValue, $"Invalid {field} '{rawValue}'. Expected a date such as 31.12.2026.");
        return null;
    }

    public bool ReadFlag(StockReportField field)
    {
        string rawValue = GetCell(field)?.DisplayText.Trim() ?? string.Empty;
        if (rawValue.Length == 0)
        {
            return false;
        }

        if (TrueFlags.Contains(rawValue, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (FalseFlags.Contains(rawValue, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        AddError(field, rawValue, $"Invalid {field} '{rawValue}'. Expected X or blank.");
        return false;
    }

    private static bool TryParseDate(OpenXmlCellData cell, string rawValue, out DateOnly date)
    {
        if (cell.NumericValue is double serial && serial is >= 1 and < 2958466)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }

        if (DateTime.TryParseExact(
                rawValue,
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        date = default;
        return false;
    }

    private static bool IsSapInitialDate(string rawValue)
    {
        return rawValue is "00.00.0000" or "00000000" or "0000-00-00";
    }

    private OpenXmlCellData? GetCell(StockReportField field)
    {
        return fieldColumnMap.TryGetValue(field, out int columnIndex)
            ? worksheet.GetCell(rowNumber, columnIndex)
            : null;
    }

    private void AddError(StockReportField field, string? rawValue, string message)
    {
        _errors.Add(new StockReportRowErrorDto(rowNumber, field.ToString(), rawValue, message));
    }
}
