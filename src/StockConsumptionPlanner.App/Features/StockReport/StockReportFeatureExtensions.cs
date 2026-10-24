using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;
using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport;

public sealed class StockReportPersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "Sqlite";

    public string? ConnectionString { get; set; }

    public Dictionary<string, string> ConnectionStrings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class StockReportFeatureExtensions
{
    public const string DefaultSqliteConnectionString = "Data Source=stock-consumption-planner.db";

    public static IServiceCollection AddStockReportFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        StockReportPersistenceOptions options = ResolvePersistenceOptions(configuration);
        services.AddSingleton(options);

        services.AddSingleton<IStockReportExcelInspector, InspectStockReportHandler>();
        services.AddSingleton<IStockReportExcelParser, ImportStockReportHandler>();

        string normalizedProvider = NormalizeProviderName(options.Provider);

        switch (normalizedProvider)
        {
            case "sqlite":
            {
                string sqliteConn = ResolveSqliteConnectionString(options, configuration);
                services.AddDbContextFactory<StockReportDbContext>((sp, builder) =>
                {
                    IConfiguration runtimeConfig = sp.GetService<IConfiguration>() ?? configuration;
                    StockReportPersistenceOptions runtimeOptions = ResolvePersistenceOptions(runtimeConfig);
                    string runtimeSqliteConn = ResolveSqliteConnectionString(runtimeOptions, runtimeConfig);
                    builder.UseSqlite(runtimeSqliteConn);
                });
                services.AddSingleton<IStockReportRepository, EfCoreStockReportRepository>();
                services.AddHostedService<StockReportDatabaseInitializerHostedService>();
                break;
            }

            case "postgres":
            {
                string postgresConn = ResolvePostgresConnectionString(options, configuration);
                services.AddDbContextFactory<StockReportDbContext>((sp, builder) =>
                {
                    IConfiguration runtimeConfig = sp.GetService<IConfiguration>() ?? configuration;
                    StockReportPersistenceOptions runtimeOptions = ResolvePersistenceOptions(runtimeConfig);
                    string runtimePostgresConn = ResolvePostgresConnectionString(runtimeOptions, runtimeConfig);
                    builder.UseNpgsql(runtimePostgresConn);
                });
                services.AddSingleton<IStockReportRepository, EfCoreStockReportRepository>();
                services.AddHostedService<StockReportDatabaseInitializerHostedService>();
                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported Persistence:Provider '{options.Provider}'. Allowed values: Sqlite, Postgres.");
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

    public static StockReportPersistenceOptions ResolvePersistenceOptions(IConfiguration configuration)
    {
        StockReportPersistenceOptions options = new();
        configuration.GetSection(StockReportPersistenceOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            options.Provider = "Sqlite";
        }
        else
        {
            options.Provider = options.Provider.Trim();
        }

        return options;
    }

    public static string NormalizeProviderName(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return "sqlite";
        }

        string trimmed = provider.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "postgresql" => "postgres",
            _ => trimmed
        };
    }

    public static string ResolveSqliteConnectionString(
        StockReportPersistenceOptions options,
        IConfiguration configuration)
    {
        string? raw = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Sqlite"))
                ? configuration.GetConnectionString("Sqlite")
                : options.ConnectionStrings.TryGetValue("Sqlite", out string? nested) && !string.IsNullOrWhiteSpace(nested)
                    ? nested
                    : null;

        string resolved = string.IsNullOrWhiteSpace(raw)
            ? DefaultSqliteConnectionString
            : raw.Trim();

        EnsureSqliteDirectoryExists(resolved);
        return resolved;
    }

    public static string ResolvePostgresConnectionString(
        StockReportPersistenceOptions options,
        IConfiguration configuration)
    {
        string? raw = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres"))
                ? configuration.GetConnectionString("Postgres")
                : !string.IsNullOrWhiteSpace(configuration.GetConnectionString("PostgreSql"))
                    ? configuration.GetConnectionString("PostgreSql")
                    : options.ConnectionStrings.TryGetValue("Postgres", out string? nested) && !string.IsNullOrWhiteSpace(nested)
                        ? nested
                        : options.ConnectionStrings.TryGetValue("PostgreSql", out string? nestedPg) && !string.IsNullOrWhiteSpace(nestedPg)
                            ? nestedPg
                            : null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is required when Persistence:Provider is 'Postgres'. " +
                "Configure 'ConnectionStrings:Postgres' or 'Persistence:ConnectionStrings:Postgres'.");
        }

        return raw.Trim();
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        try
        {
            SqliteConnectionStringBuilder builder = new(connectionString);
            string? dataSource = builder.DataSource;
            if (string.IsNullOrWhiteSpace(dataSource) ||
                string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
                builder.Mode == SqliteOpenMode.Memory)
            {
                return;
            }

            string? directory = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            // Ignore malformed connection strings during directory pre-creation; EF/SQLite will validate on connect.
        }
    }
}

public sealed class StockReportDatabaseInitializerHostedService(
    IDbContextFactory<StockReportDbContext> dbContextFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using StockReportDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
