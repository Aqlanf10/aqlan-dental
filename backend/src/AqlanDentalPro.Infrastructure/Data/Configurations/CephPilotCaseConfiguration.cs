using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotCaseConfiguration : IEntityTypeConfiguration<CephPilotCase>
{
    public void Configure(EntityTypeBuilder<CephPilotCase> builder)
    {
        builder.ToTable("CephPilotCases");
        builder.Property(item => item.CaseCode).HasMaxLength(30).IsRequired();
        builder.Property(item => item.SourceType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(item => item.SourceReference).HasMaxLength(80);
        builder.Property(item => item.ImageStorageKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ImageSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.MmPerPixel).HasPrecision(12, 8);
        builder.Property(item => item.CalibrationSource).HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.Orientation).HasMaxLength(40).IsRequired();
        builder.Property(item => item.SiteCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.DeviceCode).HasMaxLength(40).IsRequired();
        builder.Property(item => item.PatientGroupToken).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.Split).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.QualityCategory).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.SkeletalCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.VerticalCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.MigrationDecision).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => new { item.ProjectId, item.CaseSequence }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.CaseCode }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.ImageSha256 }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.IsActive });
        builder.HasOne(item => item.Project)
            .WithMany(item => item.Cases)
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
