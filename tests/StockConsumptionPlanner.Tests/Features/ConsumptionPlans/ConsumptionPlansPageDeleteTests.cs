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

public sealed class ConsumptionPlansPageDeleteTests : BunitContext
{
    private const string DeleteButtonSelector = "#plans-2 .btn-outline-danger";

    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public ConsumptionPlansPageDeleteTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(
        PlannerCultures.Ukrainian,
        "Видалити план споживання?",
        "План для запису 2026/0049000001/0001 на січень 2027\u202Fр. (заплановано: 2,5) "
            + "буде остаточно видалено.",
        "Скасувати",
        "Видалити план")]
    [InlineData(
        PlannerCultures.English,
        "Delete the consumption plan?",
        "The plan for stock record 2026/0049000001/0001 for January 2027 (2.5 planned) "
            + "will be permanently deleted.",
        "Cancel",
        "Delete plan")]
    public async Task DeleteButton_OpensLocalizedConfirmationWithoutDeleting(
        string culture,
        string title,
        string message,
        string cancel,
        string confirm)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithPlanAsync();
        IRenderedComponent<ConsumptionPlansPage> page = RenderWithPlansExpanded(host);

        page.Find(DeleteButtonSelector).Click();

        Assert.Equal(title, page.Find("#deletePlanDialogTitle").TextContent.Trim());
        Assert.Equal(message, page.Find("#deletePlanDialogMessage").TextContent.Trim());
        Assert.Equal(cancel, page.Find(".modal-footer .btn-outline-secondary").TextContent.Trim());
        Assert.Equal(confirm, page.Find(".modal-footer .btn-danger").TextContent.Trim());
        Assert.Single(await PlansAsync(host));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("escape")]
    [InlineData("backdrop")]
    public async Task Dismissing_ClosesDialogAndKeepsPlan(string dismissal)
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartWithPlanAsync();
        IRenderedComponent<ConsumptionPlansPage> page = RenderWithPlansExpanded(host);
        page.Find(DeleteButtonSelector).Click();

        Dismiss(page, dismissal);

        Assert.Empty(page.FindAll(".modal"));
        Assert.Empty(page.FindAll(".modal-backdrop"));
        Assert.Empty(page.FindAll(".alert"));
        Assert.Single(page.FindAll("#plans-2 table td:first-child"));
        Assert.Single(await PlansAsync(host));
    }

    [Fact]
    public async Task ClickInsideDialogOrOtherKey_KeepsDialogOpen()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithPlanAsync();
        IRenderedComponent<ConsumptionPlansPage> page = RenderWithPlansExpanded(host);
        page.Find(DeleteButtonSelector).Click();

        Assert.Throws<MissingEventHandlerException>(
            () => page.Find("#deletePlanDialogMessage").Click());
        page.Find(".modal-footer .btn-outline-secondary").KeyDown("Enter");

        Assert.Single(page.FindAll(".modal"));
        Assert.Single(await PlansAsync(host));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "План для запису 2026/0049000001/0001 видалено.")]
    [InlineData(PlannerCultures.English, "Plan for 2026/0049000001/0001 deleted.")]
    public async Task Confirm_DeletesPlanAndClosesDialog(string culture, string status)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithPlanAsync();
        IRenderedComponent<ConsumptionPlansPage> page = RenderWithPlansExpanded(host);
        page.Find(DeleteButtonSelector).Click();

        page.Find(".modal-footer .btn-danger").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal")));
        Assert.Empty(page.FindAll(".modal-backdrop"));
        Assert.Empty(page.FindAll("#plans-2"));
        Assert.Equal(status, page.Find(".alert-success span").TextContent);
        Assert.Empty(await PlansAsync(host));
    }

    private static void Dismiss(IRenderedComponent<ConsumptionPlansPage> page, string dismissal)
    {
        switch (dismissal)
        {
            case "cancel":
                page.Find(".modal-footer .btn-outline-secondary").Click();
                break;
            case "close":
                page.Find(".modal-header .btn-close").Click();
                break;
            case "escape":
                page.Find(".modal-footer .btn-outline-secondary").KeyDown("Escape");
                break;
            case "backdrop":
                page.Find(".modal").Click();
                break;
        }
    }

    private static async Task<ConsumptionPlanTestHost> StartWithPlanAsync()
    {
        ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        ConsumptionPlanResult<ConsumptionPlanDto> created = await host.Get<CreatePlanHandler>()
            .HandleAsync(new CreateConsumptionPlanRequest(
                2026, "0049000001", "0001", 2027, 1, 2.5m));
        Assert.True(created.IsSuccess, created.Error?.Message);
        return host;
    }

    private static async Task<IReadOnlyList<ConsumptionPlanDto>> PlansAsync(
        ConsumptionPlanTestHost host)
    {
        return (await host.Get<GetPlansHandler>().ListAsync(new GetPlansQuery())).Items;
    }

    private IRenderedComponent<ConsumptionPlansPage> RenderWithPlansExpanded(
        ConsumptionPlanTestHost host)
    {
        Services.AddFallbackServiceProvider(host.Services);
        IRenderedComponent<ConsumptionPlansPage> page = Render<ConsumptionPlansPage>();
        page.WaitForElement("tbody .dropdown-toggle");
        page.Find("tbody .dropdown-toggle").Click();
        return page;
    }
}
