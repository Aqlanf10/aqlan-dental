using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LegacyTreatmentArchiveConfiguration : IEntityTypeConfiguration<LegacyTreatmentArchive>
{
    public void Configure(EntityTypeBuilder<LegacyTreatmentArchive> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceLineId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceDocumentId).HasMaxLength(100);
        builder.Property(x => x.LegacyFileNumber).HasMaxLength(50);
        builder.Property(x => x.DocumentType).HasMaxLength(100);
        builder.Property(x => x.ServiceName).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.DoctorName).HasMaxLength(200);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.SourceSystem, x.SourceLineId }).IsUnique();
        builder.HasIndex(x => x.PatientId);

        builder.HasOne(x => x.Patient)
            .WithMany(p => p.LegacyTreatments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
