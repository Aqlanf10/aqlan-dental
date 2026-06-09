import { api } from "./api";
import axios from "axios";

/**
 * Translates an HTTP status code into an Arabic user-facing reason string.
 */
function arabicHttpReason(status: number): string {
  if (status === 401) return "غير مصرح";
  if (status === 403) return "لا تملك صلاحية الوصول";
  if (status === 404) return "السجل غير موجود";
  if (status >= 500) return "خطأ في الخادم";
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
 * Throws an Error with an Arabic message if the request fails.
 * @param url The endpoint URL.
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
 * Fetches a PDF from the specified API endpoint and opens it in a new browser tab.
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
