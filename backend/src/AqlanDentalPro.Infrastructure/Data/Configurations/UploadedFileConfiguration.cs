using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class UploadedFileConfiguration : IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("UploadedFiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(100);

        builder.HasIndex(x => x.FileName).IsUnique();
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
    }
}
