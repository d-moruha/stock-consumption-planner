using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;

public static class GetPlanningSheetEndpoint
{
    public static IEndpointRouteBuilder MapGetPlanningSheetEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapGet("/planning-sheet", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        GetPlanningSheetHandler handler,
        int year,
        int month,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        ConsumptionPlanResult<PlanningSheetDto> result = await handler.HandleAsync(
            new PlanningSheetQuery(year, month, search, page, pageSize),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
