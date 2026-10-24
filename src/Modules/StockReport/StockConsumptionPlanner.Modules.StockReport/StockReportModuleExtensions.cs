using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockConsumptionPlanner.Modules.StockReport.Application;
using StockConsumptionPlanner.Modules.StockReport.Infrastructure;

namespace StockConsumptionPlanner.Modules.StockReport;

public sealed class StockReportPersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "InMemory";
}

public static class StockReportModuleExtensions
{
    public static IServiceCollection AddStockReportModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        StockReportPersistenceOptions options = new();
        configuration.GetSection(StockReportPersistenceOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<ClosedXmlStockReportService>();
        services.AddSingleton<IStockReportExcelInspector>(sp => sp.GetRequiredService<ClosedXmlStockReportService>());
        services.AddSingleton<IStockReportExcelParser>(sp => sp.GetRequiredService<ClosedXmlStockReportService>());

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
}
