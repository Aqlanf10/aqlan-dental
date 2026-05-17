using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// DB-01 FIX: Fluent API configuration for LabOrder entity indexes.
/// </summary>
public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        // DB-01 FIX: Index for querying lab orders by doctor
        builder.HasIndex(l => l.DoctorId);

        // DB-01 FIX: Index for querying lab orders by ortho case
        builder.HasIndex(l => l.OrthoCaseId);
    }
}
