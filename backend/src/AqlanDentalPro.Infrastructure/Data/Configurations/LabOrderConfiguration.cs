using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// DB-01 FIX: Fluent API configuration for LabOrder entity indexes.
/// CON-02 FIX: Added unique index on OrderNumber to prevent duplicate order numbers
/// under concurrent requests (belt-and-suspenders alongside advisory lock).
/// Sprint 2: Added index on BranchId, VisitId for multi-branch and visit linkage.
/// </summary>
public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        // DB-01 FIX: Index for querying lab orders by doctor
        builder.HasIndex(l => l.DoctorId);

        // DB-01 FIX: Index for querying lab orders by ortho case
        builder.HasIndex(l => l.OrthoCaseId);

        // CON-02 FIX: Unique index on OrderNumber prevents duplicate lab order numbers
        // at the database level. This is a safety net in addition to the advisory lock
        // used during order creation. Filter: only non-null OrderNumbers (null = legacy data).
        builder.HasIndex(l => l.OrderNumber)
            .IsUnique()
            .HasFilter("\"OrderNumber\" IS NOT NULL");

        // Sprint 2: Index for querying lab orders by branch (multi-branch support)
        builder.HasIndex(l => l.BranchId);

        // Sprint 2: Index for querying lab orders linked to a visit
        builder.HasIndex(l => l.VisitId);

        // Sprint 2: Navigation property — Visit
        builder.HasOne(l => l.Visit)
            .WithMany()
            .HasForeignKey(l => l.VisitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
