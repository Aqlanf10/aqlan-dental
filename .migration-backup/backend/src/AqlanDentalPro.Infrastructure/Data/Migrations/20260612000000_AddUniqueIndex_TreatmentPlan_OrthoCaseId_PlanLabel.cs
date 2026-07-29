using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds a unique composite index on TreatmentPlans (OrthoCaseId, PlanLabel)
/// to prevent duplicate plan labels within the same orthodontic case.
/// This migration is safe only if no duplicate (OrthoCaseId, PlanLabel) pairs exist in production data.
/// If duplicates exist, the migration will fail at runtime and manual deduplication will be required first.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260612000000_AddUniqueIndex_TreatmentPlan_OrthoCaseId_PlanLabel")]
public partial class AddUniqueIndex_TreatmentPlan_OrthoCaseId_PlanLabel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TreatmentPlans_OrthoCaseId_PlanLabel",
            table: "TreatmentPlans",
            columns: new[] { "OrthoCaseId", "PlanLabel" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TreatmentPlans_OrthoCaseId_PlanLabel",
            table: "TreatmentPlans");
    }
}
