import api from './api';
import { useRouter } from 'next/navigation';

const GUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Check if a string is a valid GUID
 */
export function isGuid(value: string): boolean {
  return GUID_REGEX.test(value);
}

/**
 * Navigate to patient file, resolving patient number to GUID if needed
 */
export async function navigateToPatient(router: ReturnType<typeof useRouter>, patientIdOrNumber: string) {
  if (isGuid(patientIdOrNumber)) {
    router.push(`/patients/${patientIdOrNumber}`);
    return;
  }
  
  // Try to resolve patient number to GUID
  try {
    const { data } = await api.get(`/api/patients/by-number/${encodeURIComponent(patientIdOrNumber)}`);
    if (data?.id) {
      router.push(`/patients/${data.id}`);
    }
  } catch {
    // If resolution fails, navigate to the search page with the number as a query
    router.push(`/patients?search=${encodeURIComponent(patientIdOrNumber)}`);
  }
}
