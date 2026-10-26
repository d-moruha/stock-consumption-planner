using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;

public static class CreatePlanEndpoint
{
    public static IEndpointRouteBuilder MapCreatePlanEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapPost("/", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        CreateConsumptionPlanRequest request,
        CreatePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ConsumptionPlanResult<ConsumptionPlanDto> result = await handler.HandleAsync(request, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/consumption-plans/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }
}
