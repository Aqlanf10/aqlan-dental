using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreasuryConfiguration : IEntityTypeConfiguration<Treasury>
{
    public void Configure(EntityTypeBuilder<Treasury> entity)
    {
        // FIN-06 FIX: Enable optimistic concurrency on Treasury using PostgreSQL's xmin system
        // column. Previously the comment here claimed "we rely on application-level checks
        // (comparing Balance before/after update)" — but NO such before/after comparison existed
        // anywhere (grep-confirmed). The Treasury entity comment (Treasury.cs:25-26) also claimed
        // UseXminAsConcurrencyToken was configured here — it was not. The result: two concurrent
        // payments to the same treasury both loaded Balance=1000, both added their delta, both
        // saved — the second save overwrote the first (lost update), silently corrupting the
        // treasury balance.
        //
        // With UseXminAsConcurrencyToken, EF Core includes the xmin value in the UPDATE WHERE
        // clause. If the row changed since it was loaded, PostgreSQL updates 0 rows, EF throws
        // DbUpdateConcurrencyException — which FinanceService.UpdateTreasuryBalanceAsync already
        // catches (FinanceService.cs:1503) and retries. This is the standard PostgreSQL optimistic
        // concurrency pattern.
        //
        // Note: xmin is a PostgreSQL system column that exists on every table automatically —
        // no DDL migration is needed. Only the EF model + ModelSnapshot change.
        entity.UseXminAsConcurrencyToken();

        // Performance indexes for treasury lookups
        entity.HasIndex(t => t.BranchId);
        entity.HasIndex(t => new { t.BranchId, t.Type });

        // Unique constraint: only one active treasury per branch/type/name combination.
        // Filtered index (PostgreSQL partial index) ensures soft-deleted (IsActive=false)
        // treasuries don't conflict, allowing the same branch/type/name to be reused after deletion.
        entity.HasIndex(t => new { t.BranchId, t.Type, t.Name })
            .HasFilter("\"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_Treasuries_BranchId_Type_Name_Unique");
    }
}
