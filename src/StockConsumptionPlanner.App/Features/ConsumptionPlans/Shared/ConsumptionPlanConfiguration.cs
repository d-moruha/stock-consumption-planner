using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public sealed class ConsumptionPlanConfiguration : IEntityTypeConfiguration<ConsumptionPlan>
{
    public void Configure(EntityTypeBuilder<ConsumptionPlan> entity)
    {
        entity.ToTable("ConsumptionPlans", table =>
        {
            table.HasCheckConstraint("CK_ConsumptionPlans_TargetMonth", "\"TargetMonth\" BETWEEN 1 AND 12");
            table.HasCheckConstraint(
                "CK_ConsumptionPlans_TargetYear",
                $"\"TargetYear\" BETWEEN {PlanTargetMonth.MinYear} AND {PlanTargetMonth.MaxYear}");
            table.HasCheckConstraint("CK_ConsumptionPlans_PlannedQuantity", "\"PlannedQuantity\" > 0");
        });

        entity.HasKey(p => p.Id);
        entity.Property(p => p.MaterialDocumentNumber).HasMaxLength(128).IsRequired();
        entity.Property(p => p.MaterialDocumentItem).HasMaxLength(64).IsRequired();
        entity.Property(p => p.MaterialNumber).HasMaxLength(128).IsRequired();
        entity.Property(p => p.MaterialDescription).HasMaxLength(1024).IsRequired();
        entity.Property(p => p.PlannedQuantity).HasPrecision(18, ConsumptionPlan.QuantityScale);
        entity.Property(p => p.CreatedAtUtc).IsRequired();
        entity.Property(p => p.UpdatedAtUtc).IsRequired();
        entity.Property(p => p.Version).IsConcurrencyToken();

        entity.Ignore(p => p.Key);
        entity.Ignore(p => p.Target);
        entity.Ignore(p => p.IsOrphaned);

        entity.HasIndex(p => new
            {
                p.MaterialDocumentNumber,
                p.MaterialDocumentItem,
                p.MaterialDocumentYear,
                p.TargetYear,
                p.TargetMonth
            })
            .IsUnique()
            .HasDatabaseName("UX_ConsumptionPlans_Key_Target");
        entity.HasIndex(p => new { p.TargetYear, p.TargetMonth });
        entity.HasIndex(p => p.OrphanedAtUtc);
    }
}
