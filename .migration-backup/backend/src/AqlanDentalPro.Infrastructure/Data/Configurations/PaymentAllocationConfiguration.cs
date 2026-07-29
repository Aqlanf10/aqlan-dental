using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");

        builder.Property(a => a.Amount).HasPrecision(12, 2);
        builder.Property(a => a.Currency).HasMaxLength(3).HasDefaultValue("YER");
        builder.Property(a => a.Notes).HasMaxLength(500);

        builder.HasIndex(a => a.PaymentId);
        builder.HasIndex(a => a.InvoiceId);
        builder.HasIndex(a => new { a.InvoiceId, a.IsActive });
        builder.HasIndex(a => new { a.PaymentId, a.IsActive });
        builder.HasIndex(a => new { a.PaymentId, a.InvoiceId })
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");
        builder.HasIndex(a => a.JournalEntryId).IsUnique();

        builder.HasOne(a => a.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(a => a.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Invoice)
            .WithMany(i => i.PaymentAllocations)
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.JournalEntry)
            .WithMany()
            .HasForeignKey(a => a.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
