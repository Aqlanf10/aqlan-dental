using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LabOrderAttachmentConfiguration : IEntityTypeConfiguration<LabOrderAttachment>
{
    public void Configure(EntityTypeBuilder<LabOrderAttachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Category).HasMaxLength(50).IsRequired();
        builder.Property(a => a.StoragePath).HasMaxLength(1000).IsRequired();

        builder.HasIndex(a => a.LabOrderId);
        builder.HasIndex(a => a.LabOrderItemId);

        builder.HasOne(a => a.LabOrder)
            .WithMany(o => o.Attachments)
            .HasForeignKey(a => a.LabOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.LabOrderItem)
            .WithMany()
            .HasForeignKey(a => a.LabOrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.UploadedByUser)
            .WithMany()
            .HasForeignKey(a => a.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
