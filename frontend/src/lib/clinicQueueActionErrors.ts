import { extractErrorMessage } from "@/lib/errors";
import { toast, useToastStore } from "@/stores/toastStore";

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
export const CLINIC_QUEUE_ACTION_NOTICE_DELAY_MS = 100;

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
  if (/\/recall(?:\?|$)/.test(url)) return "تعذر إعادة نداء المريض";
  if (/\/call(?:\?|$)/.test(url)) return "تعذر نداء المريض";
  if (/\/enter-room(?:\?|$)/.test(url)) return "تعذر إدخال المريض إلى الغرفة";
  if (/\/start(?:\?|$)/.test(url)) return "تعذر بدء زيارة المريض";
  if (/\/complete(?:\?|$)/.test(url)) return "تعذر إنهاء زيارة المريض";
  if (/\/cancel(?:\?|$)/.test(url)) return "تعذر إلغاء المريض من قائمة الانتظار";
  if (/\/no-show(?:\?|$)/.test(url)) return "تعذر تسجيل عدم حضور المريض";
  if (/\/priority(?:\?|$)/.test(url)) return "تعذر تغيير أولوية المريض";
  if (/\/notify(?:\?|$)/.test(url)) return "تعذر إرسال إشعار المريض";
  if (/\/room(?:\?|$)/.test(url)) return "تعذر تغيير غرفة المريض";

  return "تعذر تنفيذ الإجراء على قائمة الانتظار";
}

function getCallerHandledAliases(url: string): string[] {
  if (/\/recall(?:\?|$)/.test(url)) return ["فشل إعادة النداء"];
  if (/\/call(?:\?|$)/.test(url)) return ["فشل نداء المريض"];
  if (/\/enter-room(?:\?|$)/.test(url)) return ["فشل دخول الغرفة"];
  if (/\/complete(?:\?|$)/.test(url)) return ["فشل تنفيذ الإجراء", "فشل إنهاء الزيارة"];
  if (/\/cancel(?:\?|$)/.test(url)) return ["فشل تنفيذ الإجراء"];
  if (/\/room(?:\?|$)/.test(url)) return ["فشل تغيير الغرفة"];
  return [];
}

/**
 * Shows one truthful error toast for a failed clinic-queue mutation.
 *
 * The notification is delayed briefly so callers that already handle their own error
 * can render it first. If a matching new error toast appears during that window, the
 * shared fallback stays silent and avoids a duplicate message.
 */
export function notifyClinicQueueActionFailure(error: unknown): boolean {
  const fallback = getClinicQueueActionErrorFallback(error);
  if (!fallback || typeof window === "undefined") return false;

  const failure = error as RequestFailure;
  const url = failure.config?.url ?? "";
  const message = extractErrorMessage(error, fallback);
  const equivalentMessages = new Set([message, ...getCallerHandledAliases(url)]);
  const existingErrorIds = new Set(
    useToastStore.getState().toasts
      .filter((item) => item.type === "error")
      .map((item) => item.id),
  );

  window.setTimeout(() => {
    const callerAlreadyReported = useToastStore.getState().toasts.some(
      (item) =>
        item.type === "error" &&
        !existingErrorIds.has(item.id) &&
        equivalentMessages.has(item.description),
    );

    if (!callerAlreadyReported) toast.error(message);
  }, CLINIC_QUEUE_ACTION_NOTICE_DELAY_MS);

  return true;
}
