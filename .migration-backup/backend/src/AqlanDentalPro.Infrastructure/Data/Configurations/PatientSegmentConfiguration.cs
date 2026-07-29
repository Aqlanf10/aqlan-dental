using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// YOLO-S5: PatientSegment entity configuration.
/// </summary>
public class PatientSegmentConfiguration : IEntityTypeConfiguration<PatientSegment>
{
    public void Configure(EntityTypeBuilder<PatientSegment> builder)
    {
        builder.HasKey(s => s.Id);
        builder.ToTable("PatientSegments");

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000).IsRequired(false);
        builder.Property(s => s.Color).HasMaxLength(20).IsRequired(false);
        builder.Property(s => s.QueryJson).HasColumnType("text").IsRequired(false);
        builder.Property(s => s.IsDynamic).IsRequired();

        builder.HasIndex(s => s.Name);
        builder.HasIndex(s => s.IsActive);

        // Members navigation configured from the dependent side
        // in PatientSegmentMemberConfiguration to avoid duplicate configuration.
    }
}

/// <summary>
/// YOLO-S5: PatientSegmentMember join-table configuration.
/// </summary>
public class PatientSegmentMemberConfiguration : IEntityTypeConfiguration<PatientSegmentMember>
{
    public void Configure(EntityTypeBuilder<PatientSegmentMember> builder)
    {
        builder.HasKey(m => m.Id);
        builder.ToTable("PatientSegmentMembers");

        builder.Property(m => m.AddedAt).IsRequired();

        builder.HasIndex(m => new { m.SegmentId, m.PatientId }).IsUnique();
        builder.HasIndex(m => m.SegmentId);
        builder.HasIndex(m => m.PatientId);

        builder.HasOne(m => m.Segment)
            .WithMany(s => s.Members)
            .HasForeignKey(m => m.SegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Patient)
            .WithMany()
            .HasForeignKey(m => m.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
