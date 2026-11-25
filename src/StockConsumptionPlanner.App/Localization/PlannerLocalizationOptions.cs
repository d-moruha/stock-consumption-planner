namespace StockConsumptionPlanner.App.Localization;

public sealed class PlannerLocalizationOptions
{
    public const string SectionName = "Localization";

    public const string Multilingual = "Multilingual";

    public const string English = "English";

    public const string Ukrainian = "Ukrainian";

    public string Mode { get; set; } = Multilingual;

    public bool AllowsLanguageSelection =>
        string.Equals(Mode, Multilingual, StringComparison.OrdinalIgnoreCase);

    public bool TryGetFixedCulture(out string culture)
    {
        if (string.Equals(Mode, English, StringComparison.OrdinalIgnoreCase))
        {
            culture = PlannerCultures.English;
            return true;
        }

        if (string.Equals(Mode, Ukrainian, StringComparison.OrdinalIgnoreCase))
        {
            culture = PlannerCultures.Ukrainian;
            return true;
        }

        culture = string.Empty;
        return false;
    }

    public void Validate()
    {
        string mode = Mode?.Trim() ?? string.Empty;
        if (mode.Length == 0 ||
            mode.Equals(Multilingual, StringComparison.OrdinalIgnoreCase))
        {
            Mode = Multilingual;
            return;
        }

        if (mode.Equals(English, StringComparison.OrdinalIgnoreCase))
        {
            Mode = English;
            return;
        }

        if (mode.Equals(Ukrainian, StringComparison.OrdinalIgnoreCase))
        {
            Mode = Ukrainian;
            return;
        }

        throw new InvalidOperationException(
            $"{SectionName}:{nameof(Mode)} must be {Multilingual}, {English}, or {Ukrainian}.");
    }
}
