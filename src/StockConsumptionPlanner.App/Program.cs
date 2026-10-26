using StockConsumptionPlanner.App.Components;
using StockConsumptionPlanner.App.Features.ConsumptionPlans;
using StockConsumptionPlanner.App.Features.StockReport;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddStockReportFeature(builder.Configuration);
builder.Services.AddConsumptionPlansFeature(builder.Configuration);

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
app.MapConsumptionPlanEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
