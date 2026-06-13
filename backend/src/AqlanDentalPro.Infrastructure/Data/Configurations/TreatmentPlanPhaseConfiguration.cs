using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreatmentPlanPhaseConfiguration : IEntityTypeConfiguration<TreatmentPlanPhase>
{
    public void Configure(EntityTypeBuilder<TreatmentPlanPhase> builder)
    {
        builder.Property(p => p.PhaseName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.ObjectiveSummary).HasMaxLength(1000);
        builder.Property(p => p.PlannedAppliance).HasMaxLength(500);
        builder.Property(p => p.Mechanics).HasMaxLength(2000);
        builder.Property(p => p.Status).HasMaxLength(30).HasDefaultValue("Planned");
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.HasIndex(p => new { p.TreatmentPlanId, p.SequenceNumber });

        builder.HasOne(p => p.TreatmentPlan)
            .WithMany(t => t.Phases)
            .HasForeignKey(p => p.TreatmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
