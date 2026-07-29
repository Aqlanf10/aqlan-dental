using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>
    /// Converts JsonDocument ↔ string for database storage.
    /// Required for InMemory provider compatibility (EF Core cannot construct JsonDocument
    /// via constructor binding). With Npgsql, HasColumnType("jsonb") still stores as jsonb.
    /// </summary>
    private static readonly ValueConverter<JsonDocument?, string?> JsonDocumentConverter = new(
        v => v == null ? null : v.RootElement.GetRawText(),
        v => string.IsNullOrEmpty(v) ? null : JsonDocument.Parse(v, default));

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Resource).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(50);

        // JsonDocument value converter: required for InMemory provider compatibility.
        // Npgsql handles JsonDocument → jsonb natively, but the explicit converter
        // ensures the InMemory provider can serialize/deserialize JsonDocument
        // without attempting constructor binding (which fails for System.Text.Json types).
        // HasColumnType("jsonb") still directs PostgreSQL to store as jsonb.
        builder.Property(a => a.OldData)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter);

        builder.Property(a => a.NewData)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter);

        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => new { a.Resource, a.ResourceId });

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // AuditLog is append-only — no global soft delete filter
        builder.HasQueryFilter(a => true);
    }
}
