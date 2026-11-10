using System.Globalization;

namespace StockConsumptionPlanner.App.Localization;

public static class PlannerCultures
{
    public const string Ukrainian = "uk-UA";

    public const string English = "en-US";

    public const string Default = Ukrainian;

    public static IReadOnlyList<string> Supported { get; } = [Ukrainian, English];

    public static bool TryNormalize(string? culture, out string normalized)
    {
        normalized = Supported.FirstOrDefault(
            supported => string.Equals(supported, culture?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }

    public static bool IsCurrent(string culture)
    {
        return string.Equals(CultureInfo.CurrentUICulture.Name, culture, StringComparison.OrdinalIgnoreCase);
    }
}
