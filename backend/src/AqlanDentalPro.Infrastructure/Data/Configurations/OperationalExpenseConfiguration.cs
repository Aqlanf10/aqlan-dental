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

        builder.HasIndex(e => e.BranchId);
        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.ApprovalStatus);
    }
}
