using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotExportArtifactConfiguration : IEntityTypeConfiguration<CephPilotExportArtifact>
{
    public void Configure(EntityTypeBuilder<CephPilotExportArtifact> builder)
    {
        builder.ToTable("CephPilotExportArtifacts");
        builder.Property(item => item.ArtifactType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.StorageKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ImportStatus).HasMaxLength(30).IsRequired();
        builder.HasIndex(item => new { item.CaseId, item.ArtifactType, item.Sha256 }).IsUnique();
        builder.HasIndex(item => new { item.CaseId, item.ArtifactType, item.IsActive });
        builder.HasOne(item => item.Case)
            .WithMany(item => item.ExportArtifacts)
            .HasForeignKey(item => item.CaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
