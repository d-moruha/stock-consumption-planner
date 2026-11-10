using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Localization;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans.CreatePlan;

public sealed class CreatePlanDialogTests : BunitContext
{
    private const string EnglishInvalidQuantity =
        "Enter a number greater than zero with at most 4 decimal places.";

    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlanTargetMonth December2026 = new(2026, 12);

    private readonly List<ConsumptionPlanDto> _created = [];
    private int _closeCount;

    public CreatePlanDialogTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ValidInput_CreatesPlanForChosenPeriod()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(host);

        Assert.Equal("2026", dialog.Find("#newPlanYearInput").GetAttribute("value"));
        Assert.Equal("Грудень", dialog.Find("#newPlanMonthInput").GetAttribute("value"));
        dialog.Find("#newPlanMonthInput").Change("березень");
        dialog.Find("#newPlanYearInput").Change("2027");
        dialog.Find("#newPlanQuantityInput").Change("4,5");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Single(_created));
        Assert.Equal((2027, 3, 4.5m), (
            _created[0].TargetYear,
            _created[0].TargetMonth,
            _created[0].PlannedQuantity));
        Assert.Empty(dialog.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "", "Введіть заплановану кількість.")]
    [InlineData(
        PlannerCultures.Ukrainian,
        "0",
        "Введіть число, більше за нуль, з не більше ніж 4 знаками після коми.")]
    [InlineData(
        PlannerCultures.Ukrainian,
        "25",
        "Запланована кількість не може перевищувати поточний залишок (20).")]
    [InlineData(PlannerCultures.English, "abc", EnglishInvalidQuantity)]
    [InlineData(PlannerCultures.English, "-1", EnglishInvalidQuantity)]
    [InlineData(PlannerCultures.English, "1.23456", EnglishInvalidQuantity)]
    [InlineData(
        PlannerCultures.English,
        "20.5",
        "The planned quantity cannot exceed the current stock (20).")]
    public async Task InvalidQuantity_ShowsLocalizedErrorAndCreatesNothing(
        string culture,
        string quantity,
        string expected)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find("#newPlanQuantityInput").Change(quantity);
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Contains("is-invalid", dialog.Find("#newPlanQuantityInput").ClassList);
        Assert.Equal(expected, dialog.Find("#newPlanQuantityError").TextContent);
        Assert.Empty(_created);
        Assert.Empty(await PlansAsync(host));
    }

    [Theory]
    [InlineData(
        PlannerCultures.Ukrainian,
        "План на грудень 2026\u202Fр. для цього запису запасів уже існує.")]
    [InlineData(
        PlannerCultures.English,
        "This stock record already has a plan for December 2026.")]
    public async Task PeriodWithExistingPlan_ShowsErrorAndCreatesNothing(
        string culture,
        string expected)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        await CreateAsync(host, December2026, 1m);
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(
            host,
            new PlanTargetMonth(2027, 1));
        Assert.Empty(dialog.FindAll("#newPlanPeriodError"));

        dialog.Find("#newPlanMonthInput").Change("12");
        dialog.Find("#newPlanYearInput").Change("2026");
        dialog.Find("#newPlanQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Equal(expected, dialog.Find("#newPlanPeriodError").TextContent.Trim());
        Assert.Empty(_created);
        Assert.Single(await PlansAsync(host));
    }

    [Fact]
    public async Task PeriodNotAfterCurrentMonth_ShowsErrorAndCreatesNothing()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find("#newPlanMonthInput").Change("November");
        dialog.Find("#newPlanQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Equal(
            "Choose December 2026 or a later month.",
            dialog.Find("#newPlanMonthInput-error").TextContent);
        Assert.Empty(_created);
        Assert.Empty(await PlansAsync(host));
    }

    [Fact]
    public async Task HandlerError_IsShownInsideDialog()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(host);
        await host.ImportAsync(Stock("0049000002", 5m));

        dialog.Find("#newPlanQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Equal(
            "Stock record 2026/0049000001/0001 is not in the current stock report.",
            dialog.Find(".modal-body .alert-danger").TextContent));
        Assert.Empty(_created);
    }

    [Fact]
    public async Task CancelCloseAndEscape_RequestClose()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<CreatePlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find(".modal-footer .btn-outline-secondary").Click();
        dialog.Find(".modal-header .btn-close").Click();
        dialog.Find("#newPlanQuantityInput").KeyDown("Escape");
        Assert.Equal(3, _closeCount);

        dialog.Find("#newPlanMonthInput").KeyDown("ArrowDown");
        dialog.Find("#newPlanMonthInput").KeyDown("Escape");
        Assert.Equal(3, _closeCount);
        Assert.Empty(_created);
    }

    private static async Task<ConsumptionPlanTestHost> StartWithStockAsync()
    {
        ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 20m));
        return host;
    }

    private static Task<ConsumptionPlanResult<ConsumptionPlanDto>> CreateAsync(
        ConsumptionPlanTestHost host,
        PlanTargetMonth period,
        decimal quantity)
    {
        return host.Get<CreatePlanHandler>().HandleAsync(new CreateConsumptionPlanRequest(
            2026,
            "0049000001",
            "0001",
            period.Year,
            period.Month,
            quantity));
    }

    private static async Task<IReadOnlyList<ConsumptionPlanDto>> PlansAsync(
        ConsumptionPlanTestHost host)
    {
        PlanningSheetDto sheet = (await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12))).Value!;
        return [.. sheet.Rows.Items.SelectMany(row => row.Plans)];
    }

    private async Task<IRenderedComponent<CreatePlanDialog>> RenderDialogAsync(
        ConsumptionPlanTestHost host,
        PlanTargetMonth? initialPeriod = null)
    {
        PlanningSheetDto sheet = (await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12))).Value!;
        Services.AddFallbackServiceProvider(host.Services);
        return Render<CreatePlanDialog>(parameters => parameters
            .Add(dialog => dialog.Row, sheet.Rows.Items[0])
            .Add(dialog => dialog.InitialPeriod, initialPeriod ?? December2026)
            .Add(dialog => dialog.Earliest, December2026)
            .Add(dialog => dialog.CurrentYear, 2026)
            .Add(dialog => dialog.OnCreated, (ConsumptionPlanDto plan) => _created.Add(plan))
            .Add(dialog => dialog.OnClose, () => _closeCount++));
    }
}
