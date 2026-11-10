using System.Globalization;
using Microsoft.Extensions.Localization;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public static class StockReportFieldLocalization
{
    public static string GetLocalizedName(this StockReportFieldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        string name = metadata.GetDisplayName(CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(name) ? metadata.EnglishName : name;
    }

    public static string GetLocalizedName(this StockReportField field)
    {
        return StockReportFieldCatalog.GetMetadata(field).GetLocalizedName();
    }

    /// <summary>Returns the field name in the language that is not shown, e.g. as a header tooltip.</summary>
    public static string GetAlternateName(this StockReportFieldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.GetLocalizedName() == metadata.UkrainianName ? metadata.EnglishName : metadata.UkrainianName;
    }

    /// <summary>Returns the translated description, or the catalog text for fields without a translation.</summary>
    public static string GetLocalizedDescription(this StockReportFieldMetadata metadata, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(localizer);

        LocalizedString description = localizer[$"FieldDescription_{metadata.Key}"];
        return description.ResourceNotFound ? metadata.Description : description.Value;
    }

    public static string GetLocalizedTypeName(this StockReportFieldMetadata metadata, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(localizer);

        LocalizedString typeName = localizer[$"FieldType_{metadata.DataType}"];
        return typeName.ResourceNotFound ? metadata.DataType.ToString() : typeName.Value;
    }

    public static string GetLocalizedFieldName(string fieldKey)
    {
        return StockReportFieldCatalog.TryParseFieldKey(fieldKey, out StockReportField field)
            ? field.GetLocalizedName()
            : fieldKey;
    }
}
