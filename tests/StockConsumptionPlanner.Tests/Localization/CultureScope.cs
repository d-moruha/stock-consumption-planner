using System.Globalization;

namespace StockConsumptionPlanner.Tests.Localization;

internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string culture)
    {
        CultureInfo selected = new(culture, useUserOverride: false);
        CultureInfo.CurrentCulture = selected;
        CultureInfo.CurrentUICulture = selected;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
