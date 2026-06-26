using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// EF configuration for TreatmentPackage. Mirrors the table created by migration
/// 20260712000000_AddAppointmentEnhancements. All columns are nullable/optional
/// except Name (required) and the BaseEntity audit columns.
/// </summary>
public class TreatmentPackageConfiguration : IEntityTypeConfiguration<TreatmentPackage>
{
    public void Configure(EntityTypeBuilder<TreatmentPackage> builder)
    {
        builder.ToTable("TreatmentPackages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        // TotalPrice — store as numeric(12,2) consistent with ClinicService.DefaultPrice / Payment.Amount.
        builder.Property(p => p.TotalPrice)
            .HasPrecision(12, 2);

        builder.Property(p => p.SessionCount)
            .HasDefaultValue(1);

        // Color is a hex string like "#3b82f6" — keep it short.
        builder.Property(p => p.Color)
            .HasMaxLength(20);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        // Filtered unique index on Name for active packages — prevents duplicate
        // active catalog entries while still allowing soft-deleted names to be reused.
        builder.HasIndex(p => new { p.Name, p.IsActive })
            .HasDatabaseName("IX_TreatmentPackages_Name_IsActive");
    }
}
