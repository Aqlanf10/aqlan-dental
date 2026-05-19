using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// H7 FIX: FluentAPI configuration for Employee entity.
/// Previously, Employee had zero configuration — no UserId FK relationship,
/// no index on BranchId, no BaseSalary decimal precision.
/// </summary>
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        // Decimal precision for salary
        builder.Property(e => e.BaseSalary).HasPrecision(12, 2);

        // String constraints
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.NationalId).HasMaxLength(50);
        builder.Property(e => e.Position).HasMaxLength(100);
        builder.Property(e => e.EmergencyContact).HasMaxLength(200);
        builder.Property(e => e.EmergencyPhone).HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        // Indexes
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.BranchId);

        // FK to User
        builder.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Branch
        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
