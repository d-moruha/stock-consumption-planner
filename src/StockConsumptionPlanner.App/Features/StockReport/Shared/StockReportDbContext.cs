using Microsoft.EntityFrameworkCore;

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

    public string MaterialNumber { get; set; } = string.Empty;

    public string MaterialDescription { get; set; } = string.Empty;

    public decimal MaterialQuantity { get; set; }

    public decimal MaterialValue { get; set; }

    public StockLifecycleStatus LifecycleStatus { get; set; } = StockLifecycleStatus.Staging;
}

public sealed class StockReportDbContext(DbContextOptions<StockReportDbContext> options) : DbContext(options)
{
    public DbSet<StockReportBatchEntity> StockReportBatches => Set<StockReportBatchEntity>();

    public DbSet<StockReportItemEntity> StockReportItems => Set<StockReportItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockReportBatchEntity>(entity =>
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
        });

        modelBuilder.Entity<StockReportItemEntity>(entity =>
        {
            entity.ToTable("StockReportItems");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.MaterialDocumentNumber).HasMaxLength(128).IsRequired();
            entity.Property(i => i.MaterialDocumentItem).HasMaxLength(64).IsRequired();
            entity.Property(i => i.MaterialNumber).HasMaxLength(128).IsRequired();
            entity.Property(i => i.MaterialDescription).HasMaxLength(1024).IsRequired();
            entity.Property(i => i.MaterialQuantity).HasPrecision(18, 4);
            entity.Property(i => i.MaterialValue).HasPrecision(18, 2);
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
        });
    }
}
