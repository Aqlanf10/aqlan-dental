using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class CashierSessionCurrencyOpeningBalanceConfiguration
    : IEntityTypeConfiguration<CashierSessionCurrencyOpeningBalance>
{
    public void Configure(EntityTypeBuilder<CashierSessionCurrencyOpeningBalance> builder)
    {
        builder.ToTable("CashierSessionCurrencyOpeningBalances");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Currency)
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(item => item.OpeningCash).HasPrecision(18, 2);

        builder.HasIndex(item => new { item.CashierSessionId, item.Currency })
            .IsUnique();

        builder.HasOne(item => item.CashierSession)
            .WithMany(session => session.CurrencyOpeningBalances)
            .HasForeignKey(item => item.CashierSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
