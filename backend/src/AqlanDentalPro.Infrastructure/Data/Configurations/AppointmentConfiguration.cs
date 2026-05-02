using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AppointmentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Specialty).HasConversion<string>().HasMaxLength(50);

        // Queue / clinic-flow fields (Sprint 4.5)
        builder.Property(a => a.RoomName).HasMaxLength(50);
        builder.Property(a => a.ArrivedAt).IsRequired(false);
        builder.Property(a => a.CalledAt).IsRequired(false);
        builder.Property(a => a.InRoomAt).IsRequired(false);

        // Composite index for conflict detection
        builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime });

        // Index for queue queries
        builder.HasIndex(a => a.AppointmentDate);

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Doctor)
            .WithMany(d => d.Appointments)
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Branch)
            .WithMany(b => b.Appointments)
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
