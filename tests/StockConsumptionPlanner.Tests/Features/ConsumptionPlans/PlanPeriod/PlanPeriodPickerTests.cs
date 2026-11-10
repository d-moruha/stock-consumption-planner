using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.PlanPeriod;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Localization;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans.PlanPeriod;

public sealed class PlanPeriodPickerTests : BunitContext
{
    private const string UkrainianYearDigitsError =
        "Введіть рік чотирма цифрами, від 2021 до 2031.";

    private const string UkrainianMonthError =
        "Введіть номер місяця від 1 до 12 або назву місяця, наприклад «Січень».";

    private const string EnglishMonthError =
        "Enter a month number from 1 to 12 or a month name, for example January.";

    private readonly List<PlanTargetMonth?> _reported = [];

    public PlanPeriodPickerTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_PeriodWithLocalizedMonthAndYearOptions()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);

        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        Assert.Equal("2026", picker.Find("#planYearInput").GetAttribute("value"));
        Assert.Equal("Грудень", picker.Find("#planMonthInput").GetAttribute("value"));
        Assert.Equal(11, picker.FindAll("#planYearInput-options button").Count);
        IReadOnlyList<IElement> months = picker.FindAll("#planMonthInput-options button");
        Assert.Equal(12, months.Count);
        Assert.Equal("Січень", months[0].TextContent.Trim());
        Assert.Contains("active", months[11].ClassList);
        Assert.Empty(picker.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "20x6", UkrainianYearDigitsError)]
    [InlineData(PlannerCultures.Ukrainian, "2040", "Рік має бути від 2021 до 2031.")]
    [InlineData(PlannerCultures.Ukrainian, "", "Введіть рік.")]
    [InlineData(PlannerCultures.English, "20x6", "Enter the year as 4 digits, from 2021 to 2031.")]
    [InlineData(PlannerCultures.English, "2040", "The year must be from 2021 to 2031.")]
    [InlineData(PlannerCultures.English, " ", "Enter a year.")]
    public void InvalidYear_ShowsLocalizedErrorAndReportsNoPeriod(
        string culture,
        string text,
        string expected)
    {
        using CultureScope _ = new(culture);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("#planYearInput").Change(text);

        IElement input = picker.Find("#planYearInput");
        Assert.Contains("is-invalid", input.ClassList);
        Assert.Equal("planYearInput-error", input.GetAttribute("aria-describedby"));
        Assert.Equal(expected, picker.Find("#planYearInput-error").TextContent);
        Assert.Equal(text, input.GetAttribute("value"));
        Assert.Equal([null], _reported);
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "Листопадд", UkrainianMonthError)]
    [InlineData(PlannerCultures.Ukrainian, "13", UkrainianMonthError)]
    [InlineData(PlannerCultures.Ukrainian, "", "Введіть місяць.")]
    [InlineData(PlannerCultures.English, "Movember", EnglishMonthError)]
    [InlineData(PlannerCultures.English, "", "Enter a month.")]
    public void InvalidMonth_ShowsLocalizedErrorAndReportsNoPeriod(
        string culture,
        string text,
        string expected)
    {
        using CultureScope _ = new(culture);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("#planMonthInput").Change(text);

        Assert.Contains("is-invalid", picker.Find("#planMonthInput").ClassList);
        Assert.Equal(expected, picker.Find("#planMonthInput-error").TextContent);
        Assert.DoesNotContain("is-invalid", picker.Find("#planYearInput").ClassList);
        Assert.Equal([null], _reported);
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "3", "Березень")]
    [InlineData(PlannerCultures.Ukrainian, "березень", "Березень")]
    [InlineData(PlannerCultures.English, "march", "March")]
    public void TypedMonth_ReportsPeriodAndShowsLocalizedName(
        string culture,
        string text,
        string shown)
    {
        using CultureScope _ = new(culture);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("#planMonthInput").Change(text);

        Assert.Equal([new PlanTargetMonth(2026, 3)], _reported);
        Assert.Equal(shown, picker.Find("#planMonthInput").GetAttribute("value"));
        Assert.Empty(picker.FindAll(".invalid-feedback"));
    }

    [Fact]
    public void PickingFromList_ReportsPeriodAndClosesList()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("button[aria-controls='planMonthInput-options']").Click();
        Assert.Contains("show", picker.Find("#planMonthInput-options").ClassList);

        picker.FindAll("#planMonthInput-options button")[4].Click();

        Assert.Equal([new PlanTargetMonth(2026, 5)], _reported);
        Assert.Equal("Травень", picker.Find("#planMonthInput").GetAttribute("value"));
        Assert.DoesNotContain("show", picker.Find("#planMonthInput-options").ClassList);
    }

    [Fact]
    public void PickingFromList_ReplacesInvalidManualInput()
    {
        using CultureScope _ = new(PlannerCultures.English);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("#planYearInput").Change("abcd");
        picker.Find("button[aria-controls='planYearInput-options']").Click();
        picker.FindAll("#planYearInput-options button")[6].Click();

        Assert.Equal([null, new PlanTargetMonth(2027, 12)], _reported);
        Assert.Equal("2027", picker.Find("#planYearInput").GetAttribute("value"));
        Assert.Empty(picker.FindAll(".invalid-feedback"));
    }

    [Fact]
    public void FocusLeavingOrEscape_ClosesList()
    {
        using CultureScope _ = new(PlannerCultures.English);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker();

        picker.Find("#planMonthInput").KeyDown("ArrowDown");
        Assert.Contains("show", picker.Find("#planMonthInput-options").ClassList);
        picker.Find("#planMonthInput").KeyDown("Escape");
        Assert.DoesNotContain("show", picker.Find("#planMonthInput-options").ClassList);

        picker.Find("button[aria-controls='planMonthInput-options']").Click();
        picker.Find("#planMonthInput").FocusOut();
        Assert.DoesNotContain("show", picker.Find("#planMonthInput-options").ClassList);
        Assert.Empty(_reported);
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "Оберіть грудень 2026\u202Fр. або пізніший місяць.")]
    [InlineData(PlannerCultures.English, "Choose December 2026 or a later month.")]
    public void PeriodBeforeEarliest_ShowsLocalizedErrorUnderMonth(string culture, string expected)
    {
        using CultureScope _ = new(culture);
        IRenderedComponent<PlanPeriodPicker> picker = RenderPicker(
            new PlanTargetMonth(2026, 12),
            idPrefix: "newPlan");

        picker.Find("#newPlanMonthInput").Change("11");

        Assert.Equal(expected, picker.Find("#newPlanMonthInput-error").TextContent);
        Assert.DoesNotContain("is-invalid", picker.Find("#newPlanYearInput").ClassList);
        Assert.Equal(6, picker.FindAll("#newPlanYearInput-options button").Count);
        Assert.Equal([null], _reported);

        picker.Find("#newPlanYearInput").Change("2027");

        Assert.Empty(picker.FindAll(".invalid-feedback"));
        Assert.Equal([null, new PlanTargetMonth(2027, 11)], _reported);
    }

    private IRenderedComponent<PlanPeriodPicker> RenderPicker(
        PlanTargetMonth? earliest = null,
        string idPrefix = "plan")
    {
        return Render<PlanPeriodPicker>(parameters => parameters
            .Add(picker => picker.Period, new PlanTargetMonth(2026, 12))
            .Add(picker => picker.CurrentYear, 2026)
            .Add(picker => picker.Earliest, earliest)
            .Add(picker => picker.IdPrefix, idPrefix)
            .Add(
                picker => picker.PeriodChanged,
                (PlanTargetMonth? period) => _reported.Add(period)));
    }
}
