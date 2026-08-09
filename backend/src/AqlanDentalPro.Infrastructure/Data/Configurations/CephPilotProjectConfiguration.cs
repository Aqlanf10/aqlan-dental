using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotProjectConfiguration : IEntityTypeConfiguration<CephPilotProject>
{
    public void Configure(EntityTypeBuilder<CephPilotProject> builder)
    {
        // The three checks below are declared here as well as in migration
        // 20260718000000_AddCephPilotFoundation, and the SQL is deliberately identical.
        //
        // A brand-new database is not built by replaying the migration chain — it is
        // built from GenerateCreateScript() over the EF model (see
        // StartupDatabaseMaintenance.EnsureFreshDatabaseMigratedAsync). Anything the model
        // does not know about therefore never reaches a fresh database. That is CORE-F-002,
        // and it has already produced real defects: PR #812 found a ledger table and two
        // ledger triggers missing from every fresh database for exactly this reason.
        //
        // Here the cost would be higher than a missing table. These checks ARE the study's
        // integrity guarantees: without them PostgreSQL would accept a Pilot project whose
        // blinding is switched off, or one person assigned as both reviewers. Losing them
        // silently on a new deployment would invalidate the comparison while every screen
        // continued to look correct.
        builder.ToTable("CephPilotProjects", table =>
        {
            table.HasCheckConstraint(
                "CK_CephPilotProjects_DistinctReviewers",
                "\"ReviewerAUserId\" <> \"ReviewerBUserId\" AND (\"AdjudicatorUserId\" IS NULL OR (\"AdjudicatorUserId\" <> \"ReviewerAUserId\" AND \"AdjudicatorUserId\" <> \"ReviewerBUserId\"))");

            table.HasCheckConstraint(
                "CK_CephPilotProjects_RatifiedVersion",
                "\"LandmarkDefinitionVersion\" = 'ADP-LM-LAT-v1.0'");

            table.HasCheckConstraint(
                "CK_CephPilotProjects_BlindingEnabled",
                "\"IsAiHidden\" = TRUE AND \"IsWebCephHidden\" = TRUE");
        });
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.LandmarkDefinitionVersion).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(item => item.DatasetVersion).HasMaxLength(100);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => new { item.ReviewerAUserId, item.Status, item.IsActive });
        builder.HasIndex(item => new { item.ReviewerBUserId, item.Status, item.IsActive });

        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ReviewerAUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ReviewerBUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.AdjudicatorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
