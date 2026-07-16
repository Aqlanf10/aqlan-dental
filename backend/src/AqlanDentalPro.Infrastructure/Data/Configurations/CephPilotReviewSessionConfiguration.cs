using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotReviewSessionConfiguration : IEntityTypeConfiguration<CephPilotReviewSession>
{
    public void Configure(EntityTypeBuilder<CephPilotReviewSession> builder)
    {
        builder.ToTable("CephPilotReviewSessions");
        builder.Property(item => item.ReviewerSlot).HasConversion<string>().HasMaxLength(1).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.LandmarkDefinitionVersion).HasMaxLength(50).IsRequired();
        builder.Property(item => item.ToolVersion).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => new { item.CaseId, item.ReviewerUserId }).IsUnique();
        builder.HasIndex(item => new { item.CaseId, item.ReviewerSlot }).IsUnique();
        builder.HasOne(item => item.Case)
            .WithMany(item => item.ReviewSessions)
            .HasForeignKey(item => item.CaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
