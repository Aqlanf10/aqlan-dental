using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// FluentAPI configuration for InstallmentPlan and Installment entities.
/// خطط التقسيط والأقساط الشهرية.
/// </summary>
public class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> builder)
    {
        builder.HasKey(ip => ip.Id);

        builder.ToTable("InstallmentPlans");

        // Decimal precision
        builder.Property(ip => ip.TotalAmount).HasPrecision(12, 2);
        builder.Property(ip => ip.DownPayment).HasPrecision(12, 2);
        builder.Property(ip => ip.MonthlyAmount).HasPrecision(12, 2);

        // Indexes
        builder.HasIndex(ip => ip.ContractId);
        builder.HasIndex(ip => ip.PatientId);
        builder.HasIndex(ip => ip.IsCompleted);

        // Relationships
        builder.HasOne(ip => ip.Contract)
            .WithMany()
            .HasForeignKey(ip => ip.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ip => ip.Patient)
            .WithMany()
            .HasForeignKey(ip => ip.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(ip => ip.Installments)
            .WithOne(i => i.InstallmentPlan)
            .HasForeignKey(i => i.InstallmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.HasKey(i => i.Id);

        builder.ToTable("Installments");

        // Enum conversion — stored as string for readability
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Decimal precision
        builder.Property(i => i.Amount).HasPrecision(12, 2);

        // Nullable FK
        builder.Property(i => i.PaymentId).IsRequired(false);

        // Indexes
        builder.HasIndex(i => i.InstallmentPlanId);
        builder.HasIndex(i => i.DueDate);
        builder.HasIndex(i => i.Status);

        // Relationships
        builder.HasOne(i => i.Payment)
            .WithMany()
            .HasForeignKey(i => i.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
