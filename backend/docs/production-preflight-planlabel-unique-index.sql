-- ============================================================================
-- PRODUCTION PREFLIGHT: Unique Index Migration for TreatmentPlans.PlanLabel
-- Branch: feat/ortho-professional-clean | PR #200
-- ============================================================================
-- PURPOSE: Read-only verification BEFORE deploying the migration
--   20260612000000_AddUniqueIndex_TreatmentPlan_OrthoCaseId_PlanLabel
--
-- ⚠️  DO NOT execute any DML (INSERT/UPDATE/DELETE) on production data.
-- ⚠️  If duplicates are found, STOP and report for explicit approval.
-- ============================================================================

-- ══════════════════════════════════════════════════════════════════════════════
-- PHASE 1: Run BEFORE the migration is applied
-- (PlanLabel column may not exist yet; this checks for multi-plan cases)
-- ══════════════════════════════════════════════════════════════════════════════

-- 1A. Find ortho cases with more than one TreatmentPlan.
--     Before PlanLabel assignment, each case should have at most 1 plan
--     (legacy behavior). Multiple plans per case will need labels assigned
--     BEFORE the unique index can be created safely.
SELECT
    "OrthoCaseId",
    COUNT(*) AS plan_count
FROM "TreatmentPlans"
GROUP BY "OrthoCaseId"
HAVING COUNT(*) > 1;

-- Interpretation:
--   Zero rows  → Safe to proceed. All existing cases have ≤1 plan.
--                Old plans will receive default label 'A' (entity default).
--   Any rows   → STOP. Do NOT deploy. Report the affected OrthoCaseIds
--                and plan_counts for explicit approval and manual labeling.

-- 1B. Total count of TreatmentPlans (sanity check).
SELECT COUNT(*) AS total_treatment_plans FROM "TreatmentPlans";

-- 1C. Total count of distinct OrthoCaseIds that have at least one plan.
SELECT COUNT(DISTINCT "OrthoCaseId") AS cases_with_plans FROM "TreatmentPlans";


-- ══════════════════════════════════════════════════════════════════════════════
-- PHASE 2: Run AFTER the migration is applied (on staging/preview DB first!)
-- (PlanLabel column now exists; check for duplicate label pairs)
-- ══════════════════════════════════════════════════════════════════════════════

-- 2A. Check for duplicate (OrthoCaseId, PlanLabel) pairs.
--     This should return zero rows if the canonicalisation logic works.
SELECT
    "OrthoCaseId",
    "PlanLabel",
    COUNT(*) AS dup_count
FROM "TreatmentPlans"
GROUP BY "OrthoCaseId", "PlanLabel"
HAVING COUNT(*) > 1;

-- Interpretation:
--   Zero rows  → No duplicates. Unique index is safe.
--   Any rows   → STOP. Do NOT proceed to production.
--                Report OrthoCaseId, PlanLabel, dup_count for manual resolution.

-- 2B. Verify all PlanLabel values are canonical uppercase (A/B/C only).
--     Any non-uppercase or non-ABC value indicates a bypass in the API.
SELECT
    "Id",
    "OrthoCaseId",
    "PlanLabel"
FROM "TreatmentPlans"
WHERE "PlanLabel" NOT IN ('A', 'B', 'C')
   OR "PlanLabel" != UPPER("PlanLabel");

-- Interpretation:
--   Zero rows  → All stored PlanLabels are canonical uppercase. ✅
--   Any rows   → Non-canonical values found. Investigate the API path that
--                created them and apply the same Trim+ToUpper normalisation.


-- ══════════════════════════════════════════════════════════════════════════════
-- PHASE 3: Post-deploy verification (staging/preview)
-- ══════════════════════════════════════════════════════════════════════════════

-- 3A. Verify the unique index exists.
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'TreatmentPlans'
  AND indexname = 'IX_TreatmentPlans_OrthoCaseId_PlanLabel';

-- Expected: one row showing UNIQUE index on ("OrthoCaseId", "PlanLabel")

-- 3B. Quick smoke test: attempt to insert a duplicate (will fail if index works).
-- ⚠️  Run ONLY on staging/preview — NEVER on production.
-- BEGIN;
-- INSERT INTO "TreatmentPlans" ("Id", "OrthoCaseId", "PlanLabel", "CreatedAt", "UpdatedAt")
-- VALUES (gen_random_uuid(), '<existing_ortho_case_id>', 'A', NOW(), NOW());
-- -- Expected: ERROR: duplicate key value violates unique constraint
-- ROLLBACK;


-- ============================================================================
-- REMEDIATION (if duplicates found — DO NOT AUTO-APPLY):
-- ============================================================================
-- If Phase 1A returns cases with >1 plan, the following SAFE mapping can be
-- proposed for explicit approval before the migration:
--
-- For cases with 2 plans:  assign 'A' to the oldest, 'B' to the newest
-- For cases with 3 plans:  assign 'A', 'B', 'C' by creation order
-- For cases with >3 plans: flag for manual review
--
-- Example (REVIEW ONLY — do not execute without approval):
--
-- WITH ranked AS (
--     SELECT "Id", "OrthoCaseId",
--            ROW_NUMBER() OVER (PARTITION BY "OrthoCaseId" ORDER BY "CreatedAt") AS rn
--     FROM "TreatmentPlans"
--     WHERE "OrthoCaseId" IN (<affected_ids>)
-- )
-- UPDATE "TreatmentPlans" tp
-- SET "PlanLabel" = CASE rn WHEN 1 THEN 'A' WHEN 2 THEN 'B' WHEN 3 THEN 'C' END,
--     "UpdatedAt" = NOW()
-- FROM ranked r
-- WHERE tp."Id" = r."Id";
-- ============================================================================
