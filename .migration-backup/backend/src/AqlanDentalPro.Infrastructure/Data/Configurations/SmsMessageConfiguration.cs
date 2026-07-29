using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class SmsMessageConfiguration : IEntityTypeConfiguration<SmsMessage>
{
    public void Configure(EntityTypeBuilder<SmsMessage> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.TemplateType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.MessageContent)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.ExternalId)
            .HasMaxLength(100);

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(s => s.Gateway)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.RelatedEntityType)
            .HasMaxLength(50);

        // Index on PatientId for patient SMS history
        builder.HasIndex(s => s.PatientId);

        // Index on Status for filtering pending/failed messages
        builder.HasIndex(s => s.Status);

        // Index on CreatedAt for sorting and time-based queries
        builder.HasIndex(s => s.CreatedAt);

        // Index on TemplateType for template-based queries
        builder.HasIndex(s => s.TemplateType);

        // Composite index for dashboard queries (today's messages by status)
        builder.HasIndex(s => new { s.CreatedAt, s.Status });

        builder.HasOne(s => s.Patient)
            .WithMany()
            .HasForeignKey(s => s.PatientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
