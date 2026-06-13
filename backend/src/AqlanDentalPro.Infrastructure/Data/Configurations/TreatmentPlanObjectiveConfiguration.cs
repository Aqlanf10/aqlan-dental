using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreatmentPlanObjectiveConfiguration : IEntityTypeConfiguration<TreatmentPlanObjective>
{
    public void Configure(EntityTypeBuilder<TreatmentPlanObjective> builder)
    {
        builder.Property(o => o.Category).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(o => new { o.TreatmentPlanId, o.SortOrder });

        builder.HasOne(o => o.TreatmentPlan)
            .WithMany(p => p.Objectives)
            .HasForeignKey(o => o.TreatmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
