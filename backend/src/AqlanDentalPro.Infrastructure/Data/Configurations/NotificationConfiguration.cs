using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .HasMaxLength(50);

        builder.Property(n => n.Title)
            .HasMaxLength(200);

        builder.Property(n => n.Body)
            .HasMaxLength(2000);

        builder.Property(n => n.RelatedEntity)
            .HasMaxLength(100);

        // مؤشر على UserId + IsRead لتحسين أداء استعلام عدد غير المقروء
        builder.HasIndex(n => new { n.UserId, n.IsRead });

        // مؤشر على UserId لتحسين جلب إشعارات المستخدم
        builder.HasIndex(n => n.UserId);

        // مؤشر على CreatedAt لتحسين الترتيب والتصفية الزمنية
        builder.HasIndex(n => n.CreatedAt);

        // مؤشر مركب لمنع تكرار الإشعارات: Type + RelatedEntity + RelatedId + CreatedAt
        builder.HasIndex(n => new { n.Type, n.RelatedEntity, n.RelatedId });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
