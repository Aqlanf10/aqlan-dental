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

        // Patient Journey fields (Sprint: Command Center)
        builder.Property(a => a.ServiceId).IsRequired(false);
        builder.Property(a => a.ClinicRoomId).IsRequired(false);
        builder.Property(a => a.OrthoCaseId).IsRequired(false);

        // YOLO-S1: Companion/Guardian + Color + Treatment Package
        builder.Property(a => a.CompanionName).HasMaxLength(150);
        builder.Property(a => a.CompanionPhone).HasMaxLength(30);
        builder.Property(a => a.CompanionRelationship).HasMaxLength(50);
        builder.Property(a => a.AppointmentColor).HasMaxLength(20);
        builder.Property(a => a.PackageId).IsRequired(false);

        // Composite index for conflict detection
        builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime });

        // Index for queue queries
        builder.HasIndex(a => a.AppointmentDate);
        builder.HasIndex(a => a.OrthoCaseId);
        builder.HasIndex(a => a.PackageId);

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

        builder.HasOne(a => a.Service)
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.ClinicRoom)
            .WithMany()
            .HasForeignKey(a => a.ClinicRoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.OrthoCase)
            .WithMany()
            .HasForeignKey(a => a.OrthoCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        // YOLO-S1: optional link to TreatmentPackage — SetNull so deleting a
        // package (soft-delete via IsActive=false) does not break historical
        // appointments; the read path falls back to the appointment's own type.
        builder.HasOne(a => a.Package)
            .WithMany()
            .HasForeignKey(a => a.PackageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
