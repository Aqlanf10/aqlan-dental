import api from "@/lib/api";

export interface DailyOpsBookingConversionInput {
  id: string;
  doctorId: string | null;
  preferredDate: string | null;
  preferredTime: string | null;
  serviceType: string | null;
  convertedToAppointmentId: string | null;
}

export interface DailyOpsBookingConversionResult {
  statusConfirmed: boolean;
  appointmentCreated: boolean;
  reason?: string;
}

function normalizeDateForApi(value: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed.toISOString().slice(0, 10);
}

function normalizeTimeForApi(value: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  const match = trimmed.match(/^(\d{1,2}):(\d{2})/);
  if (!match) return null;
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;
  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

function addMinutes(time: string, minutesToAdd: number): string {
  const [hour, minute] = time.split(":").map(Number);
  const total = hour * 60 + minute + minutesToAdd;
  return `${String(Math.floor(total / 60) % 24).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}

export function canConvertBookingToAppointment(item: DailyOpsBookingConversionInput): boolean {
  return Boolean(
    !item.convertedToAppointmentId &&
    item.doctorId &&
    normalizeDateForApi(item.preferredDate) &&
    normalizeTimeForApi(item.preferredTime)
  );
}

export async function confirmBookingAndCreateAppointment(
  item: DailyOpsBookingConversionInput,
  staffNotes?: string,
): Promise<DailyOpsBookingConversionResult> {
  await api.patch(`/api/booking-requests/${item.id}/status`, {
    status: "Confirmed",
    staffNotes,
  });

  if (item.convertedToAppointmentId) {
    return { statusConfirmed: true, appointmentCreated: false, reason: "already_converted" };
  }

  const appointmentDate = normalizeDateForApi(item.preferredDate);
  const startTime = normalizeTimeForApi(item.preferredTime);

  if (!item.doctorId || !appointmentDate || !startTime) {
    return { statusConfirmed: true, appointmentCreated: false, reason: "missing_doctor_date_or_time" };
  }

  // Build the payload — only include patientId if we have a real one.
  // The backend ConvertToAppointmentAsync handles patient lookup/creation by phone,
  // so omitting patientId is safe and preferred over sending a placeholder.
  // TODO: After calling this, invalidate:
  //   - queryClient.invalidateQueries({ queryKey: ["daily-ops"] })
  //   - queryClient.invalidateQueries({ queryKey: ["patient-journey"] })
  //   - queryClient.invalidateQueries({ queryKey: ["appointments"] })
  //   - queryClient.invalidateQueries({ queryKey: ["booking-requests"] })
  const payload: Record<string, unknown> = {
    doctorId: item.doctorId,
    appointmentDate,
    startTime,
    endTime: addMinutes(startTime, 30),
    appointmentType: item.serviceType || "Consultation",
    durationMinutes: 30,
  };

  await api.post(`/api/booking-requests/${item.id}/convert-to-appointment`, payload);

  return { statusConfirmed: true, appointmentCreated: true };
}

/**
 * V2 conversion: tries the dedicated confirm-and-create-appointment endpoint first,
 * falls back to the legacy convert-to-appointment on 404.
 *
 * Steps:
 *  1. PATCH status → "Confirmed"
 *  2. POST /api/booking-requests/{id}/confirm-and-create-appointment
 *  3. If 404 → POST /api/booking-requests/{id}/convert-to-appointment (legacy)
 *
 * TODO: After calling this, invalidate:
 *   - queryClient.invalidateQueries({ queryKey: ["daily-ops"] })
 *   - queryClient.invalidateQueries({ queryKey: ["patient-journey"] })
 *   - queryClient.invalidateQueries({ queryKey: ["appointments"] })
 *   - queryClient.invalidateQueries({ queryKey: ["booking-requests"] })
 */
export async function confirmBookingAndCreateAppointmentV2(
  item: DailyOpsBookingConversionInput,
  staffNotes?: string,
): Promise<DailyOpsBookingConversionResult> {
  // Step 1: Confirm booking status
  await api.patch(`/api/booking-requests/${item.id}/status`, {
    status: "Confirmed",
    staffNotes,
  });

  if (item.convertedToAppointmentId) {
    return { statusConfirmed: true, appointmentCreated: false, reason: "already_converted" };
  }

  const appointmentDate = normalizeDateForApi(item.preferredDate);
  const startTime = normalizeTimeForApi(item.preferredTime);

  if (!item.doctorId || !appointmentDate || !startTime) {
    return { statusConfirmed: true, appointmentCreated: false, reason: "missing_doctor_date_or_time" };
  }

  const payload = {
    doctorId: item.doctorId,
    appointmentDate,
    startTime,
    endTime: addMinutes(startTime, 30),
    appointmentType: item.serviceType || "Consultation",
    durationMinutes: 30,
  };

  // Step 2: Try the new dedicated endpoint
  try {
    await api.post(`/api/booking-requests/${item.id}/confirm-and-create-appointment`, payload);
    return { statusConfirmed: true, appointmentCreated: true };
  } catch (err: unknown) {
    // Fall back to legacy endpoint if the new one is not implemented (404)
    const axiosErr = err as { response?: { status?: number } };
    if (axiosErr?.response?.status === 404) {
      await api.post(`/api/booking-requests/${item.id}/convert-to-appointment`, payload);
      return { statusConfirmed: true, appointmentCreated: true };
    }
    throw err;
  }
}
