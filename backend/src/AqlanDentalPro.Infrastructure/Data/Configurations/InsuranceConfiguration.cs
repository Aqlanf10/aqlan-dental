using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// FluentAPI configuration for InsuranceCompany and InsuranceClaim entities.
/// شركات التأمين والمطالبات التأمينية.
/// </summary>
public class InsuranceCompanyConfiguration : IEntityTypeConfiguration<InsuranceCompany>
{
    public void Configure(EntityTypeBuilder<InsuranceCompany> builder)
    {
        builder.HasKey(ic => ic.Id);

        builder.ToTable("InsuranceCompanies");

        // String properties
        builder.Property(ic => ic.Name).HasMaxLength(200).IsRequired();
        builder.Property(ic => ic.ContactEmail).HasMaxLength(200).IsRequired();
        builder.Property(ic => ic.Phone).HasMaxLength(30).IsRequired();

        // Decimal precision
        builder.Property(ic => ic.DefaultCoveragePercentage).HasPrecision(5, 4);

        // Indexes
        builder.HasIndex(ic => ic.Name);
        builder.HasIndex(ic => ic.IsActive);

        // Relationships
        builder.HasMany(ic => ic.Claims)
            .WithOne(c => c.InsuranceCompany)
            .HasForeignKey(c => c.InsuranceCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InsuranceClaimConfiguration : IEntityTypeConfiguration<InsuranceClaim>
{
    public void Configure(EntityTypeBuilder<InsuranceClaim> builder)
    {
        builder.HasKey(ic => ic.Id);

        builder.ToTable("InsuranceClaims");

        // Enum conversion — stored as string for readability
        builder.Property(ic => ic.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Decimal precision
        builder.Property(ic => ic.TotalAmount).HasPrecision(12, 2);
        builder.Property(ic => ic.CoveredAmount).HasPrecision(12, 2);
        builder.Property(ic => ic.PatientCoPay).HasPrecision(12, 2);

        // String properties
        builder.Property(ic => ic.RejectionReason).HasMaxLength(500).IsRequired(false);

        // Indexes
        builder.HasIndex(ic => ic.InvoiceId);
        builder.HasIndex(ic => ic.InsuranceCompanyId);
        builder.HasIndex(ic => ic.PatientId);
        builder.HasIndex(ic => ic.Status);

        // Relationships
        builder.HasOne(ic => ic.Invoice)
            .WithMany()
            .HasForeignKey(ic => ic.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ic => ic.Patient)
            .WithMany()
            .HasForeignKey(ic => ic.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
