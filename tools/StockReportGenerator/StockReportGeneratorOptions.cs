namespace StockReportGenerator;

public enum StockReportHeaderLanguage
{
    English,
    Ukrainian
}

public sealed record StockReportGeneratorOptions
{
    public int RowCount { get; init; }

    public int? Seed { get; init; }

    public StockReportHeaderLanguage HeaderLanguage { get; init; } = StockReportHeaderLanguage.English;

    public string SheetName { get; init; } = "Stock";

    public void EnsureValid()
    {
        if (RowCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RowCount),
                RowCount,
                "Row count must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(SheetName))
        {
            throw new ArgumentException("Sheet name is required.", nameof(SheetName));
        }

        if (SheetName.Length > 31)
        {
            throw new ArgumentException("Sheet name must be at most 31 characters.", nameof(SheetName));
        }

        if (SheetName.IndexOfAny(InvalidSheetNameChars) >= 0)
        {
            throw new ArgumentException(
                "Sheet name contains characters Excel does not allow.",
                nameof(SheetName));
        }
    }

    private static readonly char[] InvalidSheetNameChars = [':', '\\', '/', '?', '*', '[', ']'];
}
