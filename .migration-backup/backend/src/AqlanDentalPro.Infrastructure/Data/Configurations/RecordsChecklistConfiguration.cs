using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class RecordsChecklistConfiguration : IEntityTypeConfiguration<RecordsChecklist>
{
    public void Configure(EntityTypeBuilder<RecordsChecklist> builder)
    {
        builder.HasKey(r => r.Id);
        builder.ToTable("RecordsChecklists");

        builder.HasIndex(r => r.OrthoCaseId).IsUnique();

        builder.HasOne(r => r.OrthoCase)
            .WithOne(c => c.RecordsChecklist)
            .HasForeignKey<RecordsChecklist>(r => r.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
