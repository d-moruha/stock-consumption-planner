using StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport;

public sealed class StockReportPersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "InMemory";
}

public static class StockReportFeatureExtensions
{
    public static IServiceCollection AddStockReportFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        StockReportPersistenceOptions options = new();
        configuration.GetSection(StockReportPersistenceOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<IStockReportExcelInspector, InspectStockReportHandler>();
        services.AddSingleton<IStockReportExcelParser, ImportStockReportHandler>();

        string provider = string.IsNullOrWhiteSpace(options.Provider)
            ? "InMemory"
            : options.Provider.Trim();

        switch (provider.ToLowerInvariant())
        {
            case "inmemory":
                services.AddSingleton<IStockReportRepository, InMemoryStockReportRepository>();
                break;
            case "sqlite":
            case "postgres":
            case "postgresql":
                throw new NotSupportedException(
                    $"Persistence provider '{options.Provider}' is configured as an extension point; use 'InMemory' in this release.");
            default:
                throw new InvalidOperationException(
                    $"Unsupported Persistence:Provider '{options.Provider}'. Allowed values: InMemory, Sqlite, Postgres.");
        }

        return services;
    }

    public static IEndpointRouteBuilder MapStockReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/stock-reports")
            .DisableAntiforgery();

        group.MapInspectStockReportEndpoint();
        group.MapImportStockReportEndpoint();
        group.MapGetCurrentStockReportEndpoint();

        return endpoints;
    }
}
