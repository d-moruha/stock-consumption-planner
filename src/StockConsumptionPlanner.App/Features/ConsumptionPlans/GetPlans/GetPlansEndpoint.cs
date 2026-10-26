using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;

public static class GetPlansEndpoint
{
    public static IEndpointRouteBuilder MapGetPlansEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetByIdAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        GetPlansHandler handler,
        int? year,
        int? month,
        string? status,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        PlanLinkFilter filter = PlanLinkFilter.All;
        if (!string.IsNullOrWhiteSpace(status) && (!Enum.TryParse(status, ignoreCase: true, out filter) || !Enum.IsDefined(filter)))
        {
            return ConsumptionPlanError.Validation("Status must be one of: all, linked, orphaned.").ToHttpResult();
        }

        PagedResult<ConsumptionPlanDto> result = await handler.ListAsync(
            new GetPlansQuery(year, month, filter, search, page, pageSize),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        int id,
        GetPlansHandler handler,
        CancellationToken cancellationToken)
    {
        ConsumptionPlanDto? plan = await handler.GetByIdAsync(id, cancellationToken);
        return plan is null ? ConsumptionPlanError.NotFound(id).ToHttpResult() : Results.Ok(plan);
    }
}
