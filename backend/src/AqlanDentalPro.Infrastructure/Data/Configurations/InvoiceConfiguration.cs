using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.ToTable("Invoices");

        // String properties
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Notes).IsRequired(false);

        // Enum conversion
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        // Decimal precision
        builder.Property(i => i.Subtotal).HasPrecision(12, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(12, 2).IsRequired(false);
        builder.Property(i => i.TaxAmount).HasPrecision(12, 2).IsRequired(false);
        builder.Property(i => i.TotalAmount).HasPrecision(12, 2);

        // Nullable FK fields
        builder.Property(i => i.VisitId).IsRequired(false);
        builder.Property(i => i.AppointmentId).IsRequired(false);
        builder.Property(i => i.CreatedBy).IsRequired(false);
        builder.Property(i => i.UpdatedBy).IsRequired(false);

        // Indexes
        builder.HasIndex(i => i.PatientId);
        builder.HasIndex(i => i.VisitId);
        builder.HasIndex(i => i.AppointmentId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();

        // Relationships
        builder.HasOne(i => i.Patient)
            .WithMany()
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Visit)
            .WithMany()
            .HasForeignKey(i => i.VisitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Appointment)
            .WithMany()
            .HasForeignKey(i => i.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.LineItems)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
