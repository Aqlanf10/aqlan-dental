using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260714042447_AddCephAiModelLineage")]
public partial class AddCephAiModelLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CephAiModelLineageSchema.UpSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CephLandmarks_CephAiInferenceRuns_SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropIndex(
            name: "IX_CephLandmarks_SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropColumn(
            name: "SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropTable(name: "CephAiInferenceRuns");
        migrationBuilder.DropTable(name: "CephAiModelDeployments");
        migrationBuilder.DropTable(name: "CephAiModelVersions");
    }
}
