using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;

public enum PlanningSheetSort
{
    SourceRow,
    StockAgeAscending,
    StockAgeDescending,
    StockValueAscending,
    StockValueDescending
}

public sealed record PlanningSheetQuery(
    int TargetYear,
    int TargetMonth,
    string? Search = null,
    int? Page = null,
    int? PageSize = null,
    PlanningSheetSort Sort = PlanningSheetSort.StockValueDescending);

public sealed record PlanningSheetRowDto(
    int SourceRowNumber,
    int MaterialDocumentYear,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    string MaterialNumber,
    string MaterialDescription,
    decimal? CurrentStockQuantity,
    decimal? UnplannedQuantity,
    decimal? TotalStockValue,
    ConsumptionPlanDto? Plan,
    IReadOnlyList<ConsumptionPlanDto> Plans,
    DateOnly? GoodsReceiptDate,
    DateOnly? StockCreationDate,
    int? StockAgeDays);

public sealed record PlanningSheetDto(
    int TargetYear,
    int TargetMonth,
    bool CanCreate,
    DateTimeOffset MonthStartUtc,
    DateTimeOffset LockBeforeMonthUtc,
    PagedResult<PlanningSheetRowDto> Rows);
