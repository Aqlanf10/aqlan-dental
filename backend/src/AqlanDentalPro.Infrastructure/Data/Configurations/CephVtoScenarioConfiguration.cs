using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephVtoScenarioConfiguration : IEntityTypeConfiguration<CephVtoScenario>
{
    public void Configure(EntityTypeBuilder<CephVtoScenario> builder)
    {
        builder.ToTable("CephVtoScenarios");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.UpperIncisorMoveMm).HasPrecision(6, 2);
        builder.Property(x => x.LowerIncisorMoveMm).HasPrecision(6, 2);
        builder.Property(x => x.OverjetBeforeMm).HasPrecision(6, 2);
        builder.Property(x => x.OverjetAfterMm).HasPrecision(6, 2);
        builder.HasIndex(x => x.CephAnalysisId);
        builder.HasIndex(x => new { x.CephAnalysisId, x.ScenarioGroupId, x.VersionNumber }).IsUnique();
        builder.HasOne(x => x.Analysis)
            .WithMany()
            .HasForeignKey(x => x.CephAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
