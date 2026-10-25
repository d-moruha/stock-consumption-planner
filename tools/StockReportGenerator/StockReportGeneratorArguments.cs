using System.Globalization;

namespace StockReportGenerator;

public sealed record ParsedStockReportGeneratorArguments(
    string OutputPath,
    StockReportGeneratorOptions Options);

public static class StockReportGeneratorArguments
{
    public static ParsedStockReportGeneratorArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        int? rowCount = null;
        string? outputPath = null;
        int? seed = null;
        StockReportHeaderLanguage headerLanguage = StockReportHeaderLanguage.English;
        string sheetName = "Stock";

        for (int index = 0; index < args.Count; index++)
        {
            string token = args[index];
            switch (token)
            {
                case "--rows":
                    rowCount = ReadInt(args, ref index, "--rows");
                    break;
                case "--output":
                    outputPath = ReadRequiredValue(args, ref index, "--output");
                    break;
                case "--seed":
                    seed = ReadInt(args, ref index, "--seed");
                    break;
                case "--headers":
                    headerLanguage = ParseHeaderLanguage(ReadRequiredValue(args, ref index, "--headers"));
                    break;
                case "--sheet":
                    sheetName = ReadRequiredValue(args, ref index, "--sheet");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{token}'.");
            }
        }

        if (rowCount is null)
        {
            throw new ArgumentException("--rows is required.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required.");
        }

        if (rowCount.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                "rows",
                rowCount.Value,
                "--rows must be an integer greater than or equal to 1.");
        }

        StockReportGeneratorOptions options = new()
        {
            RowCount = rowCount.Value,
            Seed = seed,
            HeaderLanguage = headerLanguage,
            SheetName = sheetName
        };
        options.EnsureValid();

        return new ParsedStockReportGeneratorArguments(outputPath, options);
    }

    private static int ReadInt(IReadOnlyList<string> args, ref int index, string flag)
    {
        string raw = ReadRequiredValue(args, ref index, flag);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"{flag} must be an integer.");
        }

        return value;
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string flag)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{flag} requires a value.");
        }

        index++;
        return args[index];
    }

    private static StockReportHeaderLanguage ParseHeaderLanguage(string raw)
    {
        if (string.Equals(raw, "en", StringComparison.OrdinalIgnoreCase))
        {
            return StockReportHeaderLanguage.English;
        }

        if (string.Equals(raw, "uk", StringComparison.OrdinalIgnoreCase))
        {
            return StockReportHeaderLanguage.Ukrainian;
        }

        throw new ArgumentException("--headers must be 'en' or 'uk'.");
    }
}
