namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;

public sealed record UpdateConsumptionPlanRequest(
    decimal PlannedQuantity,
    int Version,
    int? TargetYear = null,
    int? TargetMonth = null);
