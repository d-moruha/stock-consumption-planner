using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.StockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.ConsumptionPlans;

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Service provider wired like the app, on a temp-file SQLite database, with a controllable clock.</summary>
internal sealed class ConsumptionPlanTestHost : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly List<IHostedService> _hostedServices;

    private ConsumptionPlanTestHost(
        string dbPath,
        ServiceProvider services,
        MutableTimeProvider clock,
        List<IHostedService> hostedServices)
    {
        _dbPath = dbPath;
        Services = services;
        Clock = clock;
        _hostedServices = hostedServices;
    }

    public ServiceProvider Services { get; }

    public MutableTimeProvider Clock { get; }

    public EfCoreStockReportRepository Repository =>
        Assert.IsType<EfCoreStockReportRepository>(Services.GetRequiredService<IStockReportRepository>());

    public static async Task<ConsumptionPlanTestHost> StartAsync(
        DateTimeOffset utcNow,
        Action<IServiceCollection>? configureServices = null)
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-plans-{Guid.NewGuid():N}.db");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False",
                ["ConsumptionPlans:PurgeInterval"] = "00:00:00"
            })
            .Build();

        MutableTimeProvider clock = new(utcNow);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddStockReportFeature(configuration);
        services.AddConsumptionPlansFeature(configuration);
        configureServices?.Invoke(services);

        ServiceProvider provider = services.BuildServiceProvider();
        List<IHostedService> hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (IHostedService hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        return new ConsumptionPlanTestHost(dbPath, provider, clock, hostedServices);
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public Task ImportAsync(params StockReportItem[] items)
    {
        return Repository.SaveCurrentAsync(new StockReportBatch("test.xlsx", "Sheet1", Clock.GetUtcNow(), items));
    }

    public static StockReportItem Stock(
        string documentNumber,
        decimal quantity,
        string documentItem = "0001",
        int documentYear = 2026,
        int sourceRow = 2)
    {
        return new StockReportItem(
            sourceRow,
            documentNumber,
            documentItem,
            documentYear,
            $"MAT-{documentNumber}",
            $"Material {documentNumber}",
            quantity,
            quantity * 10m);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IHostedService hostedService in _hostedServices)
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        await Services.DisposeAsync();

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup lock races in test teardown.
        }
    }
}
