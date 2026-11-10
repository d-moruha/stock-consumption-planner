using System.Globalization;
using StockConsumptionPlanner.App.Features.StockReport.Shared;

namespace StockConsumptionPlanner.Tests.Features.StockReport;

public sealed class StockReportHeaderMatcherTests
{
    public static readonly string[] SapUkrainianHeaders =
    [
        "Документ матеріалу",
        "Позиція у док.мат",
        "Рік документа матер.",
        "Елемент ієрарх.стр.робіт",
        "Партія",
        "Матеріал",
        "Опис матеріалу",
        "Місце збереження",
        "Тип руху",
        "Загальний запас",
        "Базис.один.виміру",
        "Загальна вартість",
        "Внутрішня валюта",
        "Особливий запас",
        "Ст.мат.для кіл.зав",
        "Ст.мат.віднос.заводу",
        "МВ на рівні мандата",
        "МВ на рівні заводу",
        "Дата утворення запасу",
        "Тип запасу",
        "Опис типу запасу",
        "Документ закупівлі",
        "Позиція",
        "Заявка на купівлю",
        "Позиція заявки",
        "Дата надходж.товарів від постачальника",
        "Постачальник",
        "Ім`я постачальника"
    ];

    [Fact]
    public void SuggestMapping_SapUkrainianHeaders_MapsEveryFieldToItsColumn()
    {
        Dictionary<StockReportField, int> mapping = StockReportHeaderMatcher.SuggestMapping(
            SapUkrainianHeaders.Select((header, index) => (index + 1, header)).ToList());

        Assert.Equal(StockReportFieldCatalog.All.Count, mapping.Count);
        for (int index = 0; index < StockReportFieldCatalog.All.Count; index++)
        {
            Assert.Equal(index + 1, mapping[StockReportFieldCatalog.All[index].Field]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuggestMapping_ItemHeaders_DoNotConfusePurchaseOrderItemWithOtherItems(bool reversed)
    {
        List<string> headers = ["Позиція", "Позиція заявки", "Позиція у док.мат"];
        if (reversed)
        {
            headers.Reverse();
        }

        Dictionary<StockReportField, int> mapping = StockReportHeaderMatcher.SuggestMapping(
            headers.Select((header, index) => (index + 1, header)).ToList());

        Assert.Equal(headers.IndexOf("Позиція") + 1, mapping[StockReportField.PurchaseOrderItem]);
        Assert.Equal(headers.IndexOf("Позиція заявки") + 1, mapping[StockReportField.PurchaseRequisitionItem]);
        Assert.Equal(headers.IndexOf("Позиція у док.мат") + 1, mapping[StockReportField.MaterialDocumentItem]);
    }

    [Fact]
    public void SuggestMapping_TruncatedMaterialDocumentItemNextToPurchaseOrderItem_KeepsBothApart()
    {
        Dictionary<StockReportField, int> mapping = StockReportHeaderMatcher.SuggestMapping(
        [
            (1, "Позиція у д"),
            (2, "Позиція")
        ]);

        Assert.Equal(1, mapping[StockReportField.MaterialDocumentItem]);
        Assert.Equal(2, mapping[StockReportField.PurchaseOrderItem]);
    }

    [Theory]
    [InlineData("Позиція у док.", StockReportField.MaterialDocumentItem)]
    [InlineData("Рік док.мат.", StockReportField.MaterialDocumentYear)]
    [InlineData("Елемент ієрарх.стр.роб", StockReportField.WbsElement)]
    [InlineData("Базисна одиниця виміру", StockReportField.BaseUnitOfMeasure)]
    [InlineData("Базис.од.вим.", StockReportField.BaseUnitOfMeasure)]
    [InlineData("Загальна варт.", StockReportField.TotalStockValue)]
    [InlineData("Ст.мат.для кіл.з", StockReportField.CrossPlantMaterialStatus)]
    [InlineData("Ст.мат.відн.зав.", StockReportField.PlantSpecificMaterialStatus)]
    [InlineData("МВ на рівні ман", StockReportField.ClientLevelDeletionFlag)]
    [InlineData("МВ на рівні зав", StockReportField.PlantLevelDeletionFlag)]
    [InlineData("Дата утвор.запасу", StockReportField.StockCreationDate)]
    [InlineData("Дата надходж.товарів від пост", StockReportField.GoodsReceiptDate)]
    [InlineData("Ім'я постач.", StockReportField.VendorName)]
    [InlineData("Ім’я постачальника", StockReportField.VendorName)]
    [InlineData("Material Doc. Year", StockReportField.MaterialDocumentYear)]
    [InlineData("X-plant matl status", StockReportField.CrossPlantMaterialStatus)]
    [InlineData("DF at plant level", StockReportField.PlantLevelDeletionFlag)]
    public void SuggestMapping_ShortenedHeader_MapsToField(string header, StockReportField expected)
    {
        Dictionary<StockReportField, int> mapping = StockReportHeaderMatcher.SuggestMapping([(1, header)]);

        Assert.Equal(expected, Assert.Single(mapping).Key);
    }

    [Theory]
    [InlineData("Склад (Ігнорувати)")]
    [InlineData("Внутрішній коментар")]
    [InlineData("Column A")]
    public void SuggestMapping_UnrelatedHeader_IsNotMapped(string header)
    {
        Assert.Empty(StockReportHeaderMatcher.SuggestMapping([(1, header)]));
    }

    [Fact]
    public void Catalog_ExposesUkrainianAndEnglishDisplayNames()
    {
        StockReportFieldMetadata metadata = StockReportFieldCatalog.GetMetadata(StockReportField.PurchaseOrderItem);

        Assert.Equal("Позиція", metadata.GetDisplayName(CultureInfo.GetCultureInfo("uk-UA")));
        Assert.Equal("Purchase Order Item", metadata.GetDisplayName(CultureInfo.GetCultureInfo("en-US")));
        Assert.All(StockReportFieldCatalog.All, field =>
        {
            Assert.False(string.IsNullOrWhiteSpace(field.EnglishName));
            Assert.False(string.IsNullOrWhiteSpace(field.UkrainianName));
        });
    }
}
