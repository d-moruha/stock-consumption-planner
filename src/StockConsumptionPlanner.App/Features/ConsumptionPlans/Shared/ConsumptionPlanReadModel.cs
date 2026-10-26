namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed class ConsumptionPlanReadModel(PlanEditPolicy editPolicy, ConsumptionPlanOptions options)
{
    public ConsumptionPlanDto ToDto(ConsumptionPlan plan, decimal? currentStockQuantity)
    {
        DateTimeOffset editableUntil = editPolicy.GetEditableUntilUtc(plan.CreatedAtUtc, plan.Target);
        bool withinEditPeriod = editPolicy.UtcNow < editableUntil;
        decimal? stockQuantity = plan.IsOrphaned ? null : currentStockQuantity;

        return new ConsumptionPlanDto(
            plan.Id,
            plan.MaterialDocumentYear,
            plan.MaterialDocumentNumber,
            plan.MaterialDocumentItem,
            plan.TargetYear,
            plan.TargetMonth,
            plan.PlannedQuantity,
            plan.MaterialNumber,
            plan.MaterialDescription,
            stockQuantity,
            stockQuantity.HasValue && plan.PlannedQuantity > stockQuantity.Value,
            plan.IsOrphaned,
            plan.OrphanedAtUtc,
            plan.OrphanedAtUtc?.AddMonths(options.OrphanRetentionMonths),
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc,
            editableUntil,
            withinEditPeriod && !plan.IsOrphaned,
            withinEditPeriod,
            plan.Version);
    }
}
