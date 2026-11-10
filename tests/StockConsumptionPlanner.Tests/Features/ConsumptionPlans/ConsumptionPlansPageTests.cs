using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
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
    public async Task CreatedPlan_UsesManuallyEnteredPeriod()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        page.Find("#planMonthInput").Change("лютий");
        page.Find("#planYearInput").Change("2027");
        page.WaitForAssertion(
            () => Assert.Equal("Лютий", page.Find("#planMonthInput").GetAttribute("value")));
        page.Find("tbody input[type=number]").Change("4");
        page.Find("tbody .btn-primary").Click();

        page.WaitForAssertion(
            () => Assert.Contains("alert-success", page.Find("[role=alert]").ClassList));
        ConsumptionPlanDto plan = Assert.Single(
            (await host.Get<GetPlansHandler>().ListAsync(new GetPlansQuery())).Items);
        Assert.Equal((2027, 2), (plan.TargetYear, plan.TargetMonth));
        Assert.Equal(4m, plan.PlannedQuantity);
    }

    private IRenderedComponent<ConsumptionPlansPage> RenderPage(ConsumptionPlanTestHost host)
    {
        Services.AddFallbackServiceProvider(host.Services);
        return Render<ConsumptionPlansPage>();
    }
}
