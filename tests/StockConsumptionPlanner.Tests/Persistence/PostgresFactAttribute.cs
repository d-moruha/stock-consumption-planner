namespace StockConsumptionPlanner.Tests.Persistence;

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "PLANNER_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to a PostgreSQL connection string to run.";
        }
    }
}
