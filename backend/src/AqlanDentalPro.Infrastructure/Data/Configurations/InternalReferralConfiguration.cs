using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class InternalReferralConfiguration : IEntityTypeConfiguration<InternalReferral>
{
    public void Configure(EntityTypeBuilder<InternalReferral> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Patient)
            .WithMany(p => p.ReferralsFrom)
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.FromDoctor)
            .WithMany()
            .HasForeignKey(r => r.FromDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ToDoctor)
            .WithMany()
            .HasForeignKey(r => r.ToDoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
