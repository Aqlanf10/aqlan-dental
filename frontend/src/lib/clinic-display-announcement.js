export const RECEPTION_FALLBACK = "الاستقبال";

/**
 * Format a room name for Arabic speech synthesis.
 *
 * If the roomName contains a number (Arabic or Western digits),
 * extract it and return "الغرفة رقم [number]" for clearer speech.
 * If the roomName contains no number, return it as-is.
 * If roomName is empty/missing, return "الاستقبال".
 *
 * @param {string | null | undefined} roomName
 * @returns {string}
 */
export function formatRoomForSpeech(roomName) {
  const trimmed = roomName?.trim();
  if (!trimmed) return RECEPTION_FALLBACK;

  // Match Western digits (0-9) or Arabic-Indic digits (٠-٩)
  const numberMatch = trimmed.match(/\d+|[٠-٩]+/);
  if (numberMatch) {
    return `الغرفة رقم ${numberMatch[0]}`;
  }

  return trimmed;
}

/**
 * Build the privacy-safe Arabic announcement text for a patient.
 *
 * Allowed spoken fields: patient name, file number, room number.
 * Forbidden spoken fields: phone, diagnosis, payment, balance, treatment notes,
 * medical history, private notes.
 *
 * @param {string} patientName
 * @param {string} patientNumber
 * @param {string} roomName
 * @returns {string}
 */
export function buildAnnouncementText(patientName, patientNumber, roomName) {
  const hasName = patientName?.trim().length > 0;
  const fileNumber = patientNumber?.trim();
  const destination = formatRoomForSpeech(roomName);

  if (hasName) {
    const filePart = fileNumber ? `، رقم الملف ${fileNumber}` : "";
    return `المريض ${patientName.trim()}${filePart}، يرجى التوجه إلى ${destination}`;
  }

  return `صاحب الملف رقم ${fileNumber || patientNumber}، يرجى التوجه إلى ${destination}`;
}
