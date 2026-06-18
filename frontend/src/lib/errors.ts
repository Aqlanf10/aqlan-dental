/**
 * Extracts a user-friendly error message from an API error response.
 * Handles axios error response structure with Arabic fallback messages.
 *
 * FE-11: This is the single source of truth for error-message extraction. It handles:
 *   - response.data.message (ASP.NET ProblemDetails)
 *   - response.data.detail / response.data.title (RFC 7807)
 *   - response.data.errors[0] (ASP.NET validation problem-details — an array/object of
 *     field-validation errors; we take the first one)
 *   - response.data as a string (raw error body)
 *   - 401/403 status codes with Arabic messages
 *   - plain Error instances
 * Pages should NOT re-declare getApiErrorMessage locally — import this instead.
 */
export function extractErrorMessage(err: unknown, fallback = "حدث خطأ"): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string; detail?: string; title?: string; errors?: string[] | Record<string, string[]>; status?: number }; status?: number } }).response;
    if (resp?.data?.message) return resp.data.message;
    if (resp?.data?.detail) return resp.data.detail;
    if (resp?.data?.title) return resp.data.title;
    // ASP.NET validation problem-details: errors can be an array of strings OR a record
    // of { fieldName: ["error1", "error2"] }. Take the first available message.
    if (resp?.data?.errors) {
      const errors = resp.data.errors;
      if (Array.isArray(errors) && errors.length > 0) return errors[0];
      if (typeof errors === "object") {
        const firstArr = Object.values(errors).find(v => Array.isArray(v) && v.length > 0) as string[] | undefined;
        if (firstArr && firstArr[0]) return firstArr[0];
      }
    }
    if (resp?.status === 401) return "ليس لديك صلاحية. يرجى تسجيل الدخول مجدداً.";
    if (resp?.status === 403) return "غير مصرح بهذا الإجراء.";
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
