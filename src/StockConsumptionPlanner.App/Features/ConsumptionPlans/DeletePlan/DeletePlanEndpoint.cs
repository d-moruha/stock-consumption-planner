using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.DeletePlan;

public static class DeletePlanEndpoint
{
    public static IEndpointRouteBuilder MapDeletePlanEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapDelete("/{id:int}", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        int id,
        int version,
        DeletePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ConsumptionPlanResult<bool> result = await handler.HandleAsync(id, version, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.Error!.ToHttpResult();
    }
}
