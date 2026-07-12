using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class RadiologyOrderConfiguration : IEntityTypeConfiguration<RadiologyOrder>
{
    public void Configure(EntityTypeBuilder<RadiologyOrder> builder)
    {
        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.DoctorId);

        builder.Property(o => o.StudyType).IsRequired().HasMaxLength(30);
        builder.Property(o => o.Region).HasMaxLength(200);
        builder.Property(o => o.ClinicalNotes).HasMaxLength(1000);

        builder.HasOne(o => o.Patient)
            .WithMany()
            .HasForeignKey(o => o.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Doctor)
            .WithMany()
            .HasForeignKey(o => o.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Visit)
            .WithMany()
            .HasForeignKey(o => o.VisitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
