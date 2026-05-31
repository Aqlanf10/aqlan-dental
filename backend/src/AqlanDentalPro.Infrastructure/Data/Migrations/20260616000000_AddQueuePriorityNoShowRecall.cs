using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Queue Enhancements Sprint:
/// - Adds Priority column (Normal/Urgent/VIP/Emergency) to ClinicQueueItems
/// - Adds SortOrder column for manual drag-and-drop reordering
/// - Adds RecallCount column for tracking how many times a patient was called
/// - Adds NoShowAt timestamp for patients who didn't show up
/// - Adds NoShow status value to the Status enum (stored as string)
/// - Updates unique filter to exclude NoShow status
/// - Adds composite index on (QueueDate, Priority, SortOrder) for priority-based ordering
///
/// All new columns have safe defaults and are non-breaking:
/// - Priority defaults to 'Normal'
/// - SortOrder defaults to 0
/// - RecallCount defaults to 0
/// - NoShowAt is nullable
/// - Existing rows will have Priority='Normal', SortOrder=0, RecallCount=0
///
/// The unique index on (PatientId, QueueDate) is recreated with an updated filter
/// that also excludes 'NoShow' status, allowing a patient who was marked NoShow
/// to be re-added to the queue on the same day.
/// </summary>
public partial class AddQueuePriorityNoShowRecall : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add Priority column — stored as string, default 'Normal'
        migrationBuilder.AddColumn<string>(
            name: "Priority",
            table: "ClinicQueueItems",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Normal");

        // Add SortOrder column for drag-and-drop reordering
        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "ClinicQueueItems",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Add RecallCount column — how many times patient was called
        migrationBuilder.AddColumn<int>(
            name: "RecallCount",
            table: "ClinicQueueItems",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Add NoShowAt timestamp
        migrationBuilder.AddColumn<DateTime>(
            name: "NoShowAt",
            table: "ClinicQueueItems",
            type: "timestamp with time zone",
            nullable: true);

        // Add composite index for priority-based ordering
        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_QueueDate_Priority_SortOrder",
            table: "ClinicQueueItems",
            columns: new[] { "QueueDate", "Priority", "SortOrder" });

        // Drop old unique index and recreate with updated filter (include NoShow in exclusion)
        migrationBuilder.DropIndex(
            name: "IX_ClinicQueueItems_PatientId_QueueDate",
            table: "ClinicQueueItems");

        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_PatientId_QueueDate",
            table: "ClinicQueueItems",
            columns: new[] { "PatientId", "QueueDate" },
            unique: true,
            filter: "\"Status\" NOT IN ('Completed', 'Cancelled', 'NoShow')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop new index
        migrationBuilder.DropIndex(
            name: "IX_ClinicQueueItems_QueueDate_Priority_SortOrder",
            table: "ClinicQueueItems");

        // Revert unique index to original filter
        migrationBuilder.DropIndex(
            name: "IX_ClinicQueueItems_PatientId_QueueDate",
            table: "ClinicQueueItems");

        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_PatientId_QueueDate",
            table: "ClinicQueueItems",
            columns: new[] { "PatientId", "QueueDate" },
            unique: true,
            filter: "\"Status\" NOT IN ('Completed', 'Cancelled')");

        // Drop new columns
        migrationBuilder.DropColumn(
            name: "NoShowAt",
            table: "ClinicQueueItems");

        migrationBuilder.DropColumn(
            name: "RecallCount",
            table: "ClinicQueueItems");

        migrationBuilder.DropColumn(
            name: "SortOrder",
            table: "ClinicQueueItems");

        migrationBuilder.DropColumn(
            name: "Priority",
            table: "ClinicQueueItems");
    }
}
