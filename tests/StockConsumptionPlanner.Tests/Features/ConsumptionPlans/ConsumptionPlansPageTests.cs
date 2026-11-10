using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Localization;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class ConsumptionPlansPageTests : BunitContext
{
    private const string UkrainianNotice =
        "Виправте період плану, щоб переглянути та створити плани.";

    private const string EnglishNotice = "Correct the plan period to view and create plans.";

    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public ConsumptionPlansPageTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, UkrainianNotice)]
    [InlineData(PlannerCultures.English, EnglishNotice)]
    public async Task InvalidPeriod_HidesPlanningSheetUntilCorrected(string culture, string notice)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        page.Find("#planYearInput").Change("20x6");

        Assert.Empty(page.FindAll("table"));
        Assert.Equal(notice, page.Find(".alert-warning").TextContent);
        Assert.True(page.Find(".btn.btn-primary.w-100").HasAttribute("disabled"));

        page.Find("#planYearInput").Change("2027");

        page.WaitForElement("table");
        Assert.Empty(page.FindAll(".alert-warning"));
        Assert.False(page.Find(".btn.btn-primary.w-100").HasAttribute("disabled"));
    }

    [Fact]
    public async Task EachCurrentStockRecordIsOneRowWithCreatePlanButton()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2),
            Stock("0049000002", 20m, sourceRow: 3));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        Assert.Equal(2, page.FindAll("tbody > tr").Count);
        Assert.All(
            page.FindAll("tbody > tr .btn-primary"),
            button => Assert.Equal("Створити план", button.TextContent.Trim()));
        Assert.Contains("Заплановано на грудень 2026\u202Fр.", page.Find("thead").TextContent);
        Assert.All(
            page.FindAll("tbody .dropdown-toggle"),
            toggle => Assert.True(toggle.HasAttribute("disabled")));
        Assert.Empty(page.FindAll(".modal"));
    }

    [Fact]
    public async Task CreatePlanDialog_AddsPlansToRowDropdownInChronologicalOrder()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        page.Find("tbody .btn-primary").Click();
        page.Find("#newPlanMonthInput").Change("лютий");
        page.Find("#newPlanYearInput").Change("2027");
        page.Find("#newPlanQuantityInput").Change("4");
        page.Find(".modal-footer .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal")));
        Assert.Equal("План для запису 2026/0049000001/0001 на лютий 2027\u202Fр. збережено.",
            page.Find(".alert-success span").TextContent);
        Assert.Equal(["лютий 2027\u202Fр."], PlanPeriods(page));

        page.Find("tbody .btn-primary").Click();
        Assert.Equal("Грудень", page.Find("#newPlanMonthInput").GetAttribute("value"));
        page.Find("#newPlanQuantityInput").Change("3");
        page.Find(".modal-footer .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Equal(
            ["грудень 2026\u202Fр.", "лютий 2027\u202Fр."],
            PlanPeriods(page)));
        Assert.Equal("Плани (2)", page.Find("tbody .dropdown-toggle").TextContent.Trim());
        Assert.Equal("3", page.FindAll("tbody > tr")[0].Children[6].TextContent.Trim());

        page.Find("tbody .btn-primary").Click();
        Assert.Equal("2027", page.Find("#newPlanYearInput").GetAttribute("value"));
        Assert.Equal("Січень", page.Find("#newPlanMonthInput").GetAttribute("value"));
        page.Find(".modal-footer .btn-outline-secondary").Click();
        Assert.Empty(page.FindAll(".modal"));

        page.Find("tbody .dropdown-toggle").Click();
        Assert.Empty(PlanPeriods(page));
        page.Find("tbody .dropdown-toggle").Click();
        Assert.Equal(2, PlanPeriods(page).Count);

        Assert.Equal(
            [(2026, 12, 3m), (2027, 2, 4m)],
            (await PlansAsync(host))
                .Select(p => (p.TargetYear, p.TargetMonth, p.PlannedQuantity))
                .Order());
    }

    [Fact]
    public async Task PlanInRowDropdown_CanBeUpdatedAndDeleted()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2027, 1, 2m));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        page.Find("tbody .dropdown-toggle").Click();
        page.Find("#plans-2 input").Change("6");
        page.Find("#plans-2 .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Equal(
            "6",
            page.Find("#plans-2 input").GetAttribute("value")));
        Assert.Equal(6m, Assert.Single(await PlansAsync(host)).PlannedQuantity);

        page.Find("#plans-2 .btn-outline-danger").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("#plans-2")));
        Assert.Empty(await PlansAsync(host));
    }

    private static async Task<IReadOnlyList<ConsumptionPlanDto>> PlansAsync(
        ConsumptionPlanTestHost host)
    {
        return (await host.Get<GetPlansHandler>().ListAsync(new GetPlansQuery())).Items;
    }

    private static List<string> PlanPeriods(IRenderedComponent<ConsumptionPlansPage> page)
    {
        return [.. page.FindAll("[id^='plans-'] table td:first-child")
            .Select(cell => cell.TextContent.Trim())];
    }

    private IRenderedComponent<ConsumptionPlansPage> RenderPage(ConsumptionPlanTestHost host)
    {
        Services.AddFallbackServiceProvider(host.Services);
        return Render<ConsumptionPlansPage>();
    }
}
