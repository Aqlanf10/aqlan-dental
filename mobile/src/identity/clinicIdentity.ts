import { requestJson } from '@/api/client';
import type { Locale } from '@/i18n';

/**
 * The clinic's own identity, as the rest of the system already defines it.
 *
 * The owner's rule for this system is that identity lives in Settings and is never written
 * into code: the centre's name, the lead doctor's name and qualification, the address and the
 * phone all come from `clinic.*` and `website.*` rows, and every receipt, statement and lab
 * order reads them through one resolver. The phone app was the exception — it carried the same
 * strings baked into its translation bundles, so editing the clinic's phone number in Settings
 * changed the website and every PDF while the staff app kept showing the old one until somebody
 * shipped a new APK.
 *
 * `GET /api/public/website-settings` is the contract that already serves this, and it is the
 * right one for three reasons: it is anonymous, so the sign-in screen can show the centre's
 * identity before anyone has logged in; `/api/settings` is Admin-only, so a doctor or a
 * receptionist could never have read it; and its Arabic lead-doctor block is composed by
 * `FinanceClinicIdentity`, the same resolver the invoices use, so the app cannot end up naming
 * the doctor differently from the paperwork.
 */
export type ClinicIdentity = {
  clinicName: string;
  address: string;
  phone: string;
  workingHours: string;
  leadDoctor: string;
  leadDoctorCredentials: string;
};

function text(source: Record<string, unknown>, key: string): string {
  const value = source[key];
  return typeof value === 'string' ? value.trim() : '';
}

/**
 * Picks the Arabic or English identity for the reader's language, falling back to the other
 * one rather than to nothing: a clinic that has filled in only Arabic must still show a name
 * to an English reader.
 */
export function selectIdentity(payload: unknown, locale: Locale): ClinicIdentity | null {
  if (!payload || typeof payload !== 'object') return null;
  const source = payload as Record<string, unknown>;

  const ar = {
    clinicName: text(source, 'clinicName'),
    address: text(source, 'address'),
    leadDoctor: text(source, 'leadDoctorAr'),
    leadDoctorCredentials: text(source, 'leadDoctorCredentialsAr'),
  };
  const en = {
    clinicName: text(source, 'clinicNameEn'),
    address: text(source, 'addressEn'),
    leadDoctor: text(source, 'leadDoctorEn'),
    leadDoctorCredentials: text(source, 'leadDoctorCredentialsEn'),
  };
  const preferred = locale === 'en' ? en : ar;
  const other = locale === 'en' ? ar : en;
  const pick = (field: keyof typeof ar) => preferred[field] || other[field];

  const clinicName = pick('clinicName');
  // A response with no name at all is not identity; the caller keeps its bundled fallback
  // rather than rendering a header with a hole where the centre's name should be.
  if (!clinicName) return null;

  return {
    clinicName,
    address: pick('address'),
    // Neither the phone nor the working hours is a translated row — one number, one schedule.
    phone: text(source, 'phone'),
    workingHours: text(source, 'workingHours'),
    leadDoctor: pick('leadDoctor'),
    leadDoctorCredentials: pick('leadDoctorCredentials'),
  };
}

export async function fetchClinicIdentityPayload(): Promise<unknown> {
  return requestJson('/api/public/website-settings', { method: 'GET' });
}
