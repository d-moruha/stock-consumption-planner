using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StockConsumptionPlanner.App.Features.StockReport.Shared;

public sealed class StockReportBatchEntity
{
    public int Id { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    public string SheetName { get; set; } = string.Empty;

    public DateTimeOffset ImportedAtUtc { get; set; }

    public StockLifecycleStatus LifecycleStatus { get; set; } = StockLifecycleStatus.Staging;

    public List<StockReportItemEntity> Items { get; set; } = [];
}

public sealed class StockReportItemEntity
{
    public int Id { get; set; }

    public int BatchId { get; set; }

    public StockReportBatchEntity? Batch { get; set; }

    public int SourceRowNumber { get; set; }

    public string MaterialDocumentNumber { get; set; } = string.Empty;

    public string MaterialDocumentItem { get; set; } = string.Empty;

    public int MaterialDocumentYear { get; set; }

    public string? WbsElement { get; set; }

    public string? BatchNumber { get; set; }

    public string MaterialNumber { get; set; } = string.Empty;

    public string MaterialDescription { get; set; } = string.Empty;

    public string? StorageLocation { get; set; }

    public string? MovementType { get; set; }

    public decimal? TotalStockQuantity { get; set; }

    public string? BaseUnitOfMeasure { get; set; }

    public decimal? TotalStockValue { get; set; }

    public string? LocalCurrency { get; set; }

    public string? SpecialStockIndicator { get; set; }

    public string? CrossPlantMaterialStatus { get; set; }

    public string? PlantSpecificMaterialStatus { get; set; }

    public bool ClientLevelDeletionFlag { get; set; }

    public bool PlantLevelDeletionFlag { get; set; }

    public DateOnly? StockCreationDate { get; set; }

    public string? StockType { get; set; }

    public string? StockTypeDescription { get; set; }

    public string? PurchaseOrder { get; set; }

    public string? PurchaseOrderItem { get; set; }

    public string? PurchaseRequisition { get; set; }

    public string? PurchaseRequisitionItem { get; set; }

    public DateOnly? GoodsReceiptDate { get; set; }

    public string? Vendor { get; set; }

    public string? VendorName { get; set; }

    public StockLifecycleStatus LifecycleStatus { get; set; } = StockLifecycleStatus.Staging;
}

public sealed class StockReportBatchEntityConfiguration : IEntityTypeConfiguration<StockReportBatchEntity>
{
    public void Configure(EntityTypeBuilder<StockReportBatchEntity> entity)
    {
        entity.ToTable("StockReportBatches");
        entity.HasKey(b => b.Id);
        entity.Property(b => b.SourceFileName).HasMaxLength(512).IsRequired();
        entity.Property(b => b.SheetName).HasMaxLength(256).IsRequired();
        entity.Property(b => b.ImportedAtUtc).IsRequired();
        entity.Property(b => b.LifecycleStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        entity.HasIndex(b => b.LifecycleStatus);

        entity.HasMany(b => b.Items)
            .WithOne(i => i.Batch)
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockReportItemEntityConfiguration : IEntityTypeConfiguration<StockReportItemEntity>
{
    public void Configure(EntityTypeBuilder<StockReportItemEntity> entity)
    {
        entity.ToTable("StockReportItems");
        entity.HasKey(i => i.Id);
        entity.Property(i => i.MaterialDocumentNumber).HasMaxLength(128).IsRequired();
        entity.Property(i => i.MaterialDocumentItem).HasMaxLength(64).IsRequired();
        entity.Property(i => i.MaterialNumber).HasMaxLength(128).IsRequired();
        entity.Property(i => i.MaterialDescription).HasMaxLength(1024).IsRequired();
        entity.Property(i => i.TotalStockQuantity).HasPrecision(18, 4);
        entity.Property(i => i.TotalStockValue).HasPrecision(18, 2);
        entity.Property(i => i.WbsElement).HasMaxLength(MaxLength(StockReportField.WbsElement));
        entity.Property(i => i.BatchNumber).HasMaxLength(MaxLength(StockReportField.BatchNumber));
        entity.Property(i => i.StorageLocation).HasMaxLength(MaxLength(StockReportField.StorageLocation));
        entity.Property(i => i.MovementType).HasMaxLength(MaxLength(StockReportField.MovementType));
        entity.Property(i => i.BaseUnitOfMeasure).HasMaxLength(MaxLength(StockReportField.BaseUnitOfMeasure));
        entity.Property(i => i.LocalCurrency).HasMaxLength(MaxLength(StockReportField.LocalCurrency));
        entity.Property(i => i.SpecialStockIndicator).HasMaxLength(MaxLength(StockReportField.SpecialStockIndicator));
        entity.Property(i => i.CrossPlantMaterialStatus)
            .HasMaxLength(MaxLength(StockReportField.CrossPlantMaterialStatus));
        entity.Property(i => i.PlantSpecificMaterialStatus)
            .HasMaxLength(MaxLength(StockReportField.PlantSpecificMaterialStatus));
        entity.Property(i => i.StockType).HasMaxLength(MaxLength(StockReportField.StockType));
        entity.Property(i => i.StockTypeDescription).HasMaxLength(MaxLength(StockReportField.StockTypeDescription));
        entity.Property(i => i.PurchaseOrder).HasMaxLength(MaxLength(StockReportField.PurchaseOrder));
        entity.Property(i => i.PurchaseOrderItem).HasMaxLength(MaxLength(StockReportField.PurchaseOrderItem));
        entity.Property(i => i.PurchaseRequisition).HasMaxLength(MaxLength(StockReportField.PurchaseRequisition));
        entity.Property(i => i.PurchaseRequisitionItem)
            .HasMaxLength(MaxLength(StockReportField.PurchaseRequisitionItem));
        entity.Property(i => i.Vendor).HasMaxLength(MaxLength(StockReportField.Vendor));
        entity.Property(i => i.VendorName).HasMaxLength(MaxLength(StockReportField.VendorName));
        entity.Property(i => i.LifecycleStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        entity.HasIndex(i => i.LifecycleStatus);
        entity.HasIndex(i => new { i.BatchId, i.SourceRowNumber });
        entity.HasIndex(i => new
        {
            i.LifecycleStatus,
            i.MaterialDocumentNumber,
            i.MaterialDocumentItem,
            i.MaterialDocumentYear
        });
        entity.HasIndex(i => new { i.LifecycleStatus, i.MaterialNumber });
        entity.HasIndex(i => new { i.LifecycleStatus, i.StockCreationDate });
    }

    private static int MaxLength(StockReportField field)
    {
        return StockReportFieldCatalog.GetMetadata(field).MaxLength
               ?? throw new InvalidOperationException($"Field '{field}' has no maximum length.");
    }
}
