using Microsoft.Extensions.DependencyInjection.Extensions;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.CreatePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.DeletePlan;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlanningSheet;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.GetPlans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.PurgeOrphans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.ReconcileOrphans;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.UpdatePlan;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans;

/// <summary>Requires <c>AddStockReportFeature</c>, which registers the shared persistence.</summary>
public static class ConsumptionPlansFeatureExtensions
{
    public static IServiceCollection AddConsumptionPlansFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp =>
        {
            IConfiguration runtimeConfig = sp.GetService<IConfiguration>() ?? configuration;
            ConsumptionPlanOptions options = new();
            runtimeConfig.GetSection(ConsumptionPlanOptions.SectionName).Bind(options);
            options.Validate();
            return options;
        });
        services.AddSingleton(sp => new PlanEditPolicy(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ConsumptionPlanOptions>()));
        services.AddSingleton<ConsumptionPlanReadModel>();

        services.AddSingleton<CreatePlanHandler>();
        services.AddSingleton<UpdatePlanHandler>();
        services.AddSingleton<DeletePlanHandler>();
        services.AddSingleton<GetPlansHandler>();
        services.AddSingleton<GetPlanningSheetHandler>();
        services.AddSingleton<PurgeOrphanedPlansHandler>();

        services.AddSingleton<IStockPromotionParticipant, ConsumptionPlanPromotionParticipant>();
        services.AddHostedService<OrphanedPlanPurgeHostedService>();

        return services;
    }

    public static IEndpointRouteBuilder MapConsumptionPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/consumption-plans")
            .DisableAntiforgery();

        group.MapGetPlansEndpoints();
        group.MapGetPlanningSheetEndpoint();
        group.MapCreatePlanEndpoint();
        group.MapUpdatePlanEndpoint();
        group.MapDeletePlanEndpoint();

        return endpoints;
    }
}
