using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Resource).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.OldData).HasColumnType("jsonb");
        builder.Property(a => a.NewData).HasColumnType("jsonb");

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
