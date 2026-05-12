export const RECEPTION_FALLBACK = "الاستقبال";

const LETTER_NAMES = {
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
  Z: "زد"
};

const ZERO_NAMES = {
  "0": "صفر",
  "٠": "صفر"
};

export function formatRoomForSpeech(roomName) {
  const trimmed = roomName?.trim();
  if (!trimmed) return RECEPTION_FALLBACK;

  const numberMatch = trimmed.match(/\d+|[٠-٩]+/);
  if (numberMatch) {
    return `الغرفة رقم ${numberMatch[0]}`;
  }

  return trimmed;
}

function pronounceLeadingZeros(numberPart) {
  if (!numberPart || numberPart.length <= 1) return numberPart;

  let index = 0;
  const parts = [];
  while (index < numberPart.length - 1 && (numberPart[index] === "0" || numberPart[index] === "٠")) {
    parts.push(ZERO_NAMES[numberPart[index]] ?? "صفر");
    index += 1;
  }

  const rest = numberPart.slice(index);
  if (rest) parts.push(rest);
  return parts.join(" ");
}

export function formatFileNumberForSpeech(patientNumber) {
  const trimmed = patientNumber?.trim();
  if (!trimmed) return "";

  const tokens = [];
  let currentNumber = "";

  const flushNumber = () => {
    if (currentNumber) {
      tokens.push(pronounceLeadingZeros(currentNumber));
      currentNumber = "";
    }
  };

  for (const char of trimmed) {
    const upper = char.toUpperCase();
    if (LETTER_NAMES[upper]) {
      flushNumber();
      tokens.push(LETTER_NAMES[upper]);
    } else if (/\d|[٠-٩]/.test(char)) {
      currentNumber += char;
    } else if (char === "-" || char === "_" || char === "/" || char === " ") {
      flushNumber();
    } else {
      flushNumber();
      tokens.push(char);
    }
  }

  flushNumber();
  return tokens.filter(Boolean).join(" ").replace(/\s+/g, " ").trim();
}

export function buildAnnouncementText(patientName, patientNumber, roomName) {
  const reviewerName = patientName?.trim();
  const fileNumber = formatFileNumberForSpeech(patientNumber) || patientNumber?.trim() || "غير محدد";
  const destination = formatRoomForSpeech(roomName);
  const reviewerPart = reviewerName ? `المراجع ${reviewerName}` : "المراجع";

  return `${reviewerPart}، صاحب الملف رقم ${fileNumber}، يرجى التوجه إلى ${destination}`;
}
