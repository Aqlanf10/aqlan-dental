using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class CashierSessionCurrencyReconciliationConfiguration
    : IEntityTypeConfiguration<CashierSessionCurrencyReconciliation>
{
    public void Configure(EntityTypeBuilder<CashierSessionCurrencyReconciliation> builder)
    {
        builder.ToTable("CashierSessionCurrencyReconciliations");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.ExpectedCash).HasPrecision(18, 2);
        builder.Property(item => item.ActualCash).HasPrecision(18, 2);
        builder.Property(item => item.ExpectedBank).HasPrecision(18, 2);
        builder.Property(item => item.ActualBank).HasPrecision(18, 2);

        builder.HasIndex(item => new { item.CashierSessionId, item.Currency })
            .IsUnique();

        builder.HasOne(item => item.CashierSession)
            .WithMany(session => session.CurrencyReconciliations)
            .HasForeignKey(item => item.CashierSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
