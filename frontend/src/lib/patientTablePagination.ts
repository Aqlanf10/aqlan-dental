export type PatientPaginationItem = number | "ellipsis-left" | "ellipsis-right";

export function getPatientPaginationItems(
  totalPages: number,
  currentPage: number,
): PatientPaginationItem[] {
  const total = Math.max(1, Math.floor(totalPages));
  const current = Math.min(total, Math.max(1, Math.floor(currentPage)));

  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  if (current <= 4) {
    return [1, 2, 3, 4, 5, "ellipsis-right", total];
  }

  if (current >= total - 3) {
    return [1, "ellipsis-left", total - 4, total - 3, total - 2, total - 1, total];
  }

  return [1, "ellipsis-left", current - 1, current, current + 1, "ellipsis-right", total];
}
