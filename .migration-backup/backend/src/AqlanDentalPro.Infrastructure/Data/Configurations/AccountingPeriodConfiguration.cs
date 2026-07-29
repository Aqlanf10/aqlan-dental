using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("AccountingPeriods");
        builder.Property(p => p.Name).IsRequired().HasMaxLength(120);
        builder.Property(p => p.Status).IsRequired().HasMaxLength(16).HasDefaultValue("Open");
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.HasIndex(p => new { p.BranchId, p.StartDate, p.EndDate });
        builder.HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ClosedByUser).WithMany().HasForeignKey(p => p.ClosedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
