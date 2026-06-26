using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// YOLO-S2: EF configuration for ServiceConsumable (service ↔ inventory item link).
/// Quantity defaults to 1; Notes is optional free-form Arabic. Cascade-Restrict on
/// the Inventory FK so a material in use by a service can't be hard-deleted silently
/// (matching LabOrderItem → InventoryAdjustment behavior). Cascade on the service side
/// is OK because consumables belong to the service — deleting the service should drop
/// the consumables with it.
/// </summary>
public class ServiceConsumableConfiguration : IEntityTypeConfiguration<ServiceConsumable>
{
    public void Configure(EntityTypeBuilder<ServiceConsumable> builder)
    {
        builder.ToTable("ServiceConsumables");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasDefaultValue(1);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.Service)
            .WithMany(s => s.Consumables)
            .HasForeignKey(x => x.ClinicServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InventoryItem)
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate (service, item) pairs — quantity should be bumped, not duplicated.
        builder.HasIndex(x => new { x.ClinicServiceId, x.InventoryItemId })
            .IsUnique()
            .HasDatabaseName("IX_ServiceConsumables_ClinicServiceId_InventoryItemId");

        // Lookup indexes.
        builder.HasIndex(x => x.ClinicServiceId);
        builder.HasIndex(x => x.InventoryItemId);
    }
}
