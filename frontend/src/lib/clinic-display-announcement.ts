/**
 * Clinic Display Announcement Helpers
 *
 * Provides pure functions for building privacy-safe Arabic voice announcement
 * text for the clinic TV display. All digit-by-digit conversion and wording
 * rules are centralized here as the single source of truth.
 *
 * IMPORTANT: /clinic-display/page.tsx MUST import from this module.
 * Do NOT duplicate these functions inline in any page component.
 */

/* ─── Constants ───────────────────────────────────────────────────────────── */

export const RECEPTION_FALLBACK = "الاستقبال";

/** Map of English letters to their Arabic pronunciation for speech synthesis. */
const LETTER_NAMES: Record<string, string> = {
  A: "إيه",
  B: "بي",
  C: "سي",
  D: "دي",
  E: "إي",
  F: "إف",
  G: "جي",
  H: "إتش",
  I: "آي",
  J: "جاي",
  K: "كي",
  L: "إل",
  M: "إم",
  N: "إن",
  O: "أو",
  P: "بي",
  Q: "كيو",
  R: "آر",
  S: "إس",
  T: "تي",
  U: "يو",
  V: "في",
  W: "دبليو",
  X: "إكس",
  Y: "واي",
  Z: "زد",
};

/** Map of digits (Western 0-9 and Arabic-Indic ٠-٩) to Arabic word names. */
const DIGIT_NAMES: Record<string, string> = {
  "0": "صفر",
  "1": "واحد",
  "2": "اثنين",
  "3": "ثلاثة",
  "4": "أربعة",
  "5": "خمسة",
  "6": "ستة",
  "7": "سبعة",
  "8": "ثمانية",
  "9": "تسعة",
  "٠": "صفر",
  "١": "واحد",
  "٢": "اثنين",
  "٣": "ثلاثة",
  "٤": "أربعة",
  "٥": "خمسة",
  "٦": "ستة",
  "٧": "سبعة",
  "٨": "ثمانية",
  "٩": "تسعة",
};

/** Separators that are silently skipped during file-number speech (not spoken). */
const SEPARATOR_CHARS = new Set(["-", "_", "/", " ", "."]);

/* ─── Room formatting ─────────────────────────────────────────────────────── */

/**
 * Format a room name for speech synthesis.
 *
 * If the roomName contains a number (Arabic or Western digits),
 * extract it and return "الغرفة رقم [number]" for clearer speech.
 * If the roomName contains no number, return it as-is.
 * If roomName is empty/missing, return "الاستقبال" (reception).
 *
 * Examples:
 *   "غرفة 1"  → "الغرفة رقم 1"
 *   "غرفة ٢"  → "الغرفة رقم ٢"
 *   "3"       → "الغرفة رقم 3"
 *   "الاستقبال" → "الاستقبال"
 *   ""         → "الاستقبال"
 */
export function formatRoomForSpeech(roomName?: string | null): string {
  const trimmed = roomName?.trim();
  if (!trimmed) return RECEPTION_FALLBACK;

  // Match Western digits (0-9) or Arabic-Indic digits (٠-٩)
  const numberMatch = trimmed.match(/\d+|[٠-٩]+/);
  if (numberMatch) {
    return `الغرفة رقم ${numberMatch[0]}`;
  }

  return trimmed;
}

/* ─── File number formatting ──────────────────────────────────────────────── */

/**
 * Pronounce each digit of a number string individually in Arabic.
 *
 * Example: "622" → "ستة اثنين اثنين"
 */
function pronounceDigits(numberPart: string): string {
  return numberPart
    .split("")
    .map((digit) => DIGIT_NAMES[digit] ?? digit)
    .join(" ");
}

