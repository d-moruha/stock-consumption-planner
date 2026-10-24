namespace StockConsumptionPlanner.App.Features.StockReport.InspectStockReport;

public static class InspectStockReportEndpoint
{
    public static IEndpointRouteBuilder MapInspectStockReportEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapPost("/inspect", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IStockReportExcelInspector inspector,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Expected multipart/form-data request." });
        }

        IFormCollection form = await request.ReadFormAsync(cancellationToken);
        IFormFile? file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "An .xlsx file is required." });
        }

        string? sheetName = form.TryGetValue("sheetName", out var sheetValues)
            ? sheetValues.FirstOrDefault()
            : request.Query["sheetName"].FirstOrDefault();

        int headerRowNumber = 1;
        string? rawHeaderRow = form.TryGetValue("headerRowNumber", out var headerValues)
            ? headerValues.FirstOrDefault()
            : request.Query["headerRowNumber"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rawHeaderRow) && int.TryParse(rawHeaderRow, out int parsedHeaderRow))
        {
            headerRowNumber = Math.Max(1, parsedHeaderRow);
        }

        int? dataStartRowNumber = null;
        string? rawDataStartRow = form.TryGetValue("dataStartRowNumber", out var dataStartValues)
            ? dataStartValues.FirstOrDefault()
            : request.Query["dataStartRowNumber"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rawDataStartRow) && int.TryParse(rawDataStartRow, out int parsedDataStart))
        {
            dataStartRowNumber = Math.Max(headerRowNumber + 1, parsedDataStart);
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            StockReportInspectionDto inspection = inspector.Inspect(
                memoryStream,
                file.FileName,
                sheetName,
                headerRowNumber,
                dataStartRowNumber);

            return Results.Ok(inspection);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to inspect Excel file: {ex.Message}" });
        }
    }
}
