using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// DB-01 FIX: Fluent API configuration for Payment entity indexes.
/// H9 FIX: Added unique index on ReceiptNumber to prevent receipt number collisions.
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // DB-01 FIX: Index for querying payments by doctor
        builder.HasIndex(p => p.DoctorId);

        // DB-01 FIX: Index for querying payments by branch
        builder.HasIndex(p => p.BranchId);

        // Invoice link
        builder.HasIndex(p => p.InvoiceId);

        // H9 FIX: Unique index on ReceiptNumber to prevent duplicate receipt numbers
        builder.HasIndex(p => p.ReceiptNumber).IsUnique();
        builder.Property(p => p.ReceiptNumber).HasMaxLength(50);

        // Patient index for fast lookups
        builder.HasIndex(p => p.PatientId);

        // Contract link
        builder.HasIndex(p => p.ContractId);

        // Decimal precision for Amount
        builder.Property(p => p.Amount).HasPrecision(12, 2);

        // String constraints
        builder.Property(p => p.PaymentMethod).HasMaxLength(30);
        builder.Property(p => p.ServiceDescription).HasMaxLength(500);
        builder.Property(p => p.Specialty).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
