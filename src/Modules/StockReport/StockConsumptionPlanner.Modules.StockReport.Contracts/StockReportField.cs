using System.Text.Json.Serialization;

namespace StockConsumptionPlanner.Modules.StockReport.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockReportField
{
    MaterialDocumentNumber = 0,
    MaterialDocumentItem = 1,
    MaterialDocumentYear = 2,
    MaterialNumber = 3,
    MaterialDescription = 4,
    MaterialQuantity = 5,
    MaterialValue = 6
}
