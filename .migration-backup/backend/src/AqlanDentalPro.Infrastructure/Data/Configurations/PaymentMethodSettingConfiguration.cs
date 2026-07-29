using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Sprint 2 — EF Core configuration for PaymentMethodSetting entity.
/// Unique index on Code, index on BranchId, global query filter for IsActive.
/// </summary>
public class PaymentMethodSettingConfiguration : IEntityTypeConfiguration<PaymentMethodSetting>
{
    public void Configure(EntityTypeBuilder<PaymentMethodSetting> builder)
    {
        builder.ToTable("PaymentMethodSettings");

        // Unique index on Code to prevent duplicate payment method codes
        builder.HasIndex(p => p.Code)
            .IsUnique();

        // Index on BranchId for multi-branch queries
        builder.HasIndex(p => p.BranchId);

        // Global query filter: only return active payment methods by default
        builder.HasQueryFilter(p => p.IsActive);

        // Property constraints
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.AccountName)
            .HasMaxLength(200);

        builder.Property(p => p.AccountNumber)
            .HasMaxLength(100);

        builder.Property(p => p.Notes)
            .HasMaxLength(500);
    }
}
