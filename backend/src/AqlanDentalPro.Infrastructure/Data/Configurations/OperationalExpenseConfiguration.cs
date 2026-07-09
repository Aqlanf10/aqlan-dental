using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class OperationalExpenseConfiguration : IEntityTypeConfiguration<OperationalExpense>
{
    public void Configure(EntityTypeBuilder<OperationalExpense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable("OperationalExpenses");

        builder.Property(e => e.Amount).HasPrecision(12, 2);
        builder.Property(e => e.ExpenseNumber).HasMaxLength(50);
        builder.Property(e => e.Title).HasMaxLength(300);
        builder.Property(e => e.PaymentMethod).HasMaxLength(50);

        // SEQ-06: These enum columns are stored as varchar in the production DB
        // (matching the convention of every other enum in the system — see
        // InvoiceConfiguration, OrthoCaseConfiguration, etc.). Without
        // HasConversion<string>, EF Core tries to read them as int, causing:
        //   InvalidCastException: Reading as 'System.Int32' is not supported
        //   for fields having DataTypeName 'character varying'
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(e => e.BranchId);
        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.ApprovalStatus);
    }
}
