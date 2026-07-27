using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LabPayableConfiguration : IEntityTypeConfiguration<LabPayable>
{
    public void Configure(EntityTypeBuilder<LabPayable> builder)
    {
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasIndex(p => p.LabOrderId);
        builder.HasIndex(p => p.LabId);
        builder.HasIndex(p => p.SupplierBillId).IsUnique();
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.LabOrder)
            .WithMany()
            .HasForeignKey(p => p.LabOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // DB-09 FIX: Change Lab→LabPayable cascade to Restrict. Previously, deleting a Lab
        // would cascade-delete all its LabPayables (financial records) — losing the audit trail
        // of what was owed/paid. Now deleting a Lab with outstanding payables throws a FK violation,
        // forcing the user to resolve the payables first (or soft-delete the Lab).
        builder.HasOne(p => p.Lab)
            .WithMany()
            .HasForeignKey(p => p.LabId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.SupplierBill)
            .WithOne()
            .HasForeignKey<LabPayable>(p => p.SupplierBillId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
