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

        // DB-03: Partial unique index — only one Open session per cashier at the DB level.
        // The application-level guard (pg_advisory_xact_lock in OpenSession) is the primary defense,
        // but this index is the safety net: if the DB is accessed from outside the app (manual SQL,
        // a second app instance, or InMemory where advisory locks are no-ops), two open sessions
        // cannot coexist. The filter ensures soft-deleted (IsActive=false) sessions don't conflict.
        builder.HasIndex(s => s.CashierId)
            .IsUnique()
            .HasFilter("\"Status\" = 0 AND \"IsActive\" = true")
            .HasDatabaseName("IX_CashierSessions_OneOpenPerCashier");

        // Treasury FK
        builder.HasOne(s => s.Treasury)
            .WithMany()
            .HasForeignKey(s => s.TreasuryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
