namespace StockConsumptionPlanner.Modules.StockReport.Contracts;

public sealed record StockReportFieldMetadata(
    StockReportField Field,
    string Key,
    string DisplayName,
    string UkrainianName,
    string DataType,
    string Description,
    IReadOnlyList<string> Aliases);

public static class StockReportFieldCatalog
{
    public static IReadOnlyList<StockReportFieldMetadata> All { get; } =
    [
        new(
            StockReportField.MaterialDocumentNumber,
            "MaterialDocumentNumber",
            "Material Document Number",
            "Документ матеріалу / № документа",
            "string",
            "ERP material document number (preserves leading zeros).",
            [
                "materialdocumentnumber", "materialdocument", "matdoc", "docnumber", "documentnumber",
                "документматеріалу", "номердокумента", "документматериала", "№документа", "мд", "документ"
            ]),
        new(
            StockReportField.MaterialDocumentItem,
            "MaterialDocumentItem",
            "Material Document Item",
            "Позиція документа",
            "string",
            "Document line item identifier (preserves leading zeros, e.g. 0001).",
            [
                "materialdocumentitem", "documentitem", "matdocitem", "lineitem", "item",
                "позиціядокумента", "позиція", "поздокумента", "поз", "рядок"
            ]),
        new(
            StockReportField.MaterialDocumentYear,
            "MaterialDocumentYear",
            "Material Document Year",
            "Рік документа",
            "int",
            "4-digit material document year (also matches MaterialDccoumentYear).",
            [
                "materialdocumentyear", "materialdccoumentyear", "documentyear", "matdocyear", "year",
                "рікдокумента", "рік", "рікдок", "годдокумента"
            ]),
        new(
            StockReportField.MaterialNumber,
            "MaterialNumber",
            "Material Number",
            "Матеріал / Номенклатура / Код",
            "string",
            "Material code or nomenclature number.",
            [
                "materialnumber", "materialcode", "material", "nomenclature", "sku", "partnumber",
                "матеріал", "кодматеріалу", "номенклатура", "код", "номенклатурнийномер", "артикул"
            ]),
        new(
            StockReportField.MaterialDescription,
            "MaterialDescription",
            "Material Description",
            "Найменування / Опис матеріалу",
            "string",
            "Material name or full text description.",
            [
                "materialdescription", "description", "materialname", "shorttext", "name",
                "найменування", "описматеріалу", "опис", "назваматеріалу", "назва", "короткийтекст", "найменуванняматеріалу"
            ]),
        new(
            StockReportField.MaterialQuantity,
            "MaterialQuantity",
            "Material Quantity",
            "Кількість",
            "decimal",
            "Stock quantity (supports . and , decimal separators).",
            [
                "materialquantity", "quantity", "qty", "stockquantity", "amountqty",
                "кількість", "кілть", "ксть", "залишоккількість", "обсяг"
            ]),
        new(
            StockReportField.MaterialValue,
            "MaterialValue",
            "Material Value",
            "Вартість / Сума",
            "decimal",
            "Total monetary value (supports . and , decimal separators).",
            [
                "materialvalue", "value", "totalvalue", "amount", "cost", "sum",
                "вартість", "сума", "загальнавартість", "цінасума", "вартістьзалишку"
            ])
    ];

    public static StockReportFieldMetadata GetMetadata(StockReportField field)
    {
        return All.First(item => item.Field == field);
    }

    public static bool TryParseFieldKey(string? rawKey, out StockReportField field)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            field = default;
            return false;
        }

        string normalized = NormalizeHeaderToken(rawKey);
        if (normalized == "materialdccoumentyear")
        {
            field = StockReportField.MaterialDocumentYear;
            return true;
        }

        if (Enum.TryParse(rawKey.Trim(), ignoreCase: true, out field))
        {
            return true;
        }

        foreach (StockReportFieldMetadata metadata in All)
        {
            if (metadata.Aliases.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                field = metadata.Field;
                return true;
            }
        }

        field = default;
        return false;
    }

    public static string NormalizeHeaderToken(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        char[] chars = input
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '№')
            .ToArray();

        return new string(chars);
    }
}
