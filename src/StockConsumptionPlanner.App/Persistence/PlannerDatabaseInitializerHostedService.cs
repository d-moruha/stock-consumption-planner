using Microsoft.EntityFrameworkCore;

namespace StockConsumptionPlanner.App.Persistence;

/// <summary>
/// Creates the full schema of <see cref="PlannerDbContext"/> on a fresh database.
/// EnsureCreated does nothing when any table already exists, so databases created by an
/// older build must be deleted once to pick up new tables.
/// </summary>
public sealed class PlannerDatabaseInitializerHostedService(
    IDbContextFactory<PlannerDbContext> dbContextFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using PlannerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
