using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.UsernameOrEmail).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.UserId);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.ApprovedBy)
            .WithMany()
            .HasForeignKey(p => p.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
