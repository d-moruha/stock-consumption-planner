using AngleSharp.Dom;
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
    private static readonly DateTimeOffset November10 = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public ConsumptionPlansPageTests()
    {
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
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
        Assert.Empty(page.FindAll("#planYearInput"));
        Assert.Empty(page.FindAll("#planMonthInput"));
        Assert.Equal(
            [
                "Матеріал", "Опис", "СПП-елемент", "Місце збереження", "Залишок", "",
                "Незапланована кількість", "Вартість запасу ▼",
                "Дата надходження", "Дата утворення запасу", "Вік запасу (дн.) ↕", "Плани", "Дії"
            ],
            page.FindAll("thead th").Select(th => CollapseWhitespace(th.TextContent)));
        Assert.All(
            page.FindAll("tbody .dropdown-toggle"),
            toggle => Assert.True(toggle.HasAttribute("disabled")));
        Assert.Empty(page.FindAll(".modal"));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "СПП-елемент", "Місце збереження")]
    [InlineData(PlannerCultures.English, "WBS", "Storage Location")]
    public async Task StockRows_ShowWbsAndStorageLocation_InsteadOfDocumentKey(
        string culture,
        string wbsHeader,
        string storageHeader)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2) with
            {
                WbsElement = "UA-01-0001.01",
                StorageLocation = "1000"
            },
            Stock("0049000002", 5m, sourceRow: 3));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        string headers = CollapseWhitespace(page.Find("thead").TextContent);
        Assert.Contains(wbsHeader, headers);
        Assert.Contains(storageHeader, headers);
        string removedHeader = culture == PlannerCultures.Ukrainian ? "Документ" : "Document";
        Assert.DoesNotContain(removedHeader, headers);
        Assert.Equal(
            [
                ["UA-01-0001.01", "1000"],
                ["—", "—"]
            ],
            page.FindAll("tbody > tr[data-document]").Select(row => new[]
            {
                row.QuerySelector("td:nth-child(3)")!.TextContent.Trim(),
                row.QuerySelector("td:nth-child(4)")!.TextContent.Trim()
            }));
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
        page.Find("#planDialogMonthInput").Change("лютий");
        page.Find("#planDialogYearInput").Change("2027");
        page.Find("#planDialogQuantityInput").Change("4");
        page.Find(".modal-footer .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal")));
        Assert.Equal("План для запису 2026/0049000001/0001 на лютий 2027\u202Fр. збережено.",
            page.Find(".alert-success span").TextContent);
        Assert.Equal(["лютий 2027\u202Fр."], PlanPeriods(page));

        page.Find("tbody .btn-primary").Click();
        Assert.Equal("Грудень", page.Find("#planDialogMonthInput").GetAttribute("value"));
        page.Find("#planDialogQuantityInput").Change("3");
        page.Find(".modal-footer .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Equal(
            ["грудень 2026\u202Fр.", "лютий 2027\u202Fр."],
            PlanPeriods(page)));
        Assert.Equal("Плани (2)", page.Find("tbody .dropdown-toggle").TextContent.Trim());

        page.Find("tbody .btn-primary").Click();
        Assert.Equal("2027", page.Find("#planDialogYearInput").GetAttribute("value"));
        Assert.Equal("Січень", page.Find("#planDialogMonthInput").GetAttribute("value"));
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
    public async Task PlanInRowDropdown_IsEditedInDialogAndCanBeDeleted()
    {
        using CultureScope _ = new(PlannerCultures.English);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(Stock("0049000001", 10m));
        await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2027, 1, 2m));
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        page.Find("tbody .dropdown-toggle").Click();
        Assert.Empty(page.FindAll("#plans-2 input"));
        Assert.Equal("Edit", page.Find("#plans-2 .btn-primary").TextContent.Trim());
        page.Find("#plans-2 .btn-primary").Click();

        Assert.Equal("Edit consumption plan", page.Find("#planDialogTitle").TextContent.Trim());
        Assert.Equal("January", page.Find("#planDialogMonthInput").GetAttribute("value"));
        Assert.Equal("2", page.Find("#planDialogQuantityInput").GetAttribute("value"));
        page.Find("#planDialogMonthInput").Change("March");
        page.Find("#planDialogQuantityInput").Change("6");
        page.Find(".modal-footer .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal")));
        Assert.Equal(["March 2027"], PlanPeriods(page));
        Assert.Equal("6", page.Find("#plans-2 table td:nth-child(2)").TextContent.Trim());
        ConsumptionPlanDto updated = Assert.Single(await PlansAsync(host));
        Assert.Equal(
            (2027, 3, 6m),
            (updated.TargetYear, updated.TargetMonth, updated.PlannedQuantity));

        page.Find("#plans-2 .btn-outline-danger").Click();
        page.Find(".modal-footer .btn-danger").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("#plans-2")));
        Assert.Empty(await PlansAsync(host));
    }

    [Theory]
    [InlineData(PlannerCultures.English, "10/28/2026", "11/1/2026", "11/10/2025")]
    [InlineData(PlannerCultures.Ukrainian, "28.10.2026", "01.11.2026", "10.11.2025")]
    public async Task StockRows_ShowDatesInUiCulture_AndAgeHeaderCyclesServerSort(
        string culture,
        string received,
        string created,
        string createdYearAgo)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2) with
            {
                GoodsReceiptDate = new DateOnly(2026, 10, 28),
                StockCreationDate = new DateOnly(2026, 11, 1)
            },
            Stock("0049000002", 10m, sourceRow: 3),
            Stock("0049000003", 10m, sourceRow: 4) with
            {
                StockCreationDate = new DateOnly(2025, 11, 10)
            });
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        Assert.Equal(
            [
                [received, created, "9"],
                ["—", "—", "—"],
                ["—", createdYearAgo, "365"]
            ],
            StockDateCells(page));
        AssertStockAgeSort(page, "none", "↕", ["0049000001", "0049000002", "0049000003"]);

        page.Find("#stockAgeSortButton").Click();
        page.WaitForAssertion(() => AssertStockAgeSort(
            page, "ascending", "▲", ["0049000001", "0049000003", "0049000002"]));

        page.Find("#stockAgeSortButton").Click();
        page.WaitForAssertion(() => AssertStockAgeSort(
            page, "descending", "▼", ["0049000003", "0049000001", "0049000002"]));

        page.Find("#stockAgeSortButton").Click();
        page.WaitForAssertion(() => AssertStockAgeSort(
            page, "none", "↕", ["0049000001", "0049000002", "0049000003"]));
    }

    [Theory]
    [InlineData(PlannerCultures.Ukrainian, "Незапланована кількість", "Вартість запасу")]
    [InlineData(PlannerCultures.English, "Unplanned Quantity", "Stock Value")]
    public async Task Rows_ShowUnplannedQuantityAndStockValue_AndValueSortCycles(
        string culture,
        string unplannedHeader,
        string valueHeader)
    {
        using CultureScope _ = new(culture);
        await using ConsumptionPlanTestHost host = await StartAsync(November10);
        await host.ImportAsync(
            Stock("0049000001", 10m, sourceRow: 2) with { TotalStockValue = 100m },
            Stock("0049000002", 4m, sourceRow: 3) with { TotalStockValue = 50m },
            Stock("0049000003", 7m, sourceRow: 4) with { TotalStockValue = 1m });
        await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2026, 12, 6m));
        await host.Get<CreatePlanHandler>().HandleAsync(
            new CreateConsumptionPlanRequest(2026, "0049000001", "0001", 2027, 1, 6m));
        await host.SetStockFiguresAsync("0049000003", null, null);
        IRenderedComponent<ConsumptionPlansPage> page = RenderPage(host);
        page.WaitForElement("table");

        string headers = CollapseWhitespace(page.Find("thead").TextContent);
        Assert.Contains(unplannedHeader, headers);
        Assert.Contains(valueHeader, headers);
        AssertValueSort(page, "descending", "▼", ["0049000001", "0049000002", "0049000003"]);
        Assert.Equal(
            [
                [FormatQuantity(10m), FormatQuantity(-2m), FormatMoney(100m)],
                [FormatQuantity(4m), FormatQuantity(4m), FormatMoney(50m)],
                ["—", "—", "—"]
            ],
            FigureCells(page));
        AssertBalanceBar(page.FindAll("tbody > tr")[0], "83.33%", null, "16.67%");
        AssertBalanceBar(page.FindAll("tbody > tr")[1], null, "100%", null);
        Assert.Null(page.FindAll("tbody > tr")[2].QuerySelector(".stock-split"));
        IElement firstRow = page.FindAll("tbody > tr")[0];
        Assert.NotNull(firstRow.QuerySelector("td.stock-balance-col .stock-split"));
        Assert.Equal(
            FormatQuantity(-2m),
            firstRow.QuerySelectorAll("td.stock-figure-col")[1].TextContent.Trim());
        string legend = CollapseWhitespace(page.Find(".stock-split-legend").TextContent);
        Assert.Contains(
            culture == PlannerCultures.Ukrainian ? "Понад залишок" : "Over stock",
            legend);

        page.Find("#stockValueSortButton").Click();
        page.WaitForAssertion(() => AssertValueSort(
            page, "none", "↕", ["0049000001", "0049000002", "0049000003"]));

        page.Find("#stockValueSortButton").Click();
        page.WaitForAssertion(() => AssertValueSort(
            page, "ascending", "▲", ["0049000002", "0049000001", "0049000003"]));

        page.Find("#stockValueSortButton").Click();
        page.WaitForAssertion(() => AssertValueSort(
            page, "descending", "▼", ["0049000001", "0049000002", "0049000003"]));
    }

    private static void AssertValueSort(
        IRenderedComponent<ConsumptionPlansPage> page,
        string ariaSort,
        string indicator,
        string[] documents)
    {
        Assert.Equal(
            ariaSort,
            page.Find("#stockValueSortButton").ParentElement!.GetAttribute("aria-sort"));
        Assert.Equal(
            indicator,
            page.Find("#stockValueSortButton [aria-hidden='true']").TextContent.Trim());
        Assert.Equal(
            documents,
            page.FindAll("tbody > tr[data-document]")
                .Select(row => row.GetAttribute("data-document")));
    }

    private static void AssertBalanceBar(
        IElement row,
        string? planned,
        string? unplanned,
        string? over)
    {
        AssertSegment(row, "stock-split-planned", planned);
        AssertSegment(row, "stock-split-unplanned", unplanned);
        AssertSegment(row, "stock-split-over", over);
    }

    private static void AssertSegment(IElement row, string className, string? width)
    {
        IElement? segment = row.QuerySelector("." + className);
        if (width is null)
        {
            Assert.Null(segment);
            return;
        }

        Assert.NotNull(segment);
        Assert.Contains("width:" + width, segment.GetAttribute("style"));
    }

    private static List<string[]> FigureCells(IRenderedComponent<ConsumptionPlansPage> page)
    {
        return [.. page.FindAll("tbody > tr").Select(row => new[]
        {
            row.QuerySelector("td:nth-child(5)")!.TextContent.Trim(),
            row.QuerySelector("td:nth-child(7)")!.TextContent.Trim(),
            row.QuerySelector("td:nth-child(8)")!.TextContent.Trim()
        })];
    }

    private static string FormatQuantity(decimal value)
    {
        return ConsumptionPlanDisplay.FormatQuantity(value);
    }

    private static string FormatMoney(decimal value) => ConsumptionPlanDisplay.FormatMoney(value);

    private static void AssertStockAgeSort(
        IRenderedComponent<ConsumptionPlansPage> page,
        string ariaSort,
        string indicator,
        string[] documents)
    {
        Assert.Equal(
            ariaSort,
            page.Find("#stockAgeSortButton").ParentElement!.GetAttribute("aria-sort"));
        Assert.Equal(
            indicator,
            page.Find("#stockAgeSortButton [aria-hidden='true']").TextContent.Trim());
        Assert.Equal(
            documents,
            page.FindAll("tbody > tr[data-document]")
                .Select(row => row.GetAttribute("data-document")));
    }

    private static List<string[]> StockDateCells(IRenderedComponent<ConsumptionPlansPage> page)
    {
        return [.. page.FindAll("tbody > tr").Select(row => new[]
        {
            row.QuerySelector("td:nth-child(9)")!.TextContent.Trim(),
            row.QuerySelector("td:nth-child(10)")!.TextContent.Trim(),
            row.QuerySelector("td:nth-child(11)")!.TextContent.Trim()
        })];
    }

    private static string CollapseWhitespace(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
