using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class ClinicServiceConfiguration : IEntityTypeConfiguration<ClinicService>
{
    public void Configure(EntityTypeBuilder<ClinicService> builder)
    {
        builder.ToTable("ClinicServices");

        builder.Property(s => s.ArabicName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.EnglishName)
            .HasMaxLength(200);

        builder.Property(s => s.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Department)
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(s => s.DefaultPrice)
            .HasPrecision(12, 2);

        // YOLO-S2: Color (hex string, nullable) — for calendar + queue display.
        builder.Property(s => s.Color)
            .HasMaxLength(20);

        // Unique code index
        builder.HasIndex(s => s.Code)
            .IsUnique();

        // Filter index for active services
        builder.HasIndex(s => s.Category);
    }
}
