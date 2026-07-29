export const DOCTOR_ACCOUNT_NOT_LINKED_CODE = "DOCTOR_ACCOUNT_NOT_LINKED";

export function createDoctorAccountNotLinkedError(): Error {
  const error = new Error(DOCTOR_ACCOUNT_NOT_LINKED_CODE);
  error.name = "DoctorAccountNotLinkedError";
  return error;
}

export function isDoctorAccountNotLinkedError(error: unknown): boolean {
  return error instanceof Error && error.message === DOCTOR_ACCOUNT_NOT_LINKED_CODE;
}
