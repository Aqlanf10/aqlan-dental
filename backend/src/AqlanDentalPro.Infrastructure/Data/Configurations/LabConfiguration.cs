using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Lab Sprint 2 — Fluent API configuration for Lab entity.
/// </summary>
public class LabConfiguration : IEntityTypeConfiguration<Lab>
{
    public void Configure(EntityTypeBuilder<Lab> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Phone).HasMaxLength(30);
        builder.Property(l => l.WhatsApp).HasMaxLength(30);
        builder.Property(l => l.Address).HasMaxLength(500);
        builder.Property(l => l.ContactPerson).HasMaxLength(200);
        builder.Property(l => l.Email).HasMaxLength(200);

        builder.HasIndex(l => l.Name);
        builder.HasIndex(l => l.BranchId);

        builder.HasOne(l => l.Branch)
            .WithMany()
            .HasForeignKey(l => l.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
