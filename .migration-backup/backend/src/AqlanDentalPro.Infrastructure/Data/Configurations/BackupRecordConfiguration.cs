using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.Property(b => b.FilePath).HasMaxLength(500);
        builder.Property(b => b.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(b => b.StartedAt);
    }
}
