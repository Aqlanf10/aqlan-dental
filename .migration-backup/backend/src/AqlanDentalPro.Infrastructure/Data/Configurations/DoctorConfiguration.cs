using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Specialty).HasMaxLength(100);
        builder.Property(d => d.LicenseNumber).HasMaxLength(100);
        builder.Property(d => d.Color).HasMaxLength(20);
        builder.Property(d => d.AvatarInitials).HasMaxLength(5);

        // Future compensation compatibility (Sprint 6)
        builder.Property(d => d.CompensationType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(CompensationType.None);
        builder.Property(d => d.DefaultCommissionPercentage).HasPrecision(5, 2);
        builder.Property(d => d.CompensationNotes).HasMaxLength(500);

        builder.HasOne(d => d.User)
            .WithOne(u => u.Doctor)
            .HasForeignKey<Doctor>(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Branch)
            .WithMany(b => b.Doctors)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        // Standing room assignment ("تعيينات غرف الأطباء"). No inverse collection on
        // ClinicRoom — a room doesn't need to enumerate its doctors. Deleting a room
        // simply clears the assignment (SetNull), never blocks nor cascades.
        builder.HasOne(d => d.DefaultClinicRoom)
            .WithMany()
            .HasForeignKey(d => d.DefaultClinicRoomId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
