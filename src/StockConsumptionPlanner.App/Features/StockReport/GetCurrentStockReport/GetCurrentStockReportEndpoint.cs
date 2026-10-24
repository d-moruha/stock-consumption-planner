using StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.GetCurrentStockReport;

public static class GetCurrentStockReportEndpoint
{
    public static IEndpointRouteBuilder MapGetCurrentStockReportEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapGet("/current", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        IStockReportRepository repository,
        CancellationToken cancellationToken)
    {
        StockReportBatch? batch = await repository.GetCurrentAsync(cancellationToken);
        if (batch is null)
        {
            return Results.Ok(new CurrentStockReportDto(
                false,
                null,
                null,
                null,
                0,
                0m,
                0m,
                []));
        }

        List<StockReportItemDto> items = batch.Items
            .Select(item => new StockReportItemDto(
                item.SourceRowNumber,
                item.MaterialDocumentNumber,
                item.MaterialDocumentItem,
                item.MaterialDocumentYear,
                item.MaterialNumber,
                item.MaterialDescription,
                item.MaterialQuantity,
                item.MaterialValue))
            .ToList();

        return Results.Ok(new CurrentStockReportDto(
            true,
            batch.SourceFileName,
            batch.SheetName,
            batch.ImportedAtUtc,
            batch.TotalRows,
            batch.TotalQuantity,
            batch.TotalValue,
            items));
    }
}
