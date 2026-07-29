using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephAiModelVersionConfiguration : IEntityTypeConfiguration<CephAiModelVersion>
{
    public void Configure(EntityTypeBuilder<CephAiModelVersion> builder)
    {
        builder.ToTable("CephAiModelVersions");
        builder.Property(item => item.RegistryKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Provider).HasMaxLength(50).IsRequired();
        builder.Property(item => item.ModelId).HasMaxLength(150).IsRequired();
        builder.Property(item => item.ModelVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PreprocessingVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.DatasetVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.LandmarkDefinitionVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IdentitySha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.ArtifactSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ValidationEvidenceId).HasMaxLength(150);
        builder.Property(item => item.StatisticsApprovalId).HasMaxLength(150);
        builder.Property(item => item.ClinicalApprovalId).HasMaxLength(150);
        builder.HasIndex(item => item.RegistryKey).IsUnique();
        builder.HasIndex(item => item.IdentitySha256).IsUnique();
        builder.HasIndex(item => new { item.Status, item.Provider, item.ModelId });
    }
}
