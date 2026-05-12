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

const DIGIT_NAMES = {
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
  "٩": "تسعة"
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

function pronounceDigits(numberPart) {
  return numberPart
    .split("")
    .map((digit) => DIGIT_NAMES[digit] ?? digit)
    .join(" ");
}

export function formatFileNumberForSpeech(patientNumber) {
  const trimmed = patientNumber?.trim();
  if (!trimmed) return "";

  const tokens = [];
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
    } else if (/\d|[٠-٩]/.test(char)) {
      currentNumber += char;
    } else if (char === "-" || char === "_" || char === "/" || char === " " || char === ".") {
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
