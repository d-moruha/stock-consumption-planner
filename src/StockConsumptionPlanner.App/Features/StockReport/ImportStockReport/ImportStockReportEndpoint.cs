using System.Text.Json;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.App.Features.StockReport.ImportStockReport;

public static class ImportStockReportEndpoint
{
    public static IEndpointRouteBuilder MapImportStockReportEndpoint(this IEndpointRouteBuilder group)
    {
        group.MapPost("/import", HandleAsync);
        return group;
    }

    public static bool TryDeserializeMapping(string? rawJson, out StockReportColumnMappingDto? mapping, out string? error)
    {
        mapping = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "Column mapping JSON payload is required.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;

            StockReportColumnMappingDto dto = new();

            if (TryGetPropertyIgnoreCase(root, "sheetName", out JsonElement sheetEl) &&
                sheetEl.ValueKind == JsonValueKind.String)
            {
                dto.SheetName = sheetEl.GetString();
            }

            if (TryGetPropertyIgnoreCase(root, "headerRowNumber", out JsonElement headerEl) &&
                headerEl.TryGetInt32(out int headerRow))
            {
                dto.HeaderRowNumber = headerRow;
            }

            if (TryGetPropertyIgnoreCase(root, "dataStartRowNumber", out JsonElement dataStartEl) &&
                dataStartEl.TryGetInt32(out int dataStartRow))
            {
                dto.DataStartRowNumber = dataStartRow;
            }
            else
            {
                dto.DataStartRowNumber = dto.HeaderRowNumber + 1;
            }

            if (TryGetPropertyIgnoreCase(root, "fieldColumnMap", out JsonElement mapEl) &&
                mapEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in mapEl.EnumerateObject())
                {
                    if (!StockReportFieldCatalog.TryParseFieldKey(prop.Name, out StockReportField field))
                    {
                        error = $"Unknown stock report field key '{prop.Name}'.";
                        return false;
                    }

                    if (!TryParseColumnIndex(prop.Value, out int colIndex))
                    {
                        error = $"Invalid column index for field '{prop.Name}'.";
                        return false;
                    }

                    dto.FieldColumnMap[field] = colIndex;
                }
            }

            mapping = dto;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid mapping JSON: {ex.Message}";
            return false;
        }
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IStockReportExcelParser parser,
        IStockReportRepository repository,
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

        string? mappingJson = form.TryGetValue("mapping", out var mappingValues)
            ? mappingValues.FirstOrDefault()
            : null;

        if (!TryDeserializeMapping(mappingJson, out StockReportColumnMappingDto? mapping, out string? mappingError) ||
            mapping is null)
        {
            return Results.BadRequest(new { error = mappingError ?? "Invalid column mapping." });
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            StockReportImportResultDto result = parser.Parse(memoryStream, file.FileName, mapping);

            List<StockReportItem> domainItems = result.Items
                .Select(dto => new StockReportItem(
                    dto.SourceRowNumber,
                    dto.MaterialDocumentNumber,
                    dto.MaterialDocumentItem,
                    dto.MaterialDocumentYear,
                    dto.MaterialNumber,
                    dto.MaterialDescription,
                    dto.MaterialQuantity,
                    dto.MaterialValue))
                .ToList();

            StockReportBatch batch = new(
                result.SourceFileName,
                result.SheetName,
                result.ImportedAtUtc,
                domainItems);

            await repository.SaveCurrentAsync(batch, cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to import Excel file: {ex.Message}" });
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryParseColumnIndex(JsonElement element, out int colIndex)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out colIndex) && colIndex > 0)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            string? text = element.GetString()?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (int.TryParse(text, out colIndex) && colIndex > 0)
                {
                    return true;
                }

                if (text.All(char.IsLetter))
                {
                    int result = 0;
                    foreach (char ch in text.ToUpperInvariant())
                    {
                        result = (result * 26) + (ch - 'A' + 1);
                    }

                    colIndex = result;
                    return colIndex > 0;
                }
            }
        }

        colIndex = 0;
        return false;
    }
}
