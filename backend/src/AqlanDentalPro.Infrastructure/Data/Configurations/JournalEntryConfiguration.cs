using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable("JournalEntries");

        // String constraints
        builder.Property(e => e.EntryNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("YER");
        builder.Property(e => e.ExchangeRateToYer).HasPrecision(18, 6).HasDefaultValue(1m);

        // Decimal precision for debit/credit totals (not stored, but consistent)
        builder.Property(e => e.FinancialDocumentType).HasConversion<string>().HasMaxLength(30);

        // Performance indexes
        builder.HasIndex(e => e.EntryNumber).IsUnique();
        builder.HasIndex(e => e.BranchId);
        builder.HasIndex(e => e.EntryDate);
        builder.HasIndex(e => e.FinancialDocumentType);
        builder.HasIndex(e => e.IsPosted);
        builder.HasIndex(e => e.IsReversal);
        builder.HasIndex(e => e.CashierSessionId);
        builder.HasIndex(e => e.TreasuryId);
        builder.HasIndex(e => e.PerformedBy);

        // One canonical journal entry per source document. Reversal entries are
        // deliberately excluded because they point back to the same source.
        // Year-end closing creates one entry per currency for a single period.
        builder.HasIndex(e => new { e.BranchId, e.FinancialDocumentType, e.FinancialDocumentId })
            .IsUnique()
            .HasFilter("\"IsReversal\" = FALSE AND \"FinancialDocumentType\" <> 'YearEndClosing'");

        // A journal entry may be reversed at most once. This is the authoritative
        // database invariant; ReversedByEntryId remains the convenient back-link.
        builder.HasIndex(e => e.ReversalOfEntryId)
            .IsUnique()
            .HasFilter("\"ReversalOfEntryId\" IS NOT NULL");

        // Composite index for common queries: branch + date range
        builder.HasIndex(e => new { e.BranchId, e.EntryDate });

        // Navigation: Lines (one-to-many)
        builder.HasMany(e => e.Lines)
            .WithOne(l => l.JournalEntry)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reversal tracking (self-referencing, Restrict to prevent cascade cycles)
        builder.HasOne(e => e.ReversalOfEntry)
            .WithMany()
            .HasForeignKey(e => e.ReversalOfEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReversedByEntry)
            .WithMany()
            .HasForeignKey(e => e.ReversedByEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Branch FK
        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // CashierSession FK
        builder.HasOne(e => e.CashierSession)
            .WithMany()
            .HasForeignKey(e => e.CashierSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Treasury FK
        builder.HasOne(e => e.Treasury)
            .WithMany()
            .HasForeignKey(e => e.TreasuryId)
            .OnDelete(DeleteBehavior.SetNull);

        // PerformedBy user FK
        builder.HasOne(e => e.PerformedByUser)
            .WithMany()
            .HasForeignKey(e => e.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
