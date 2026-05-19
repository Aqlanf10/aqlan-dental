using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class ClinicRoomConfiguration : IEntityTypeConfiguration<ClinicRoom>
{
    public void Configure(EntityTypeBuilder<ClinicRoom> builder)
    {
        builder.ToTable("ClinicRooms");

        builder.Property(r => r.ArabicName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.EnglishName)
            .HasMaxLength(200);

        builder.Property(r => r.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.RoomType)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Unique code index
        builder.HasIndex(r => r.Code)
            .IsUnique();

        builder.HasIndex(r => r.RoomType);
    }
}
