using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// H7 FIX: FluentAPI configuration for Prescription entity.
/// Previously, Prescription had zero configuration — no indexes on PatientId/DoctorId,
/// no Drugs jsonb optimization for PostgreSQL.
/// </summary>
public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        // Indexes for common query patterns
        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.VisitId);
        builder.HasIndex(p => p.DoctorId);

        // Drugs is stored as jsonb — use PostgreSQL native column type
        builder.Property(p => p.Drugs)
            .HasColumnType("jsonb");

        // String constraints
        builder.Property(p => p.Diagnosis).HasMaxLength(1000);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        // Relationships
        builder.HasOne(p => p.Patient)
            .WithMany(pt => pt.Prescriptions)
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Visit)
            .WithMany(v => v.Prescriptions)
            .HasForeignKey(p => p.VisitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Doctor)
            .WithMany()
            .HasForeignKey(p => p.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
