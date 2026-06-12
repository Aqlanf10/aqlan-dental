using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        // Optional link to an orthodontic case (standardized records — Phase 2).
        // SetNull: deleting an ortho case must never delete patient documents.
        builder.HasIndex(d => d.OrthoCaseId);

        builder.HasOne(d => d.OrthoCase)
            .WithMany()
            .HasForeignKey(d => d.OrthoCaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
