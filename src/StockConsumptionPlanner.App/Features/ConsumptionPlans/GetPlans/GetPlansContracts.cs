namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;

public enum PlanLinkFilter
{
    All,
    Linked,
    Orphaned
}

public sealed record GetPlansQuery(
    int? TargetYear = null,
    int? TargetMonth = null,
    PlanLinkFilter Status = PlanLinkFilter.All,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);
