using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotProjectConfiguration : IEntityTypeConfiguration<CephPilotProject>
{
    public void Configure(EntityTypeBuilder<CephPilotProject> builder)
    {
        builder.ToTable("CephPilotProjects");
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.LandmarkDefinitionVersion).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.DatasetVersion).HasMaxLength(100);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.ReviewerAUserId, item.Status, item.IsActive });
        builder.HasIndex(item => new { item.ReviewerBUserId, item.Status, item.IsActive });

        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ReviewerAUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ReviewerBUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.AdjudicatorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
