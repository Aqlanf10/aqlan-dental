using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// H1/M2 FIX: FluentAPI configuration for Contract entity.
/// ContractStatus enum stored as string in DB for backward compatibility.
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

        // M2 FIX: Enum conversion — stored as string in DB, mapped to ContractStatus enum in C#
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // String constraints
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
