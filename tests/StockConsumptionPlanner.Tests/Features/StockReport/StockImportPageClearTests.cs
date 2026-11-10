using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;
using StockConsumptionPlanner.App.Localization;
using StockConsumptionPlanner.Tests.Features.ConsumptionPlans;
using StockConsumptionPlanner.Tests.Localization;
using static StockConsumptionPlanner.Tests.Features.ConsumptionPlans.ConsumptionPlanTestHost;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockImportPageClearTests : BunitContext
{
    private const string ClearButtonSelector =
        ".scroll-table-card .card-header .btn-outline-danger";

    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public StockImportPageClearTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(
        PlannerCultures.Ukrainian,
        "Очистити звіт про запаси?",
        "Збережений звіт про запаси «test.xlsx» (рядків: 2) буде остаточно видалено. "
            + "Щоб продовжити роботу із запасами, потрібно буде імпортувати звіт знову.",
        "Скасувати",
        "Очистити звіт")]
    [InlineData(
        PlannerCultures.English,
        "Clear the stock report?",
        "The stored stock report “test.xlsx” (2 rows) will be permanently deleted. "
            + "To continue working with stock, you will need to import a report again.",
        "Cancel",
        "Clear report")]
    public async Task ClearButton_OpensLocalizedConfirmationWithoutClearing(
        string culture,
        string title,
        string message,
        string cancel,
        string confirm)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<StockImportPage> page = RenderPage(host);
        page.WaitForElement(ClearButtonSelector);

        page.Find(ClearButtonSelector).Click();

        Assert.Equal(title, page.Find("#clearReportDialogTitle").TextContent.Trim());
        Assert.Equal(message, page.Find("#clearReportDialogMessage").TextContent.Trim());
        Assert.Equal(cancel, page.Find(".modal-footer .btn-outline-secondary").TextContent.Trim());
        Assert.Equal(confirm, page.Find(".modal-footer .btn-danger").TextContent.Trim());
        Assert.NotNull(await host.Repository.GetCurrentAsync());
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("escape")]
    [InlineData("backdrop")]
    public async Task Dismissing_ClosesDialogAndKeepsReport(string dismissal)
    {
        using CultureScope _ = new(PlannerCultures.Ukrainian);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<StockImportPage> page = RenderPage(host);
        page.WaitForElement(ClearButtonSelector);
        page.Find(ClearButtonSelector).Click();

        Dismiss(page, dismissal);

        Assert.Empty(page.FindAll(".modal"));
        Assert.Empty(page.FindAll(".modal-backdrop"));
        Assert.Empty(page.FindAll(".alert"));
        Assert.Equal(2, page.FindAll(".scroll-table-card tbody > tr").Count);
        StockReportBatch? stored = await host.Repository.GetCurrentAsync();
        Assert.Equal(2, stored?.TotalRows);
    }

    [Fact]
    public async Task ClickInsideDialogOrOtherKey_KeepsDialogOpen()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<StockImportPage> page = RenderPage(host);
        page.WaitForElement(ClearButtonSelector);
        page.Find(ClearButtonSelector).Click();

        Assert.Throws<MissingEventHandlerException>(
            () => page.Find("#clearReportDialogMessage").Click());
        page.Find(".modal-footer .btn-outline-secondary").KeyDown("Enter");

        Assert.Single(page.FindAll(".modal"));
        Assert.NotNull(await host.Repository.GetCurrentAsync());
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "Поточний збережений звіт про запаси очищено.")]
    [InlineData(PlannerCultures.English, "Cleared current stored stock report.")]
    public async Task Confirm_ClearsReportAndClosesDialog(string culture, string status)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartWithStockAsync();
        IRenderedComponent<StockImportPage> page = RenderPage(host);
        page.WaitForElement(ClearButtonSelector);
        page.Find(ClearButtonSelector).Click();

        page.Find(".modal-footer .btn-danger").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal")));
        Assert.Empty(page.FindAll(".modal-backdrop"));
        Assert.Empty(page.FindAll(ClearButtonSelector));
        Assert.Equal(status, page.Find(".alert-success span").TextContent);
        Assert.Null(await host.Repository.GetCurrentAsync());
    }

    private static void Dismiss(IRenderedComponent<StockImportPage> page, string dismissal)
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

    private static async Task<ConsumptionPlanTestHost> StartWithStockAsync()
    {
        ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2),
            Stock("0049000002", 20m, sourceRow: 3));
        return host;
    }

    private IRenderedComponent<StockImportPage> RenderPage(ConsumptionPlanTestHost host)
    {
        Services.AddFallbackServiceProvider(host.Services);
        return Render<StockImportPage>();
    }
}
