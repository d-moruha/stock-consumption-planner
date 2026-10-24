namespace StockConsumptionPlanner.Modules.StockReport.Contracts;

public sealed class StockReportColumnMappingDto
{
    public string? SheetName { get; set; }

    public int HeaderRowNumber { get; set; } = 1;

    public int DataStartRowNumber { get; set; } = 2;

    public Dictionary<StockReportField, int> FieldColumnMap { get; set; } = new();
}
