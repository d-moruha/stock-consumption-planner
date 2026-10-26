using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;

public sealed record PlanningSheetQuery(
    int TargetYear,
    int TargetMonth,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

public sealed record PlanningSheetRowDto(
    int SourceRowNumber,
    int MaterialDocumentYear,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    string MaterialNumber,
    string MaterialDescription,
    decimal CurrentStockQuantity,
    ConsumptionPlanDto? Plan);

public sealed record PlanningSheetDto(
    int TargetYear,
    int TargetMonth,
    bool CanCreate,
    DateTimeOffset MonthStartUtc,
    DateTimeOffset LockBeforeMonthUtc,
    PagedResult<PlanningSheetRowDto> Rows);
