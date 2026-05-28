using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class ClinicQueueItemConfiguration : IEntityTypeConfiguration<ClinicQueueItem>
{
    public void Configure(EntityTypeBuilder<ClinicQueueItem> builder)
    {
        builder.ToTable("ClinicQueueItems");

        builder.Property(q => q.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(q => q.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ClinicQueuePriority.Normal);

        builder.Property(q => q.SortOrder)
            .HasDefaultValue(0);

        builder.Property(q => q.RecallCount)
            .HasDefaultValue(0);

        builder.Property(q => q.RoomName)
            .HasMaxLength(50);

        builder.Property(q => q.Notes)
            .HasMaxLength(500);

        builder.Property(q => q.QueueDate)
            .IsRequired();

        // Index for querying today's queue efficiently
        builder.HasIndex(q => new { q.QueueDate, q.Status });

        // Index for priority-based ordering
        builder.HasIndex(q => new { q.QueueDate, q.Priority, q.SortOrder });

        // Prevent duplicate active queue items for the same patient on the same day
        builder.HasIndex(q => new { q.PatientId, q.QueueDate })
            .HasFilter("\"Status\" NOT IN ('Completed', 'Cancelled', 'NoShow')")
            .IsUnique();

        // DB-01 FIX: Index for querying queue items by doctor
        builder.HasIndex(q => q.DoctorId);

        builder.HasOne(q => q.Patient)
            .WithMany()
            .HasForeignKey(q => q.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Appointment)
            .WithMany()
            .HasForeignKey(q => q.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.Visit)
            .WithMany()
            .HasForeignKey(q => q.VisitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.Doctor)
            .WithMany()
            .HasForeignKey(q => q.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.AddedByUser)
            .WithMany()
            .HasForeignKey(q => q.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.CalledByUser)
            .WithMany()
            .HasForeignKey(q => q.CalledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.Service)
            .WithMany()
            .HasForeignKey(q => q.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.ClinicRoom)
            .WithMany()
            .HasForeignKey(q => q.ClinicRoomId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
