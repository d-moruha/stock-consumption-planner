using System.Globalization;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public sealed record StockReportFieldMetadata(
    StockReportField Field,
    string EnglishName,
    string UkrainianName,
    string SapField,
    StockReportFieldType DataType,
    bool IsRequired,
    int? MaxLength,
    string Description,
    IReadOnlyList<string> Aliases)
{
    public string Key => Field.ToString();

    public IEnumerable<string> HeaderForms => new[] { Key, EnglishName, UkrainianName }.Concat(Aliases);

    public string GetDisplayName(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return string.Equals(culture.TwoLetterISOLanguageName, "uk", StringComparison.OrdinalIgnoreCase)
            ? UkrainianName
            : EnglishName;
    }
}

public static class StockReportFieldCatalog
{
    public const int CodeMaxLength = 64;

    public const int TextMaxLength = 256;

    public static IReadOnlyList<StockReportFieldMetadata> All { get; } =
    [
        new(
            StockReportField.MaterialDocumentNumber,
            "Material Document",
            "Документ матеріалу",
            "MBLNR",
            StockReportFieldType.Text,
            true,
            128,
            "Material document number (MB51). Part of the stock key; leading zeros are kept.",
            [
                "Material Document Number", "Material Doc.", "Mat. Doc.", "Number of Material Document",
                "MBLNR", "MatDoc", "Document Number", "Номер документа матеріалу", "№ документа"
            ]),
        new(
            StockReportField.MaterialDocumentItem,
            "Material Document Item",
            "Позиція у док.мат",
            "ZEILE",
            StockReportFieldType.Text,
            true,
            64,
            "Item in the material document. Part of the stock key; leading zeros are kept (e.g. 0001).",
            [
                "Item in Material Document", "Material Doc.Item", "Mat. Doc. Item", "MatDoc Item", "ZEILE",
                "Позиція у документі матеріалу", "Позиція документа матеріалу", "Позиція документа"
            ]),
        new(
            StockReportField.MaterialDocumentYear,
            "Material Document Year",
            "Рік документа матер.",
            "MJAHR",
            StockReportFieldType.Year,
            true,
            null,
            "4-digit material document year. Part of the stock key.",
            [
                "Material Doc. Year", "Mat. Doc. Year", "MaterialDccoumentYear", "Document Year", "MJAHR",
                "Рік документа матеріалу", "Рік документа"
            ]),
        new(
            StockReportField.WbsElement,
            "WBS Element",
            "Елемент ієрарх.стр.робіт",
            "PS_PSP_PNR / MAT_PSPNR",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Work breakdown structure element of project stock (special stock Q), as the external WBS key.",
            [
                "Work Breakdown Structure Element", "WBS", "Valuated Sales Order Stock WBS Element",
                "Елемент ієрархічної структури робіт", "СПП-елемент"
            ]),
        new(
            StockReportField.BatchNumber,
            "Batch",
            "Партія",
            "CHARG",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Batch number; leading zeros are kept.",
            ["Batch Number", "CHARG", "Номер партії"]),
        new(
            StockReportField.MaterialNumber,
            "Material",
            "Матеріал",
            "MATNR",
            StockReportFieldType.Text,
            true,
            128,
            "Material number; leading zeros are kept.",
            [
                "Material Number", "Material Code", "MATNR", "Nomenclature", "SKU",
                "Код матеріалу", "Номенклатура", "Номенклатурний номер", "Артикул"
            ]),
        new(
            StockReportField.MaterialDescription,
            "Material Description",
            "Опис матеріалу",
            "MAKTX",
            StockReportFieldType.Text,
            true,
            1024,
            "Material short text.",
            [
                "Material Text", "Material Short Text", "MAKTX", "Description",
                "Найменування матеріалу", "Найменування", "Назва матеріалу", "Короткий текст матеріалу"
            ]),
        new(
            StockReportField.StorageLocation,
            "Storage Location",
            "Місце збереження",
            "LGORT",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Storage location within the plant.",
            ["Stor. Location", "SLoc", "LGORT", "Склад"]),
        new(
            StockReportField.MovementType,
            "Movement Type",
            "Тип руху",
            "BWART",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Inventory management movement type of the material document item (e.g. 101).",
            ["Movement Type (Inventory Management)", "MvT", "BWART", "Вид руху"]),
        new(
            StockReportField.TotalStockQuantity,
            "Total Stock",
            "Загальний запас",
            "LBKUM",
            StockReportFieldType.Decimal,
            true,
            null,
            "Stock quantity of the document item on the report date, in the base unit of measure.",
            [
                "Total Stock Quantity", "Total Valuated Stock", "MaterialQuantity", "Stock Quantity", "Quantity",
                "LBKUM", "Кількість", "Залишок"
            ]),
        new(
            StockReportField.BaseUnitOfMeasure,
            "Base Unit of Measure",
            "Базис.один.виміру",
            "MEINS",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Base unit of measure of the stock quantity.",
            [
                "Base Unit", "BUn", "MEINS", "Unit of Measure",
                "Базисна одиниця виміру", "Одиниця виміру", "БОВ"
            ]),
        new(
            StockReportField.TotalStockValue,
            "Total Value",
            "Загальна вартість",
            "SALK3",
            StockReportFieldType.Decimal,
            true,
            null,
            "Stock value of the document item on the report date, in local currency.",
            [
                "Total Stock Value", "Value of Total Valuated Stock", "MaterialValue", "Stock Value", "Value",
                "SALK3", "Вартість", "Сума", "Вартість залишку"
            ]),
        new(
            StockReportField.LocalCurrency,
            "Local Currency",
            "Внутрішня валюта",
            "WAERS",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Currency key of the total value (company code currency).",
            ["Currency", "Currency Key", "Crcy", "WAERS", "Валюта"]),
        new(
            StockReportField.SpecialStockIndicator,
            "Special Stock",
            "Особливий запас",
            "SOBKZ",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Special stock indicator (e.g. E sales order stock, K vendor consignment, Q project stock).",
            ["Special Stock Indicator", "SOBKZ", "Індикатор особливого запасу"]),
        new(
            StockReportField.CrossPlantMaterialStatus,
            "Cross-Plant Material Status",
            "Ст.мат.для кіл.зав",
            "MSTAE",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Material status valid in all plants (material master, client level).",
            [
                "X-plant matl status", "X-Plant Material Status", "MSTAE",
                "Статус матеріалу для кількох заводів"
            ]),
        new(
            StockReportField.PlantSpecificMaterialStatus,
            "Plant-Specific Material Status",
            "Ст.мат.віднос.заводу",
            "MMSTA",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Material status in the plant of the stock (material master, plant level).",
            [
                "Plant-sp.matl status", "Plant-Sp. Material Status", "MMSTA",
                "Статус матеріалу відносно заводу"
            ]),
        new(
            StockReportField.ClientLevelDeletionFlag,
            "Deletion Flag at Client Level",
            "МВ на рівні мандата",
            "MARA-LVORM",
            StockReportFieldType.Flag,
            false,
            null,
            "Material is flagged for deletion at client level (X = flagged).",
            [
                "DF at client level", "Client-Level Deletion Flag", "Flag Material for Deletion at Client Level",
                "Мітка видалення на рівні мандата"
            ]),
        new(
            StockReportField.PlantLevelDeletionFlag,
            "Deletion Flag at Plant Level",
            "МВ на рівні заводу",
            "MARC-LVORM",
            StockReportFieldType.Flag,
            false,
            null,
            "Material is flagged for deletion in the plant (X = flagged).",
            [
                "DF at plant level", "Plant-Level Deletion Flag", "Flag Material for Deletion at Plant Level",
                "Мітка видалення на рівні заводу"
            ]),
        new(
            StockReportField.StockCreationDate,
            "Stock Creation Date",
            "Дата утворення запасу",
            "custom",
            StockReportFieldType.Date,
            false,
            null,
            "Date the stock of the document item came into existence (custom report column).",
            ["Date of Stock Creation", "Дата створення запасу"]),
        new(
            StockReportField.StockType,
            "Stock Type",
            "Тип запасу",
            "LBBSA_SID / INSMK",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Stock type of the goods movement (e.g. 01 unrestricted use, 02 quality inspection, 07 blocked).",
            ["LBBSA_SID", "INSMK", "Вид запасу"]),
        new(
            StockReportField.StockTypeDescription,
            "Stock Type Description",
            "Опис типу запасу",
            "text of stock type",
            StockReportFieldType.Text,
            false,
            TextMaxLength,
            "Text of the stock type.",
            ["Stock Type Text", "Назва типу запасу"]),
        new(
            StockReportField.PurchaseOrder,
            "Purchase Order",
            "Документ закупівлі",
            "EBELN",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Purchase order of the goods receipt (SAP 45* documents).",
            ["Purchasing Document", "Purchase Order Number", "Purch.Doc.", "EBELN", "Замовлення на поставку"]),
        new(
            StockReportField.PurchaseOrderItem,
            "Purchase Order Item",
            "Позиція",
            "EBELP",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Item of the purchase order; leading zeros are kept (e.g. 00010).",
            [
                "Item Number of Purchasing Document", "Purchasing Document Item", "PO Item", "EBELP",
                "Позиція документа закупівлі", "Позиція замовлення"
            ]),
        new(
            StockReportField.PurchaseRequisition,
            "Purchase Requisition",
            "Заявка на купівлю",
            "BANFN",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Purchase requisition behind the purchase order (SAP 37*, 48* documents).",
            ["Purchase Requisition Number", "Purch.Req.", "PReq", "BANFN", "Заявка на закупівлю"]),
        new(
            StockReportField.PurchaseRequisitionItem,
            "Requisition Item",
            "Позиція заявки",
            "BNFPO",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Item of the purchase requisition; leading zeros are kept (e.g. 00010).",
            [
                "Item Number of Purchase Requisition", "Purchase Requisition Item", "PReq Item", "BNFPO",
                "Позиція заявки на купівлю"
            ]),
        new(
            StockReportField.GoodsReceiptDate,
            "Goods Receipt Date",
            "Дата надходж.товарів від постачальника",
            "custom (GR posting date)",
            StockReportFieldType.Date,
            false,
            null,
            "Date the goods were received from the vendor (custom report column).",
            [
                "Date of Goods Receipt from Vendor", "GR Date", "Date of Last Goods Receipt",
                "Дата надходження товарів від постачальника", "Дата надходження товарів"
            ]),
        new(
            StockReportField.Vendor,
            "Vendor",
            "Постачальник",
            "LIFNR",
            StockReportFieldType.Text,
            false,
            CodeMaxLength,
            "Vendor (supplier) account number; leading zeros are kept.",
            ["Supplier", "Vendor Account Number", "LIFNR", "Код постачальника"]),
        new(
            StockReportField.VendorName,
            "Vendor Name",
            "Ім`я постачальника",
            "LFA1-NAME1",
            StockReportFieldType.Text,
            false,
            TextMaxLength,
            "Name of the vendor.",
            ["Name of Vendor", "Supplier Name", "NAME1", "Назва постачальника", "Найменування постачальника"])
    ];

    public static IEnumerable<StockReportFieldMetadata> Required => All.Where(metadata => metadata.IsRequired);

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

        if (Enum.TryParse(rawKey.Trim(), ignoreCase: true, out field) && Enum.IsDefined(field))
        {
            return true;
        }

        string normalized = NormalizeHeaderToken(rawKey);
        foreach (StockReportFieldMetadata metadata in All)
        {
            if (metadata.HeaderForms.Any(form => NormalizeHeaderToken(form) == normalized))
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
        return string.Concat(StockReportHeaderMatcher.Tokenize(input));
    }
}
