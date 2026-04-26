using AqlanDentalPro.Domain.Entities;
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

        builder.HasOne(d => d.User)
            .WithOne(u => u.Doctor)
            .HasForeignKey<Doctor>(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Branch)
            .WithMany(b => b.Doctors)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
