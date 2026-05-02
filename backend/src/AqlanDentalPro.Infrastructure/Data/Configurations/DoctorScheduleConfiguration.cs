using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.HasKey(ds => ds.Id);

        builder.Property(ds => ds.DayOfWeek).IsRequired();
        builder.Property(ds => ds.SlotDurationMinutes).HasDefaultValue(30);
        builder.Property(ds => ds.BreakStart).IsRequired(false);
        builder.Property(ds => ds.BreakEnd).IsRequired(false);

        // Unique index: one schedule per doctor per day
        builder.HasIndex(ds => new { ds.DoctorId, ds.DayOfWeek }).IsUnique();

        builder.HasOne(ds => ds.Doctor)
            .WithMany(d => d.Schedules)
            .HasForeignKey(ds => ds.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
