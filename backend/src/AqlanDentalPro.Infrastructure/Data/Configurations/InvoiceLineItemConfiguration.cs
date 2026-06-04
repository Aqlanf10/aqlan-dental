using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(l => l.Id);

        builder.ToTable("InvoiceLineItems");

        // String properties
        builder.Property(l => l.ServiceNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();

        // Decimal precision
        builder.Property(l => l.UnitPrice).HasPrecision(12, 2);
        builder.Property(l => l.TotalPrice).HasPrecision(12, 2);

        // Nullable FK fields
        builder.Property(l => l.ServiceId).IsRequired(false);
        builder.Property(l => l.RelatedTreatmentPlanStepId).IsRequired(false);
        builder.Property(l => l.RelatedVisitId).IsRequired(false);

        // Indexes
        builder.HasIndex(l => l.InvoiceId);
        builder.HasIndex(l => l.ServiceId);
        builder.HasIndex(l => l.DoctorId);
        builder.HasIndex(l => l.CommissionStatus);

        // Relationships
        builder.HasOne(l => l.Service)
            .WithMany()
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.RelatedTreatmentPlanStep)
            .WithMany()
            .HasForeignKey(l => l.RelatedTreatmentPlanStepId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.RelatedVisit)
            .WithMany()
            .HasForeignKey(l => l.RelatedVisitId)
            .OnDelete(DeleteBehavior.SetNull);

        // LabOrder relationship — must be explicitly configured to prevent EF Core
        // from creating a shadow "LabOrderId1" column (conflict with LabOrderId FK property)
        builder.HasOne(l => l.LabOrder)
            .WithMany()
            .HasForeignKey(l => l.LabOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
