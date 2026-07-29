using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.Property(d => d.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(300);
        builder.Property(d => d.ContentType).HasMaxLength(100);

        builder.HasIndex(d => d.EmployeeId);

        builder.HasOne(d => d.Employee)
            .WithMany()
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
