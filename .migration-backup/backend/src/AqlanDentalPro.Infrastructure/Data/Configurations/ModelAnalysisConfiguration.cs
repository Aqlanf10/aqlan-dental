using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class ModelAnalysisConfiguration : IEntityTypeConfiguration<ModelAnalysis>
{
    public void Configure(EntityTypeBuilder<ModelAnalysis> builder)
    {
        builder.Property(x => x.DentitionStage)
            .HasMaxLength(20)
            .HasDefaultValue("Permanent");

        builder.Property(x => x.AnalysisVersion)
            .HasMaxLength(10)
            .HasDefaultValue("2.0");

        builder.Property(x => x.InputDataJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.ResultDataJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.HasIndex(x => new { x.OrthoCaseId, x.AnalysisDate });
    }
}
