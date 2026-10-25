namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;

public sealed record UpdateConsumptionPlanRequest(decimal PlannedQuantity, int Version);
