using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// H1 FIX: FluentAPI configuration for Contract entity.
/// Previously, Contract had zero configuration — no decimal precision, no FK
/// relationships, no indexes, no max-length constraints. This caused financial
/// precision loss, slow queries, and unconstrained status values.
/// </summary>
public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        // Decimal precision for financial fields
        builder.Property(c => c.TotalAmount).HasPrecision(12, 2);
        builder.Property(c => c.DownPayment).HasPrecision(12, 2);
        builder.Property(c => c.DiscountAmount).HasPrecision(12, 2);
        builder.Property(c => c.InstallmentAmount).HasPrecision(12, 2);

        // String constraints
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Specialty).HasMaxLength(100);
        builder.Property(c => c.DiscountReason).HasMaxLength(300);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        // Indexes for common query patterns
        builder.HasIndex(c => c.PatientId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedBy);

        // Relationships
        builder.HasOne(c => c.Patient)
            .WithMany(p => p.Contracts)
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
