namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed record ConsumptionPlanDto(
    int Id,
    int MaterialDocumentYear,
    string MaterialDocumentNumber,
    string MaterialDocumentItem,
    int TargetYear,
    int TargetMonth,
    decimal PlannedQuantity,
    string MaterialNumber,
    string MaterialDescription,
    decimal? CurrentStockQuantity,
    bool ExceedsStock,
    bool IsOrphaned,
    DateTimeOffset? OrphanedAtUtc,
    DateTimeOffset? PurgeDueAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset EditableUntilUtc,
    bool IsEditable,
    bool CanDelete,
    int Version);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 500;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        int normalizedPage = Math.Max(1, page ?? 1);
        int normalizedSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (normalizedPage, normalizedSize);
    }
}
