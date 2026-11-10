using System.Collections;
using System.Globalization;
using System.Resources;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Localization;

namespace StockConsumptionPlanner.Tests.Localization;

public sealed class LocalizationResourceTests
{
    public static TheoryData<Type> ResourceTypes => new()
    {
        typeof(SharedResources),
        typeof(StockReportResources),
        typeof(ConsumptionPlansResources)
    };

    [Theory]
    [MemberData(nameof(ResourceTypes))]
    public void UkrainianResources_TranslateEveryEnglishString(Type resourceType)
    {
        ResourceManager resources = new(resourceType);

        Dictionary<string, string> english = ReadStrings(resources, CultureInfo.InvariantCulture);
        Dictionary<string, string> ukrainian = ReadStrings(resources, new CultureInfo("uk"));

        Assert.NotEmpty(english);
        Assert.Empty(english.Keys.Except(ukrainian.Keys));
        Assert.All(english.Keys, key => Assert.False(string.IsNullOrWhiteSpace(ukrainian[key]), key));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "Рік документа матер.")]
    [InlineData(PlannerCultures.English, "Material Document Year")]
    public void StockFieldNames_UseCatalogDisplayNameForUiCulture(string culture, string expected)
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            Assert.Equal(expected, StockReportField.MaterialDocumentYear.GetLocalizedName());
            Assert.Equal(expected, StockReportFieldLocalization.GetLocalizedFieldName("MaterialDocumentYear"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void UkrainianResources_DescribeEveryStockFieldAndType()
    {
        Dictionary<string, string> ukrainian = ReadStrings(
            new ResourceManager(typeof(StockReportResources)),
            new CultureInfo("uk"));

        Assert.All(StockReportFieldCatalog.All, metadata =>
            Assert.True(ukrainian.ContainsKey($"FieldDescription_{metadata.Key}"), metadata.Key));
        Assert.All(Enum.GetValues<StockReportFieldType>(), type =>
            Assert.True(ukrainian.ContainsKey($"FieldType_{type}"), type.ToString()));
    }

    private static Dictionary<string, string> ReadStrings(ResourceManager resources, CultureInfo culture)
    {
        ResourceSet? set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        return set.Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value as string ?? string.Empty);
    }
}
