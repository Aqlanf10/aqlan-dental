import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";

type RequestFailure = {
  code?: string;
  config?: {
    method?: string;
    url?: string;
    signal?: { aborted?: boolean };
  };
  response?: { status?: number };
};

const MUTATING_METHODS = new Set(["post", "patch", "put", "delete"]);

/**
 * Returns the Arabic fallback for a failed clinic-queue mutation.
 * Read requests, cancelled requests, and authentication failures are deliberately ignored.
 */
export function getClinicQueueActionErrorFallback(error: unknown): string | null {
  const failure = error as RequestFailure | null;
  const method = failure?.config?.method?.toLowerCase() ?? "";
  const url = failure?.config?.url ?? "";

  if (!MUTATING_METHODS.has(method)) return null;
  if (!url.includes("/api/clinic-queue/")) return null;
  if (failure?.code === "ERR_CANCELED" || failure?.config?.signal?.aborted) return null;
  if (failure?.response?.status === 401) return null;

  if (/\/reorder(?:\?|$)/.test(url)) return "تعذر تغيير ترتيب قائمة الانتظار";
  if (/\/(?:call|recall)(?:\?|$)/.test(url)) return "تعذر نداء المريض";
  if (/\/enter-room(?:\?|$)/.test(url)) return "تعذر إدخال المريض إلى الغرفة";
  if (/\/start(?:\?|$)/.test(url)) return "تعذر بدء زيارة المريض";
  if (/\/complete(?:\?|$)/.test(url)) return "تعذر إنهاء زيارة المريض";
  if (/\/cancel(?:\?|$)/.test(url)) return "تعذر إلغاء المريض من قائمة الانتظار";
  if (/\/no-show(?:\?|$)/.test(url)) return "تعذر تسجيل عدم حضور المريض";
  if (/\/priority(?:\?|$)/.test(url)) return "تعذر تغيير أولوية المريض";
  if (/\/notify(?:\?|$)/.test(url)) return "تعذر إرسال إشعار المريض";

  return "تعذر تنفيذ الإجراء على قائمة الانتظار";
}

/** Shows one truthful error toast for a failed clinic-queue mutation. */
export function notifyClinicQueueActionFailure(error: unknown): boolean {
  const fallback = getClinicQueueActionErrorFallback(error);
  if (!fallback) return false;

  toast.error(extractErrorMessage(error, fallback));
  return true;
}
