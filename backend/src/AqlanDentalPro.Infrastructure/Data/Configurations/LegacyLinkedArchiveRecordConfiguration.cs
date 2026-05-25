using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LegacyLinkedArchiveRecordConfiguration : IEntityTypeConfiguration<LegacyLinkedArchiveRecord>
{
    public void Configure(EntityTypeBuilder<LegacyLinkedArchiveRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceTable).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceRecordId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Classification).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LegacyFileNumber).HasMaxLength(50);
        builder.Property(x => x.NumberValue01).HasPrecision(18, 2);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.SourceSystem, x.SourceTable, x.SourceRecordId }).IsUnique();
        builder.HasIndex(x => x.PatientId);
        builder.HasOne(x => x.Patient)
            .WithMany(x => x.LegacyLinkedRecords)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
