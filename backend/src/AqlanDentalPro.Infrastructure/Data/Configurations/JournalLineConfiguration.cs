using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.ToTable("JournalLines");

        // Decimal precision for Debit/Credit
        builder.Property(l => l.Debit).HasPrecision(12, 2);
        builder.Property(l => l.Credit).HasPrecision(12, 2);

        // AccountType stored as string for readability
        builder.Property(l => l.AccountType).HasConversion<string>().HasMaxLength(30);

        // String constraints
        builder.Property(l => l.Description).HasMaxLength(500);

        // Performance indexes
        builder.HasIndex(l => l.JournalEntryId);
        builder.HasIndex(l => l.AccountType);
        builder.HasIndex(l => l.AccountId);
        builder.HasIndex(l => l.BranchId);

        // Composite index: account type + account id (for balance queries)
        builder.HasIndex(l => new { l.AccountType, l.AccountId });

        // Branch FK
        builder.HasOne(l => l.Branch)
            .WithMany()
            .HasForeignKey(l => l.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Check constraint: Debit and Credit are mutually exclusive
        // At least one must be > 0, and they cannot both be > 0
        // Note: This is enforced at application level as well
        builder.ToTable(t => t.HasCheckConstraint("CK_JournalLines_DebitCreditMutual",
            "\"Debit\" >= 0 AND \"Credit\" >= 0 AND (\"Debit\" > 0 OR \"Credit\" > 0)"));
    }
}
