using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephAiModelDeploymentConfiguration : IEntityTypeConfiguration<CephAiModelDeployment>
{
    public void Configure(EntityTypeBuilder<CephAiModelDeployment> builder)
    {
        builder.ToTable("CephAiModelDeployments");
        builder.Property(item => item.Slot).HasMaxLength(80).IsRequired();
        builder.HasIndex(item => item.Slot).IsUnique();
        builder.HasOne(item => item.ActiveModelVersion)
            .WithMany()
            .HasForeignKey(item => item.ActiveModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PreviousModelVersion)
            .WithMany()
            .HasForeignKey(item => item.PreviousModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
