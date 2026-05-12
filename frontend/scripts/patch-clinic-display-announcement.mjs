import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const frontendRoot = join(__dirname, "..");
const pagePath = join(frontendRoot, "src/app/clinic-display/page.tsx");

const source = readFileSync(pagePath, "utf8");
const startMarker = "function buildAnnouncementText(patientName: string, patientNumber: string, roomName: string): string {";
const endMarker = "\n\n/* ─── Arabic Speech Utility";

const start = source.indexOf(startMarker);
const end = source.indexOf(endMarker, start);

if (start === -1 || end === -1) {
  throw new Error("Could not locate clinic display announcement function to patch.");
}

const replacement = `const FILE_LETTER_NAMES: Record<string, string> = {
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

const FILE_ZERO_NAMES: Record<string, string> = {
  "0": "صفر",
  "٠": "صفر",
};

function pronounceLeadingZeros(numberPart: string): string {
  if (!numberPart || numberPart.length <= 1) return numberPart;

  let index = 0;
  const parts: string[] = [];
  while (index < numberPart.length - 1 && (numberPart[index] === "0" || numberPart[index] === "٠")) {
    parts.push(FILE_ZERO_NAMES[numberPart[index]] ?? "صفر");
    index += 1;
  }

  const rest = numberPart.slice(index);
  if (rest) parts.push(rest);
  return parts.join(" ");
}

function formatFileNumberForSpeech(patientNumber?: string | null): string {
  const trimmed = patientNumber?.trim();
  if (!trimmed) return "";

  const tokens: string[] = [];
  let currentNumber = "";

  const flushNumber = () => {
    if (currentNumber) {
      tokens.push(pronounceLeadingZeros(currentNumber));
      currentNumber = "";
    }
  };

  for (const char of trimmed) {
    const upper = char.toUpperCase();
    if (FILE_LETTER_NAMES[upper]) {
      flushNumber();
      tokens.push(FILE_LETTER_NAMES[upper]);
    } else if (/\\d|[٠-٩]/.test(char)) {
      currentNumber += char;
    } else if (char === "-" || char === "_" || char === "/" || char === " ") {
      flushNumber();
    } else {
      flushNumber();
      tokens.push(char);
    }
  }

  flushNumber();
  return tokens.filter(Boolean).join(" ").replace(/\\s+/g, " ").trim();
}

function buildAnnouncementText(patientName: string, patientNumber: string, roomName: string): string {
  const reviewerName = patientName?.trim();
  const fileNumber = formatFileNumberForSpeech(patientNumber) || patientNumber?.trim() || "غير محدد";
  const destination = formatRoomForSpeech(roomName);
  const reviewerPart = reviewerName ? \`المراجع \${reviewerName}\` : "المراجع";

  return \`\${reviewerPart}، صاحب الملف رقم \${fileNumber}، يرجى التوجه إلى \${destination}\`;
}`;

const nextSource = source.slice(0, start) + replacement + source.slice(end);
writeFileSync(pagePath, nextSource, "utf8");

console.log("Clinic display announcement patch applied.");
