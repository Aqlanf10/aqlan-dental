/**
 * Extracts a user-friendly error message from an API error response.
 * Handles axios error response structure with Arabic fallback messages.
 */
export function extractErrorMessage(err: unknown, fallback = "حدث خطأ"): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string; detail?: string; title?: string }; status?: number } }).response;
    if (resp?.data?.message) return resp.data.message;
    if (resp?.data?.detail) return resp.data.detail;
    if (resp?.data?.title) return resp.data.title;
    if (resp?.status === 401) return "ليس لديك صلاحية. يرجى تسجيل الدخول مجدداً.";
    if (resp?.status === 403) return "غير مصرح بهذا الإجراء.";
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
