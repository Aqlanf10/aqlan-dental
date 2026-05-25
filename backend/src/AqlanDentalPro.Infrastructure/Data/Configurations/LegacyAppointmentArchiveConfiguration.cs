using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LegacyAppointmentArchiveConfiguration : IEntityTypeConfiguration<LegacyAppointmentArchive>
{
    public void Configure(EntityTypeBuilder<LegacyAppointmentArchive> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceAppointmentId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LegacyFileNumber).HasMaxLength(50);
        builder.Property(x => x.ArchiveType).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.SourceSystem, x.SourceAppointmentId }).IsUnique();
        builder.HasIndex(x => x.PatientId);
        builder.HasOne(x => x.Patient)
            .WithMany(x => x.LegacyAppointments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
