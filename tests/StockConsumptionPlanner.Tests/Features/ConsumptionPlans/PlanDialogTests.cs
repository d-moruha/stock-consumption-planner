using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Localization;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

public sealed class PlanDialogTests : BunitContext
{
    private const string EnglishInvalidQuantity =
        "Enter a number greater than zero with at most 4 decimal places.";

    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlanTargetMonth December2026 = new(2026, 12);

    private static readonly PlanTargetMonth January2027 = new(2027, 1);

    private readonly List<ConsumptionPlanDto> _saved = [];
    private int _closeCount;

    public PlanDialogTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ValidInput_CreatesPlanForChosenPeriod()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(host);

        Assert.Equal("2026", dialog.Find("#planDialogYearInput").GetAttribute("value"));
        Assert.Equal("Грудень", dialog.Find("#planDialogMonthInput").GetAttribute("value"));
        dialog.Find("#planDialogMonthInput").Change("березень");
        dialog.Find("#planDialogYearInput").Change("2027");
        dialog.Find("#planDialogQuantityInput").Change("4,5");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Single(_saved));
        Assert.Equal((2027, 3, 4.5m), (
            _saved[0].TargetYear,
            _saved[0].TargetMonth,
            _saved[0].PlannedQuantity));
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
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find("#planDialogQuantityInput").Change(quantity);
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Contains("is-invalid", dialog.Find("#planDialogQuantityInput").ClassList);
        Assert.Equal(expected, dialog.Find("#planDialogQuantityError").TextContent);
        Assert.Empty(_saved);
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
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(
            host,
            new PlanTargetMonth(2027, 1));
        Assert.Empty(dialog.FindAll("#planDialogPeriodError"));

        dialog.Find("#planDialogMonthInput").Change("12");
        dialog.Find("#planDialogYearInput").Change("2026");
        dialog.Find("#planDialogQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Equal(expected, dialog.Find("#planDialogPeriodError").TextContent.Trim());
        Assert.Empty(_saved);
        Assert.Single(await PlansAsync(host));
    }

    [Fact]
    public async Task PeriodNotAfterCurrentMonth_ShowsErrorAndCreatesNothing()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find("#planDialogMonthInput").Change("November");
        dialog.Find("#planDialogQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Equal(
            "Choose December 2026 or a later month.",
            dialog.Find("#planDialogMonthInput-error").TextContent);
        Assert.Empty(_saved);
        Assert.Empty(await PlansAsync(host));
    }

    [Fact]
    public async Task HandlerError_IsShownInsideDialog()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(host);
        await host.ImportAsync(Stock("0049000002", 5m));

        dialog.Find("#planDialogQuantityInput").Change("2");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Equal(
            "Stock record 2026/0049000001/0001 is not in the current stock report.",
            dialog.Find(".modal-body .alert-danger").TextContent));
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task EditMode_PrefillsPlanAndSavesNewPeriodAndQuantity()
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        ConsumptionPlanDto plan = (await CreateAsync(host, January2027, 2.5m)).Value!;
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(
            host,
            January2027,
            plan.Id);

        Assert.Equal(
            "Редагування плану споживання",
            dialog.Find("#planDialogTitle").TextContent.Trim());
        Assert.Equal("2027", dialog.Find("#planDialogYearInput").GetAttribute("value"));
        Assert.Equal("Січень", dialog.Find("#planDialogMonthInput").GetAttribute("value"));
        Assert.Equal("2,5", dialog.Find("#planDialogQuantityInput").GetAttribute("value"));
        Assert.Equal("Зберегти", dialog.Find(".modal-footer .btn-primary").TextContent.Trim());

        dialog.Find("#planDialogMonthInput").Change("квітень");
        dialog.Find("#planDialogQuantityInput").Change("7,25");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Single(_saved));
        ConsumptionPlanDto saved = Assert.Single(await PlansAsync(host));
        Assert.Equal(plan.Id, saved.Id);
        Assert.Equal(
            (2027, 4, 7.25m),
            (saved.TargetYear, saved.TargetMonth, saved.PlannedQuantity));
        Assert.Equal(plan.CreatedAtUtc, saved.CreatedAtUtc);
    }

    [Fact]
    public async Task EditMode_RejectsPeriodOfAnotherPlanButAllowsItsOwn()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        await CreateAsync(host, December2026, 1m);
        ConsumptionPlanDto plan = (await CreateAsync(host, January2027, 2m)).Value!;
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(
            host,
            January2027,
            plan.Id);

        dialog.Find("#planDialogMonthInput").Change("12");
        dialog.Find("#planDialogYearInput").Change("2026");
        dialog.Find(".modal-footer .btn-primary").Click();

        Assert.Equal(
            "This stock record already has a plan for December 2026.",
            dialog.Find("#planDialogPeriodError").TextContent.Trim());
        Assert.Empty(_saved);

        dialog.Find("#planDialogYearInput").Change("2027");
        dialog.Find("#planDialogMonthInput").Change("January");
        dialog.Find("#planDialogQuantityInput").Change("3");
        dialog.Find(".modal-footer .btn-primary").Click();

        dialog.WaitForAssertion(() => Assert.Single(_saved));
        ConsumptionPlanDto saved = _saved[0];
        Assert.Equal((2027, 1, 3m), (saved.TargetYear, saved.TargetMonth, saved.PlannedQuantity));
    }

    [Fact]
    public async Task CancelCloseAndEscape_RequestClose()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<PlanDialog> dialog = await RenderDialogAsync(host);

        dialog.Find(".modal-footer .btn-outline-secondary").Click();
        dialog.Find(".modal-header .btn-close").Click();
        dialog.Find("#planDialogQuantityInput").KeyDown("Escape");
        Assert.Equal(3, _closeCount);

        dialog.Find("#planDialogMonthInput").KeyDown("ArrowDown");
        dialog.Find("#planDialogMonthInput").KeyDown("Escape");
        Assert.Equal(3, _closeCount);
        Assert.Empty(_saved);
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

    private async Task<IRenderedComponent<PlanDialog>> RenderDialogAsync(
        ConsumptionPlanTestHost host,
        PlanTargetMonth? initialPeriod = null,
        int? editPlanId = null)
    {
        PlanningSheetDto sheet = (await host.Get<GetPlanningSheetHandler>()
            .HandleAsync(new PlanningSheetQuery(2026, 12))).Value!;
        PlanningSheetRowDto row = sheet.Rows.Items[0];
        ConsumptionPlanDto? plan = row.Plans.SingleOrDefault(p => p.Id == editPlanId);
        Services.AddFallbackServiceProvider(host.Services);
        return Render<PlanDialog>(parameters => parameters
            .Add(dialog => dialog.Row, row)
            .Add(dialog => dialog.Plan, plan)
            .Add(dialog => dialog.InitialPeriod, initialPeriod ?? December2026)
            .Add(dialog => dialog.Earliest, December2026)
            .Add(dialog => dialog.CurrentYear, 2026)
            .Add(dialog => dialog.OnSaved, (ConsumptionPlanDto saved) => _saved.Add(saved))
            .Add(dialog => dialog.OnClose, () => _closeCount++));
    }
}
