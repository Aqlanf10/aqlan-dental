using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LegacyFinancialArchiveEntryConfiguration : IEntityTypeConfiguration<LegacyFinancialArchiveEntry>
{
    public void Configure(EntityTypeBuilder<LegacyFinancialArchiveEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceEntryId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LegacyFileNumber).HasMaxLength(50);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.SourceDocumentId).HasMaxLength(100);
        builder.Property(x => x.ReconciliationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DebitAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreditAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.SourceSystem, x.SourceEntryId }).IsUnique();
        builder.HasIndex(x => x.PatientId);

        builder.HasOne(x => x.Patient)
            .WithMany(p => p.LegacyFinancialEntries)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