/**
 * Convert a file number string into speech-safe Arabic text.
 *
 * Rules:
 * - Each English letter is converted to its Arabic pronunciation (G→جي, M→إم)
 * - Each digit is converted to its Arabic word name (2→اثنين, 6→ستة)
 * - Arabic-Indic digits (٠-٩) are also supported
 * - Separators (-, _, /, space, .) are silently skipped
 * - Unknown characters are passed through as-is
 *
 * CRITICAL: This function MUST be called on file numbers before passing
 * text to SpeechSynthesis. Never send raw file numbers like "2020-622"
 * or "GM2026-0022" to the speech engine.
 *
 * Examples:
 *   "2020-622"   → "اثنين صفر اثنين صفر ستة اثنين اثنين"
 *   "GM2026-0022" → "جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين"
 *   "8501"        → "ثمانية خمسة صفر واحد"
 *   ""            → ""
 *   null          → ""
 */
export function formatFileNumberForSpeech(patientNumber?: string | null): string {
  const trimmed = patientNumber?.trim();
  if (!trimmed) return "";

  const tokens: string[] = [];
  let currentNumber = "";

  const flushNumber = () => {
    if (currentNumber) {
      tokens.push(pronounceDigits(currentNumber));
      currentNumber = "";
    }
  };

  for (const char of trimmed) {
    const upper = char.toUpperCase();
    if (LETTER_NAMES[upper]) {
      flushNumber();
      tokens.push(LETTER_NAMES[upper]);
    } else if (/\d/.test(char) || /[٠-٩]/.test(char)) {
      currentNumber += char;
    } else if (SEPARATOR_CHARS.has(char)) {
      // Silently skip separators — do not speak them
      flushNumber();
    } else {
      flushNumber();
      tokens.push(char);
    }
  }

  flushNumber();
  return tokens.filter(Boolean).join(" ").replace(/\s+/g, " ").trim();
}

/* ─── Announcement text builder ───────────────────────────────────────────── */

/**
 * Build the privacy-safe Arabic announcement text for a patient call.
 *
 * This is the SINGLE SOURCE OF TRUTH for announcement wording.
 * Do NOT create duplicate versions of this function in page components.
 *
 * Wording rules:
 * - Always say "المراجع" (the reviewer/visitor), NEVER "المريض" (the patient)
 * - Include reviewer name when patientName is non-empty
 * - Always include file number (digit-converted for speech)
 * - Room: "الغرفة رقم [N]" when room contains a number
 * - Room: "الاستقبال" when room is missing/empty
 *
 * Privacy: Only safe fields are spoken (name, file number, room).
 * Never speak: phone, diagnosis, payment, balance, treatment notes.
 *
 * @param patientName  - The reviewer/patient display name (may be empty)
 * @param patientNumber - The file number string (e.g. "2020-622", "GM2026-0022")
 * @param roomName     - The room name (e.g. "غرفة 1", may be empty)
 * @returns The complete Arabic announcement text ready for SpeechSynthesis
 *
 * Examples:
 *   buildAnnouncementText("علي أحمد", "2020-622", "غرفة 1")
 *     → "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
 *
 *   buildAnnouncementText("علي أحمد", "GM2026-0022", "غرفة 1")
 *     → "المراجع علي أحمد، صاحب الملف رقم جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
 *
 *   buildAnnouncementText("", "2020-622", "غرفة 2")
 *     → "المراجع، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 2"
 *
 *   buildAnnouncementText("علي أحمد", "2020-622", "")
 *     → "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الاستقبال"
 */
export function buildAnnouncementText(
  patientName: string,
  patientNumber: string,
  roomName: string
): string {
  const reviewerName = patientName?.trim();
  const fileNumber =
    formatFileNumberForSpeech(patientNumber) ||
    patientNumber?.trim() ||
    "غير محدد";
  const destination = formatRoomForSpeech(roomName);
  const reviewerPart = reviewerName
    ? `المراجع ${reviewerName}`
    : "المراجع";

  return `${reviewerPart}، صاحب الملف رقم ${fileNumber}، يرجى التوجه إلى ${destination}`;
}
