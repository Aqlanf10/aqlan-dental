using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// H7/H9 FIX: FluentAPI configuration for Receipt entity.
/// Previously, Receipt had zero configuration — no unique index on ReceiptNumber,
/// no FK relationship to Payment, no max-length constraints.
/// ReceiptNumber collisions violated financial audit requirements.
/// </summary>
public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        // ReceiptNumber must be unique — audit requirement
        builder.HasIndex(r => r.ReceiptNumber).IsUnique();
        builder.Property(r => r.ReceiptNumber).HasMaxLength(50).IsRequired();

        // FK relationship to Payment
        builder.HasOne(r => r.Payment)
            .WithOne(p => p.Receipt)
            .HasForeignKey<Receipt>(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
