using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// DB-01 FIX: Fluent API configuration for Visit entity indexes.
/// </summary>
public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        // DB-01 FIX: Index for querying visits by doctor
        builder.HasIndex(v => v.DoctorId);
    }
}
