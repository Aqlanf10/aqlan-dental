using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LabPayableConfiguration : IEntityTypeConfiguration<LabPayable>
{
    public void Configure(EntityTypeBuilder<LabPayable> builder)
    {
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasIndex(p => p.LabOrderId);
        builder.HasIndex(p => p.LabId);
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.LabOrder)
            .WithMany()
            .HasForeignKey(p => p.LabOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Lab)
            .WithMany()
            .HasForeignKey(p => p.LabId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
