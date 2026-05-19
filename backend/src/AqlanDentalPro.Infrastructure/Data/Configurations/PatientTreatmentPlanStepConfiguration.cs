using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class PatientTreatmentPlanStepConfiguration : IEntityTypeConfiguration<PatientTreatmentPlanStep>
{
    public void Configure(EntityTypeBuilder<PatientTreatmentPlanStep> builder)
    {
        builder.HasKey(s => s.Id);

        // String properties
        builder.Property(s => s.ServiceNameSnapshot).HasMaxLength(200).IsRequired(false);
        builder.Property(s => s.ToothNumber).HasMaxLength(10).IsRequired(false);
        builder.Property(s => s.ToothArea).HasMaxLength(100).IsRequired(false);
        builder.Property(s => s.Title).HasMaxLength(300).IsRequired();
        builder.Property(s => s.Description).IsRequired(false);
        builder.Property(s => s.Notes).IsRequired(false);

        // Enum conversions
        builder.Property(s => s.Department).HasConversion<string>().HasMaxLength(50).IsRequired(false);
        builder.Property(s => s.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        // Nullable decimal
        builder.Property(s => s.EstimatedCost).HasPrecision(12, 2).IsRequired(false);

        // Nullable date fields
        builder.Property(s => s.PlannedDate).IsRequired(false);
        builder.Property(s => s.CompletedDate).IsRequired(false);

        // Nullable FK fields
        builder.Property(s => s.ServiceId).IsRequired(false);
        builder.Property(s => s.ResponsibleDoctorId).IsRequired(false);
        builder.Property(s => s.RelatedAppointmentId).IsRequired(false);
        builder.Property(s => s.RelatedVisitId).IsRequired(false);
        builder.Property(s => s.CreatedBy).IsRequired(false);
        builder.Property(s => s.UpdatedBy).IsRequired(false);

        // Indexes
        builder.HasIndex(s => s.PatientId);
        builder.HasIndex(s => s.ServiceId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.ResponsibleDoctorId);
        builder.HasIndex(s => s.RelatedAppointmentId);
        builder.HasIndex(s => s.RelatedVisitId);
        builder.HasIndex(s => s.SequenceNumber);

        // Relationships
        builder.HasOne(s => s.Patient)
            .WithMany()
            .HasForeignKey(s => s.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Service)
            .WithMany()
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.ResponsibleDoctor)
            .WithMany()
            .HasForeignKey(s => s.ResponsibleDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.RelatedAppointment)
            .WithMany()
            .HasForeignKey(s => s.RelatedAppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.RelatedVisit)
            .WithMany()
            .HasForeignKey(s => s.RelatedVisitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
