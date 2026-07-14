using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephAiInferenceRunConfiguration : IEntityTypeConfiguration<CephAiInferenceRun>
{
    public void Configure(EntityTypeBuilder<CephAiInferenceRun> builder)
    {
        builder.ToTable("CephAiInferenceRuns");
        builder.Property(item => item.Operation).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Precision).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ImageSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.FailureCode).HasMaxLength(100);
        builder.Property(item => item.OriginalPredictionsJson).HasColumnType("jsonb");
        builder.Property(item => item.OutputSha256).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(item => new { item.AnalysisId, item.StartedAt });
        builder.HasIndex(item => new { item.ModelVersionId, item.Status, item.StartedAt });
        builder.HasOne(item => item.Analysis)
            .WithMany(item => item.AiInferenceRuns)
            .HasForeignKey(item => item.AnalysisId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ModelVersion)
            .WithMany(item => item.InferenceRuns)
            .HasForeignKey(item => item.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
