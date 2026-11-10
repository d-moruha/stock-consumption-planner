using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class ConsumptionPlanDisplayTests
{
    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "1\u00A0234,5")]
    [InlineData(PlannerCultures.English, "1,234.5")]
    public void FormatQuantity_FollowsCurrentCulture(string culture, string expected)
    {
        using CultureScope _ = new(culture);

        Assert.Equal(expected, ConsumptionPlanDisplay.FormatQuantity(1234.5m));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "15\u00A0400,75")]
    [InlineData(PlannerCultures.English, "15,400.75")]
    public void FormatMoney_FollowsCurrentCulture(string culture, string expected)
    {
        using CultureScope _ = new(culture);

        Assert.Equal(expected, ConsumptionPlanDisplay.FormatMoney(15400.75m));
        Assert.Equal("—", ConsumptionPlanDisplay.FormatMoney(null));
        Assert.Equal("—", ConsumptionPlanDisplay.FormatQuantity(null));
    }

    [Fact]
    public void FormatQuantity_KeepsTheSignWhenUnplannedIsNegative()
    {
        using CultureScope _ = new(PlannerCultures.English);

        Assert.Equal("-2", ConsumptionPlanDisplay.FormatQuantity(-2m));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "План змінив інший користувач. Натисніть «Пошук», щоб оновити дані.")]
    [InlineData(PlannerCultures.English, "The plan was changed by someone else. Use Search to reload.")]
    public void LocalizeError_TranslatesByErrorCode(string culture, string expected)
    {
        using CultureScope _ = new(culture);
        using ServiceProvider services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        IStringLocalizer<ConsumptionPlansResources> localizer =
            services.GetRequiredService<IStringLocalizer<ConsumptionPlansResources>>();

        string message = ConsumptionPlanDisplay.LocalizeError(
            localizer,
            ConsumptionPlanError.StaleVersion(),
            new StockDocumentKey(2026, "0049000001", "0001"),
            new PlanTargetMonth(2026, 12));

        Assert.Equal(expected, message);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture)
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
