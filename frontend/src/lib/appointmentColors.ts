/**
 * YOLO-S1: Default suggested colors for appointment types on the calendar.
 *
 * These are SUGGESTIONS surfaced in the appointment form's color picker —
 * NOT enforced anywhere in business logic. The doctor/reception can pick any
 * hex color via the color input, or leave AppointmentColor null to fall back
 * to the doctor's color (existing behavior, unchanged).
 *
 * Per CLAUDE.md "no hardcoding in business logic" — these defaults live in a
 * single front-end constant so they can be tuned without touching logic. They
 * are NOT stored in the Settings table because they are display hints, not
 * operational rules. If the owner wants to customize them later, the natural
 * extension is a Settings key `appointment.default_colors` (JSON).
 */

export interface AppointmentTypeColorSuggestion {
  /** Arabic label shown next to the swatch. */
  label: string;
  /** Hex color, e.g. "#3b82f6". */
  color: string;
}

export const APPOINTMENT_COLOR_SUGGESTIONS: readonly AppointmentTypeColorSuggestion[] = [
  { label: "تقويم",   color: "#8b5cf6" }, // purple
  { label: "كشف",     color: "#3b82f6" }, // blue
  { label: "متابعة",  color: "#10b981" }, // green
  { label: "إجراء",   color: "#f59e0b" }, // amber
  { label: "طارئ",    color: "#ef4444" }, // red
];

/**
 * Returns the appointment color to render on the calendar/day-schedule.
 * Priority:
 *   1. Appointment.AppointmentColor (explicit pick by the doctor/reception)
 *   2. Appointment.PackageColor (color inherited from the linked treatment package)
 *   3. Appointment.DoctorColor (existing behavior — falls back to the doctor's color)
 *   4. null (caller falls back to status color or a default)
 *
 * Centralizing this here keeps the priority rule consistent between DaySchedule
 * and WeekCalendar and makes it easy to test.
 */
export function resolveAppointmentColor(
  appointmentColor?: string | null,
  packageColor?: string | null,
  doctorColor?: string | null,
): string | null {
  if (appointmentColor && appointmentColor.trim() !== "") return appointmentColor;
  if (packageColor && packageColor.trim() !== "") return packageColor;
  if (doctorColor && doctorColor.trim() !== "") return doctorColor;
  return null;
}
