using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class OrthodonticAiLogConfiguration : IEntityTypeConfiguration<OrthodonticAiLog>
{
    public void Configure(EntityTypeBuilder<OrthodonticAiLog> builder)
    {
        builder.ToTable("OrthodonticAiLogs");

        builder.Property(l => l.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.ModelId)
            .HasMaxLength(100);

        builder.Property(l => l.ErrorSummary)
            .HasMaxLength(300);

        builder.Property(l => l.InputSummary)
            .HasMaxLength(300);

        builder.HasIndex(l => l.AnalysisId);
    }
}
