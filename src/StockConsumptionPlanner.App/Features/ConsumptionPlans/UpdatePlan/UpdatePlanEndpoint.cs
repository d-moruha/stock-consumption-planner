using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;

public static class UpdatePlanEndpoint
{
    public static IEndpointRouteBuilder MapUpdatePlanEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapPut("/{id:int}", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        int id,
        UpdateConsumptionPlanRequest request,
        UpdatePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ConsumptionPlanResult<ConsumptionPlanDto> result = await handler.HandleAsync(id, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
