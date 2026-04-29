using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentPlanABSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanLabel",
                table: "TreatmentPlans",
                type: "text",
                nullable: false,
                defaultValue: "A");

            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                table: "TreatmentPlans",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanLabel",
                table: "TreatmentPlans");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                table: "TreatmentPlans");
        }
    }
}
