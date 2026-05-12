import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { buildAnnouncementText, formatRoomForSpeech } from "../src/lib/clinic-display-announcement.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const frontendRoot = join(__dirname, "..");
const pagePath = join(frontendRoot, "src/app/clinic-display/page.tsx");
const pageSource = readFileSync(pagePath, "utf8");

function verifyAnnouncementExamples() {
  assert.equal(
    buildAnnouncementText("علي أحمد", "8501", "غرفة 1"),
    "المريض علي أحمد، رقم الملف 8501، يرجى التوجه إلى الغرفة رقم 1",
    "Arabic announcement must include patient name, file number, and room number"
  );

  assert.equal(
    buildAnnouncementText("", "8501", "غرفة 2"),
    "صاحب الملف رقم 8501، يرجى التوجه إلى الغرفة رقم 2",
    "Missing patient name must fall back to file number"
  );

  assert.equal(
    buildAnnouncementText("علي أحمد", "8501", ""),
    "المريض علي أحمد، رقم الملف 8501، يرجى التوجه إلى الاستقبال",
    "Missing room must fall back to reception"
  );

  assert.equal(
    buildAnnouncementText("  علي أحمد  ", " 8501 ", "غرفة 3"),
    "المريض علي أحمد، رقم الملف 8501، يرجى التوجه إلى الغرفة رقم 3",
    "Announcement must trim patient name and file number"
  );
}

function verifyArabicIndicRoomDigits() {
  assert.equal(formatRoomForSpeech("غرفة ١"), "الغرفة رقم ١");
  assert.equal(
    buildAnnouncementText("علي أحمد", "8501", "غرفة ١"),
    "المريض علي أحمد، رقم الملف 8501، يرجى التوجه إلى الغرفة رقم ١",
    "Arabic-Indic room digits must be preserved in speech"
  );
}

function verifyPageStillContainsAnnouncementGuardrails() {
  assert.match(
    pageSource,
    /Allowed spoken fields: patient name, file number, room number/,
    "Clinic display page must document the allowed spoken fields"
  );
  assert.match(
    pageSource,
    /Forbidden spoken fields: phone, diagnosis, payment, balance, treatment notes,/,
    "Clinic display page must document forbidden private spoken fields"
  );
  assert.match(
    pageSource,
    /المريض \$\{patientName\.trim\(\)\}\$\{filePart\}، يرجى التوجه إلى \$\{destination\}/,
    "Clinic display page must speak patient name + file number + destination"
  );
  assert.match(
    pageSource,
    /صاحب الملف رقم \$\{fileNumber \|\| patientNumber\}، يرجى التوجه إلى \$\{destination\}/,
    "Clinic display page must fall back to file number when patient name is missing"
  );
  assert.match(
    pageSource,
    /الغرفة رقم \$\{numberMatch\[0\]\}/,
    "Clinic display page must say الغرفة رقم before the extracted room number"
  );
}

verifyAnnouncementExamples();
verifyArabicIndicRoomDigits();
verifyPageStillContainsAnnouncementGuardrails();

console.log("Clinic display announcement text verified successfully.");
