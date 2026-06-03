using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Lab Sprint 2 — Fluent API configuration for LabWorkType entity.
/// </summary>
public class LabWorkTypeConfiguration : IEntityTypeConfiguration<LabWorkType>
{
    public void Configure(EntityTypeBuilder<LabWorkType> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.NameAr).HasMaxLength(100);
        builder.Property(w => w.Category).HasMaxLength(50);

        builder.HasIndex(w => w.Name);
        builder.HasIndex(w => w.SortOrder);
    }
}
