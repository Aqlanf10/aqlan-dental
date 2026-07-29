using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class SalaryRecordConfiguration : IEntityTypeConfiguration<SalaryRecord>
{
    public void Configure(EntityTypeBuilder<SalaryRecord> builder)
    {
        builder.Property(s => s.BaseSalary).HasPrecision(12, 2);
        builder.Property(s => s.Deductions).HasPrecision(12, 2);
        builder.Property(s => s.Advances).HasPrecision(12, 2);
        builder.Property(s => s.Bonuses).HasPrecision(12, 2);
        builder.Property(s => s.NetSalary).HasPrecision(12, 2);
        builder.Property(s => s.PaymentMethod).HasMaxLength(50);
        builder.Property(s => s.Notes).HasMaxLength(500);

        // Unique: one salary record per employee per month/year
        builder.HasIndex(s => new { s.EmployeeId, s.Year, s.Month }).IsUnique();

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
