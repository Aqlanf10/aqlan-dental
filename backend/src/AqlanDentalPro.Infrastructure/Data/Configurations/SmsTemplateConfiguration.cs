using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class SmsTemplateConfiguration : IEntityTypeConfiguration<SmsTemplate>
{
    public void Configure(EntityTypeBuilder<SmsTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TemplateKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.NameAr)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.ContentTemplate)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasMaxLength(30)
            .IsRequired();

        // Unique index on TemplateKey
        builder.HasIndex(t => t.TemplateKey)
            .IsUnique();

        // Index on Category for template grouping
        builder.HasIndex(t => t.Category);
    }
}
