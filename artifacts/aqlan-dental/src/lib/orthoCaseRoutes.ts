export function patientActiveOrthoCasesUrl(
  patientId: string,
  pageSize = 100,
): string {
  const params = new URLSearchParams({
    patientId,
    status: "active",
    page: "1",
    pageSize: String(pageSize),
  });
  return `/api/ortho-cases?${params.toString()}`;
}
