using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PatientNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.PatientNumber).IsUnique();
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.MiddleName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.WhatsApp).HasMaxLength(20);
        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(10);

        // Full-text search index on patient name + number
        builder.HasIndex(p => new { p.BranchId, p.IsActive });

        builder.HasOne(p => p.PrimaryDoctor)
            .WithMany(d => d.PrimaryPatients)
            .HasForeignKey(p => p.PrimaryDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Branch)
            .WithMany(b => b.Patients)
            .HasForeignKey(p => p.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.MedicalHistory)
            .WithOne(m => m.Patient)
            .HasForeignKey<MedicalHistory>(m => m.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.DentalHistory)
            .WithOne(d => d.Patient)
            .HasForeignKey<DentalHistory>(d => d.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
