namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;

public sealed record CreateConsumptionPlanRequest(
    int MaterialDocumentYear,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int TargetYear,
    int TargetMonth,
    decimal PlannedQuantity);
