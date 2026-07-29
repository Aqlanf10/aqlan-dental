using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for CreditNote entity.
/// CreditNoteStatus enum stored as string in DB for readability.
/// </summary>
public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.HasKey(cn => cn.Id);
        builder.ToTable("CreditNotes");

        builder.Property(cn => cn.Amount).HasPrecision(12, 2);
        builder.Property(cn => cn.Reason).HasMaxLength(500).IsRequired();

        // Enum stored as string in DB (column is varchar(20))
        builder.Property(cn => cn.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(CreditNoteStatus.Draft);

        // Indexes
        builder.HasIndex(cn => cn.InvoiceId);
        builder.HasIndex(cn => cn.PatientId);
        builder.HasIndex(cn => cn.Status);
        builder.HasIndex(cn => cn.BranchId);

        // Relationships
        builder.HasOne(cn => cn.Invoice)
            .WithMany()
            .HasForeignKey(cn => cn.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cn => cn.Patient)
            .WithMany()
            .HasForeignKey(cn => cn.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
