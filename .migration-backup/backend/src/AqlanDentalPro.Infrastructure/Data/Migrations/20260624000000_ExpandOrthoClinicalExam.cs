using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Ortho Module Phase 3 — structured clinical examination.
///
/// Adds nullable columns to OrthoClinicalExams (all additive — no existing column
/// is renamed or retyped):
/// - Occlusal right/left split (molar/canine), incisor relation, overbite percent,
///   deep/scissor bite, crossbite type, signed midline shifts (mm), crowding/spacing (mm),
///   curve of Spee, arch forms, Bolton note.
/// - Extraoral: lip competence grade, nasolabial angle, chin position,
///   functional shift, gummy smile.
/// - Structured habit flags (thumb sucking, mouth breathing, tongue thrust,
///   lip biting, nail biting, bruxism) — legacy free-text Habits kept.
/// - Intraoral health: oral hygiene, gingival/periodontal, FDI lists
///   (missing/retained deciduous/impacted), supernumerary/ectopic/frenum/tongue/caries notes.
///
/// Everything is idempotent raw SQL (ADD COLUMN IF NOT EXISTS / guarded drops),
/// matching the established hand-written migration pattern in this project.
/// </summary>
public partial class ExpandOrthoClinicalExam : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""OrthoClinicalExams""') IS NOT NULL THEN
        -- Occlusal: right/left split + missing measures
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MolarRelationRight"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MolarRelationLeft"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""CanineRelationRight"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""CanineRelationLeft"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""IncisorRelation"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""OverbitePercent"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""DeepBite"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""CrossbiteType"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ScissorBite"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MidlineUpperShiftMm"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MidlineLowerShiftMm"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""UpperCrowdingMm"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""LowerCrowdingMm"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""LowerSpacingMm"" numeric NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""CurveOfSpee"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ArchFormUpper"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ArchFormLower"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""BoltonDiscrepancyNote"" text NULL;
        -- Extraoral additions
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""LipCompetenceGrade"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""NasolabialAngle"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ChinPosition"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""FunctionalShift"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""GummySmile"" boolean NULL;
        -- Structured habit flags
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ThumbSucking"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MouthBreathing"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""TongueThrust"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""LipBiting"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""NailBiting"" boolean NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""Bruxism"" boolean NULL;
        -- Intraoral health
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""OralHygiene"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""GingivalCondition"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""PeriodontalConcerns"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""MissingTeethFdi"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""RetainedDeciduousFdi"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""ImpactedTeethFdi"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""SupernumeraryNote"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""EctopicEruptionNote"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""FrenumNote"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""TongueNote"" text NULL;
        ALTER TABLE ""OrthoClinicalExams"" ADD COLUMN IF NOT EXISTS ""CariesNote"" text NULL;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""OrthoClinicalExams""') IS NOT NULL THEN
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MolarRelationRight"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MolarRelationLeft"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""CanineRelationRight"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""CanineRelationLeft"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""IncisorRelation"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""OverbitePercent"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""DeepBite"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""CrossbiteType"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ScissorBite"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MidlineUpperShiftMm"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MidlineLowerShiftMm"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""UpperCrowdingMm"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""LowerCrowdingMm"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""LowerSpacingMm"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""CurveOfSpee"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ArchFormUpper"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ArchFormLower"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""BoltonDiscrepancyNote"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""LipCompetenceGrade"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""NasolabialAngle"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ChinPosition"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""FunctionalShift"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""GummySmile"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ThumbSucking"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MouthBreathing"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""TongueThrust"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""LipBiting"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""NailBiting"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""Bruxism"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""OralHygiene"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""GingivalCondition"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""PeriodontalConcerns"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""MissingTeethFdi"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""RetainedDeciduousFdi"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""ImpactedTeethFdi"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""SupernumeraryNote"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""EctopicEruptionNote"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""FrenumNote"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""TongueNote"";
        ALTER TABLE ""OrthoClinicalExams"" DROP COLUMN IF EXISTS ""CariesNote"";
    END IF;
END $$;
");
    }
}
