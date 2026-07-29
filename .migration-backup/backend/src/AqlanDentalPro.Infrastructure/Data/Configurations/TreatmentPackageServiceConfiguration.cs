using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// YOLO-S2: EF configuration for TreatmentPackageService (package ↔ service link).
/// Quantity defaults to 1; OverridePrice is nullable. Cascade-Restrict on both FKs
/// so deleting a package or a service must be explicit (a service in use by a
/// package can't be hard-deleted silently). Composite unique index prevents the
/// same service from being added twice to the same package.
/// </summary>
public class TreatmentPackageServiceConfiguration : IEntityTypeConfiguration<TreatmentPackageService>
{
    public void Configure(EntityTypeBuilder<TreatmentPackageService> builder)
    {
        builder.ToTable("TreatmentPackageServices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasDefaultValue(1);

        builder.Property(x => x.OverridePrice)
            .HasPrecision(12, 2);

        builder.HasOne(x => x.Package)
            .WithMany(p => p.Services)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Service)
            .WithMany(s => s.PackageLinks)
            .HasForeignKey(x => x.ClinicServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate (package, service) pairs — quantity should be bumped, not duplicated.
        builder.HasIndex(x => new { x.PackageId, x.ClinicServiceId })
            .IsUnique()
            .HasDatabaseName("IX_TreatmentPackageServices_PackageId_ClinicServiceId");

        // Lookup indexes for the FK columns (also speeds up the service→packages reverse query).
        builder.HasIndex(x => x.PackageId);
        builder.HasIndex(x => x.ClinicServiceId);
    }
}
