import { api } from "./api";
import axios from "axios";

/**
 * Translates an HTTP status code into an Arabic user-facing reason string.
 */
function arabicHttpReason(status: number): string {
  if (status === 401) return "غير مصرح — يرجى تسجيل الدخول من جديد";
  if (status === 403) return "لا تملك صلاحية الوصول إلى هذا المستند";
  if (status === 404) return "السجل غير موجود";
  if (status >= 500) return "خطأ في الخادم — يرجى المحاولة لاحقاً";
  return `خطأ (${status})`;
}

/**
 * Extracts a human-readable Arabic error message from an Axios error whose
 * response may be a Blob (because we requested responseType: "blob").
 * Tries to read the Blob as JSON first, falls back to HTTP status, then generic.
 */
export async function extractPdfError(err: unknown): Promise<string> {
  if (axios.isAxiosError(err)) {
    const status = err.response?.status;
    const data = err.response?.data;

    // When responseType is "blob", the error body arrives as a Blob.
    // We try to parse it as JSON to get the backend { message } field.
    if (data instanceof Blob) {
      try {
        const text = await data.text();
        const json = JSON.parse(text) as { message?: string };
        if (json?.message) return json.message;
      } catch {
        // Ignore — fall through to status-based message
      }
    }

    // Plain JSON error (fallback)
    if (typeof data === "object" && data !== null) {
      const msg = (data as Record<string, unknown>).message;
      if (typeof msg === "string" && msg) return msg;
    }

    if (status) return arabicHttpReason(status);
  }
  return "خطأ غير متوقع";
}

/**
 * Downloads a PDF from the specified API endpoint and triggers a browser file download.
 * Sends Authorization token via the api axios instance interceptor.
 * Throws an Error with an Arabic message if the request fails.
 * @param url The endpoint URL (e.g. `/api/payments/{id}/pdf`).
 * @param filename The default file name for the download.
 */
export async function downloadPdfFromApi(url: string, filename: string): Promise<void> {
  let response;
  try {
    response = await api.get(url, { responseType: "blob" });
  } catch (err) {
    const reason = await extractPdfError(err);
    throw new Error(reason);
  }

  const blob = new Blob([response.data], { type: "application/pdf" });
  const objectUrl = window.URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = objectUrl;
  link.download = filename;
  document.body.appendChild(link);
  link.click();

  document.body.removeChild(link);
  window.URL.revokeObjectURL(objectUrl);
}

/**
 * Fetches a PDF from the specified API endpoint with Authorization token,
 * then opens it in a new browser tab for printing.
 *
 * IMPORTANT: This does NOT use window.print() on the system page.
 * Instead it opens the PDF itself in a dedicated tab so only the
 * PDF content is printed, never the dashboard UI.
 *
 * If the browser blocks the print window/tab, the user is shown
 * a clear Arabic message explaining what to do.
 *
 * @param url The endpoint URL (e.g. `/api/payments/{id}/pdf`).
 * @param filename Fallback filename used if user wants to save instead.
 */
export async function printPdfFromApi(url: string, filename: string): Promise<void> {
  let response;
  try {
    response = await api.get(url, { responseType: "blob" });
  } catch (err) {
    const reason = await extractPdfError(err);
    throw new Error(reason);
  }

  const blob = new Blob([response.data], { type: "application/pdf" });
  const objectUrl = window.URL.createObjectURL(blob);

  // Try to open the PDF in a new window and trigger print.
  // This prints the PDF document itself, NOT the system dashboard.
  const printWindow = window.open(objectUrl, "_blank");

  if (!printWindow) {
    // Browser blocked the popup — show Arabic message to user
    window.URL.revokeObjectURL(objectUrl);
    throw new Error(
      "تم حظر فتح نافذة الطباعة من قبل المتصفح. يرجى السماح بالنوافذ المنبثقة لهذا الموقع ثم المحاولة مرة أخرى، أو استخدم زر التحميل ثم اطبع الملف يدوياً."
    );
  }

  // Wait for the PDF to load, then try to trigger print dialog.
  // Some browsers' built-in PDF viewers support window.print(),
  // which will only print the PDF content, not the system page.
  printWindow.addEventListener("load", () => {
    try {
      printWindow.print();
    } catch {
      // If print() fails (e.g. cross-origin PDF viewer), the user
      // can still use Ctrl+P in the opened PDF tab.
    }
  });

  // Clean up the object URL after a delay to allow the browser to load it.
  // We don't revoke immediately because the PDF viewer needs the URL.
  setTimeout(() => {
    try {
      window.URL.revokeObjectURL(objectUrl);
    } catch {
      // Ignore — URL may already be revoked or still in use
    }
  }, 60_000); // 60 seconds — enough time for viewing/printing
}

/**
 * Opens a PDF from the specified API endpoint in a new browser tab.
 * Sends Authorization token via the api axios instance interceptor.
 * Throws an Error with an Arabic message if the request fails.
 * @param url The endpoint URL.
 */
export async function openPdfFromApi(url: string): Promise<void> {
  let response;
  try {
    response = await api.get(url, { responseType: "blob" });
  } catch (err) {
    const reason = await extractPdfError(err);
    throw new Error(reason);
  }

  const blob = new Blob([response.data], { type: "application/pdf" });
  const objectUrl = window.URL.createObjectURL(blob);
  window.open(objectUrl, "_blank");
}
