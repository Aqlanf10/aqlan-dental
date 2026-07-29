/**
 * YOLO-S5: Patient segment types — mirrors the backend DTOs returned by
 * PatientSegmentsController.
 */

export interface PatientSegmentDto {
  id: string;
  /** Stable key — `builtin:*` for pre-built dynamic segments, GUID string for custom. */
  key: string;
  name: string;
  description?: string | null;
  color?: string | null;
  isDynamic: boolean;
  isBuiltIn: boolean;
  memberCount: number;
  createdAt: string;
}

export interface PatientSegmentMemberDto {
  id: string;
  patientId: string;
  patientNumber: string;
  fullName: string;
  phone?: string | null;
  addedAt: string;
  /** Context for built-in dynamic segments (e.g. overdue amount, days since visit). */
  reason?: string | null;
}

export interface PatientSegmentsListResponse {
  builtIn: PatientSegmentDto[];
  custom: PatientSegmentDto[];
}

export interface CreatePatientSegmentRequest {
  name: string;
  description?: string;
  color?: string;
}
