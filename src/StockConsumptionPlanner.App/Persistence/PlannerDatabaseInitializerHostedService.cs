using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace StockConsumptionPlanner.App.Persistence;

/// <summary>
/// Creates the schema of <see cref="PlannerDbContext"/> on startup. EnsureCreated never alters
/// existing tables and the app has no migrations, so a database whose stored schema hash differs
/// from the current model is dropped and recreated, deleting its data.
/// </summary>
public sealed class PlannerDatabaseInitializerHostedService(
    IDbContextFactory<PlannerDbContext> dbContextFactory,
    ILogger<PlannerDatabaseInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using PlannerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string modelHash = ComputeModelHash(dbContext);
        IRelationalDatabaseCreator creator = dbContext.GetService<IRelationalDatabaseCreator>();

        if (await creator.ExistsAsync(cancellationToken) &&
            await creator.HasTablesAsync(cancellationToken))
        {
            string? storedHash = await ReadStoredHashAsync(dbContext, cancellationToken);
            if (storedHash == modelHash)
            {
                return;
            }

            logger.LogWarning(
                "The database schema does not match the current model (stored schema hash: " +
                "{StoredHash}, current: {ModelHash}). Dropping and recreating the database; " +
                "all existing data is deleted.",
                storedHash ?? "none",
                modelHash);
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        dbContext.Add(new PlannerSchemaVersion { Hash = modelHash });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string ComputeModelHash(PlannerDbContext dbContext)
    {
        string createScript = dbContext.Database.GenerateCreateScript().ReplaceLineEndings("\n");
        byte[] script = Encoding.UTF8.GetBytes(createScript);
        return Convert.ToHexString(SHA256.HashData(script));
    }

    private static async Task<string?> ReadStoredHashAsync(
        PlannerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT \"Hash\" FROM \"{PlannerSchemaVersion.TableName}\"";
            return await command.ExecuteScalarAsync(cancellationToken) as string;
        }
        catch (DbException)
        {
            return null;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
