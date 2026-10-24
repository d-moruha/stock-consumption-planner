using StockConsumptionPlanner.App.Components;
using StockConsumptionPlanner.Modules.StockReport;
using StockConsumptionPlanner.Modules.StockReport.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddStockReportModule(builder.Configuration);

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();

app.MapGet("/healthz", (StockReportPersistenceOptions persistenceOptions) =>
    Results.Ok(new
    {
        status = "Healthy",
        service = "StockConsumptionPlanner.App",
        persistenceProvider = persistenceOptions.Provider,
        timestampUtc = DateTimeOffset.UtcNow
    }));

app.MapStockReportEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
