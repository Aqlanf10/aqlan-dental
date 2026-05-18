using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// DB-01 FIX: Fluent API configuration for Payment entity indexes.
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // DB-01 FIX: Index for querying payments by doctor
        builder.HasIndex(p => p.DoctorId);

        // DB-01 FIX: Index for querying payments by branch
        builder.HasIndex(p => p.BranchId);
    }
}
