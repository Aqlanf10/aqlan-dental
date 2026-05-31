using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for Supplier entity indexes and constraints.
/// </summary>
public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(s => s.Id);

        builder.ToTable("Suppliers");

        // String properties
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactPerson).HasMaxLength(100).IsRequired(false);
        builder.Property(s => s.Phone).HasMaxLength(30).IsRequired(false);
        builder.Property(s => s.Email).HasMaxLength(200).IsRequired(false);
        builder.Property(s => s.Address).HasMaxLength(500).IsRequired(false);
        builder.Property(s => s.Notes).IsRequired(false);

        // Indexes
        builder.HasIndex(s => s.Name);
        builder.HasIndex(s => s.Phone);

        // Note: PurchaseOrders relationship is configured from the dependent side
        // in PurchaseOrderConfiguration to avoid duplicate configuration.
    }
}
