/**
 * Tests for clinic-display-announcement helpers.
 *
 * Run with:  npx tsx scripts/test-clinic-announcement.ts
 *
 * All tests use Node.js assert module — no external test framework needed.
 */
import assert from "node:assert/strict";
import {
  buildAnnouncementText,
  formatFileNumberForSpeech,
  formatRoomForSpeech,
  RECEPTION_FALLBACK,
} from "../src/lib/clinic-display-announcement.js";

let passed = 0;
let failed = 0;

function test(name: string, fn: () => void) {
  try {
    fn();
    passed++;
    console.log(`  ✓ ${name}`);
  } catch (err) {
    failed++;
    console.error(`  ✗ ${name}`);
    console.error(`    ${(err as Error).message}`);
  }
}

console.log("\n=== Clinic Display Announcement Tests ===\n");

// ─── formatFileNumberForSpeech ────────────────────────────────────────────
console.log("formatFileNumberForSpeech:");

test("2020-622 → digit-by-digit Arabic", () => {
  assert.equal(
    formatFileNumberForSpeech("2020-622"),
    "اثنين صفر اثنين صفر ستة اثنين اثنين"
  );
});

test("GM2026-0022 → letters + digits", () => {
  assert.equal(
    formatFileNumberForSpeech("GM2026-0022"),
    "جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين"
  );
});

test("8501 → simple digits", () => {
  assert.equal(
    formatFileNumberForSpeech("8501"),
    "ثمانية خمسة صفر واحد"
  );
});

test("empty string → empty", () => {
  assert.equal(formatFileNumberForSpeech(""), "");
});

test("null → empty", () => {
  assert.equal(formatFileNumberForSpeech(null), "");
});

test("undefined → empty", () => {
  assert.equal(formatFileNumberForSpeech(undefined), "");
});

test("Arabic-Indic digits: ٢٠٢٠-٦٢٢", () => {
  assert.equal(
    formatFileNumberForSpeech("٢٠٢٠-٦٢٢"),
    "اثنين صفر اثنين صفر ستة اثنين اثنين"
  );
});

test("mixed Western+Arabic-Indic: 2020-٦٢٢", () => {
  assert.equal(
    formatFileNumberForSpeech("2020-٦٢٢"),
    "اثنين صفر اثنين صفر ستة اثنين اثنين"
  );
});

test("single digit: 5", () => {
  assert.equal(formatFileNumberForSpeech("5"), "خمسة");
});

test("separator / is skipped", () => {
  assert.equal(
    formatFileNumberForSpeech("2020/622"),
    "اثنين صفر اثنين صفر ستة اثنين اثنين"
  );
});

test("separator _ is skipped", () => {
  assert.equal(
    formatFileNumberForSpeech("GM_2026"),
    "جي إم اثنين صفر اثنين ستة"
  );
});

// ─── formatRoomForSpeech ──────────────────────────────────────────────────
console.log("\nformatRoomForSpeech:");

test("غرفة 1 → الغرفة رقم 1", () => {
  assert.equal(formatRoomForSpeech("غرفة 1"), "الغرفة رقم 1");
});

test("غرفة ٢ → الغرفة رقم ٢ (Arabic-Indic)", () => {
  assert.equal(formatRoomForSpeech("غرفة ٢"), "الغرفة رقم ٢");
});

test("empty → الاستقبال", () => {
  assert.equal(formatRoomForSpeech(""), RECEPTION_FALLBACK);
});

test("null → الاستقبال", () => {
  assert.equal(formatRoomForSpeech(null), RECEPTION_FALLBACK);
});

test("undefined → الاستقبال", () => {
  assert.equal(formatRoomForSpeech(undefined), RECEPTION_FALLBACK);
});

test("الاستقبال → الاستقبال (no number)", () => {
  assert.equal(formatRoomForSpeech("الاستقبال"), "الاستقبال");
});

// ─── buildAnnouncementText ────────────────────────────────────────────────
console.log("\nbuildAnnouncementText:");

test("full: علي أحمد, 2020-622, غرفة 1", () => {
  assert.equal(
    buildAnnouncementText("علي أحمد", "2020-622", "غرفة 1"),
    "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
  );
});

test("full: علي أحمد, GM2026-0022, غرفة 1", () => {
  assert.equal(
    buildAnnouncementText("علي أحمد", "GM2026-0022", "غرفة 1"),
    "المراجع علي أحمد، صاحب الملف رقم جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
  );
});

test("no name: empty, 2020-622, غرفة 2", () => {
  assert.equal(
    buildAnnouncementText("", "2020-622", "غرفة 2"),
    "المراجع، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 2"
  );
});

test("no room: علي أحمد, 2020-622, empty → الاستقبال", () => {
  assert.equal(
    buildAnnouncementText("علي أحمد", "2020-622", ""),
    "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الاستقبال"
  );
});

test("Arabic-Indic room: غرفة ١", () => {
  assert.equal(
    buildAnnouncementText("علي أحمد", "2020-622", "غرفة ١"),
    "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم ١"
  );
});

test("never uses المريض — always المراجع", () => {
  const result1 = buildAnnouncementText("سعد", "123", "غرفة 1");
  assert.ok(!result1.includes("المريض"), "Should not contain المريض");
  assert.ok(result1.includes("المراجع"), "Should contain المراجع");

  const result2 = buildAnnouncementText("", "123", "غرفة 1");
  assert.ok(!result2.includes("المريض"), "Should not contain المريض");
  assert.ok(result2.includes("المراجع"), "Should contain المراجع");
});

// ─── Summary ──────────────────────────────────────────────────────────────
console.log(`\n${"=".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
console.log(`${"=".repeat(50)}\n`);

if (failed > 0) {
  process.exit(1);
}
