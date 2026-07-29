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
        builder.Property(t => t.Currency).HasMaxLength(3).HasDefaultValue("YER");

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
        builder.HasIndex(t => new { t.BranchId, t.Currency, t.TransactionDate });

        // Finance V3: Treasury FK and index
        builder.HasIndex(t => t.TreasuryId);

        // Finance V3: Treasury navigation
        builder.HasOne(t => t.Treasury)
            .WithMany()
            .HasForeignKey(t => t.TreasuryId)
            .OnDelete(DeleteBehavior.SetNull);

        // C3: Reversal tracking navigation properties
        builder.HasOne(t => t.ReversalOfTransaction)
            .WithMany()
            .HasForeignKey(t => t.ReversalOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ReversedByTransaction)
            .WithMany()
            .HasForeignKey(t => t.ReversedByTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
