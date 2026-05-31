using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class CashierSessionConfiguration : IEntityTypeConfiguration<CashierSession>
{
    public void Configure(EntityTypeBuilder<CashierSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable("CashierSessions");

        // Decimal precision
        builder.Property(s => s.OpeningBalance).HasPrecision(12, 2);
        builder.Property(s => s.ExpectedClosingCash).HasPrecision(12, 2);
        builder.Property(s => s.ExpectedClosingCard).HasPrecision(12, 2);
        builder.Property(s => s.ExpectedClosingBank).HasPrecision(12, 2);
        builder.Property(s => s.ActualClosingCash).HasPrecision(12, 2);
        builder.Property(s => s.ActualClosingCard).HasPrecision(12, 2);
        builder.Property(s => s.ActualClosingBank).HasPrecision(12, 2);
        builder.Property(s => s.ShortageOrSurplus).HasPrecision(12, 2);

        // Composite index for active session lookup:
        // WHERE CashierId = @id AND Status = Open AND IsActive
        builder.HasIndex(s => new { s.CashierId, s.Status });
        builder.HasIndex(s => s.BranchId);
        builder.HasIndex(s => s.TreasuryId);

        // Treasury FK
        builder.HasOne(s => s.Treasury)
            .WithMany()
            .HasForeignKey(s => s.TreasuryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
