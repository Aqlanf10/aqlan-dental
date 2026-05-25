using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class CashFlowTransactionConfiguration : IEntityTypeConfiguration<CashFlowTransaction>
{
    public void Configure(EntityTypeBuilder<CashFlowTransaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ToTable("CashFlowTransactions");

        // Decimal precision for Amount
        builder.Property(t => t.Amount).HasPrecision(12, 2);

        // String constraints
        builder.Property(t => t.TransactionNumber).HasMaxLength(100);
        builder.Property(t => t.PaymentMethod).HasMaxLength(50);
        builder.Property(t => t.ReferenceNumber).HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);

        // Performance indexes — these columns are used in frequent queries:
        // - Session close: WHERE CashierSessionId = @id AND IsActive
        // - Reports: WHERE BranchId = @id AND TransactionDate BETWEEN @from AND @to
        // - Category filtering: WHERE Category = @cat AND IsActive
        builder.HasIndex(t => t.CashierSessionId);
        builder.HasIndex(t => t.BranchId);
        builder.HasIndex(t => t.TransactionDate);
        builder.HasIndex(t => t.Category);
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => t.IsActive);
    }
}
