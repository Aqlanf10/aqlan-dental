using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class RadiographConfiguration : IEntityTypeConfiguration<Radiograph>
{
    public void Configure(EntityTypeBuilder<Radiograph> builder)
    {
        // Optional link to an orthodontic case (standardized records — Phase 2).
        // SetNull: deleting an ortho case must never delete patient radiographs.
        builder.HasIndex(r => r.OrthoCaseId);

        builder.HasOne(r => r.OrthoCase)
            .WithMany()
            .HasForeignKey(r => r.OrthoCaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
