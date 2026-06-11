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
 * Fetches a PDF blob from the specified API endpoint with Authorization token.
 * Internal helper used by both downloadPdfFromApi and printPdfFromApi.
 * @param url The endpoint URL (e.g. `/api/payments/{id}/pdf`).
 * @returns A Blob of type application/pdf.
 * @throws Error with Arabic message if the request fails.
 */
async function fetchPdfBlob(url: string): Promise<Blob> {
  let response;
  try {
    response = await api.get(url, { responseType: "blob" });
  } catch (err) {
    const reason = await extractPdfError(err);
    throw new Error(reason);
  }
  return new Blob([response.data], { type: "application/pdf" });
}

/**
 * Downloads a PDF from the specified API endpoint and triggers a browser file download.
 * Sends Authorization token via the api axios instance interceptor.
 *
 * STRICT BEHAVIOR — DOWNLOAD ONLY:
 * - Creates a temporary `<a>` element with `download` attribute and clicks it.
 * - Does NOT call window.open.
 * - Does NOT call .print().
 * - Does NOT invoke any print-related function.
 * - Revokes the object URL immediately after triggering the download.
 *
 * Throws an Error with an Arabic message if the request fails.
 * @param url The endpoint URL (e.g. `/api/payments/{id}/pdf`).
 * @param filename The default file name for the download.
 */
export async function downloadPdfFromApi(url: string, filename: string): Promise<void> {
  const blob = await fetchPdfBlob(url);
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
 * then opens an intermediate HTML window containing an iframe to trigger
 * the browser's print dialog for the PDF content only.
 *
 * STRICT BEHAVIOR — PRINT ONLY:
 * - Opens an intermediate HTML window immediately (before any await) to
 *   avoid popup blockers.
 * - Shows "جاري تجهيز المستند للطباعة..." while the PDF loads.
 * - Loads the PDF blob inside an iframe within the print window.
 * - Calls iframe.contentWindow.print() after the iframe loads.
 * - Does NOT call downloadPdfFromApi or trigger file download.
 * - Does NOT use window.open(objectUrl) directly as the primary print method.
 * - Does NOT use window.print() on the system/dashboard page.
 *
 * If the browser blocks the print window, throws an Error with an Arabic message.
 * If automatic print fails, shows fallback buttons inside the print window:
 *   - "طباعة" button to retry
 *   - "فتح ملف PDF" link to open the PDF
 *   - "تحميل PDF" link to download the PDF
 *
 * @param url The endpoint URL (e.g. `/api/payments/{id}/pdf`).
 * @param filename Fallback filename used if user wants to save instead.
 */
export async function printPdfFromApi(url: string, filename: string): Promise<void> {
  // Open the print window IMMEDIATELY (before any async work) so the browser
  // associates it with the user gesture and doesn't block it as a popup.
  const printWindow = window.open("", "_blank", "noopener,noreferrer");

  if (!printWindow) {
    throw new Error(
      "تم حظر نافذة الطباعة من قبل المتصفح. يرجى السماح بالنوافذ المنبثقة لهذا الموقع ثم المحاولة مرة أخرى."
    );
  }

  // Write intermediate HTML with status message into the print window.
  printWindow.document.open();
  printWindow.document.write(`<!doctype html>
<html lang="ar" dir="rtl">
  <head>
    <meta charset="utf-8" />
    <title>طباعة المستند</title>
    <style>
      html, body {
        margin: 0;
        padding: 0;
        width: 100%;
        height: 100%;
        font-family: Arial, sans-serif;
        direction: rtl;
      }
      #status {
        padding: 24px;
        text-align: center;
        font-size: 16px;
        color: #555;
      }
      iframe {
        width: 100vw;
        height: 100vh;
        border: 0;
        position: absolute;
        top: 0;
        left: 0;
      }
      .actions {
        padding: 16px;
        text-align: center;
        background: #f9fafb;
        border-bottom: 1px solid #e5e7eb;
      }
      .actions p {
        margin: 0 0 10px 0;
        font-size: 14px;
        color: #666;
      }
      .actions button, .actions a {
        margin: 4px;
        padding: 8px 16px;
        border-radius: 8px;
        border: 1px solid #ddd;
        background: #fff;
        cursor: pointer;
        text-decoration: none;
        color: #111;
        font-size: 14px;
        display: inline-block;
        transition: background 0.15s;
      }
      .actions button:hover, .actions a:hover {
        background: #f0f0f0;
      }
      @media print {
        .actions { display: none !important; }
        #status { display: none !important; }
      }
    </style>
  </head>
  <body>
    <div id="status">جاري تجهيز المستند للطباعة...</div>
  </body>
</html>`);
  printWindow.document.close();

  // Now fetch the PDF blob (async — after popup is already open).
  let blob: Blob;
  try {
    blob = await fetchPdfBlob(url);
  } catch (err) {
    const reason = err instanceof Error ? err.message : "خطأ غير متوقع";
    // Show error in the already-open print window.
    try {
      printWindow.document.body.innerHTML =
        `<div id="status" style="padding:24px;text-align:center;color:#dc2626;font-size:16px;">` +
        `فشل تجهيز المستند للطباعة: ${reason}</div>`;
    } catch {
      // Window may have been closed by the user already.
    }
    throw new Error(reason);
  }

  const objectUrl = window.URL.createObjectURL(blob);
  const safeFilename = filename.replace(/"/g, "");
  const doc = printWindow.document;

  // Replace the status message with an iframe + fallback actions.
  doc.body.innerHTML =
    `<iframe id="pdf-frame" src="${objectUrl}"></iframe>` +
    `<div class="actions" id="fallback-actions" style="display:none;">` +
    `<p>إذا لم تظهر نافذة الطباعة تلقائياً، استخدم أحد الخيارات التالية:</p>` +
    `<button id="manual-print">طباعة</button>` +
    `<a id="open-pdf" href="${objectUrl}" target="_blank" rel="noopener">فتح ملف PDF</a>` +
    `<a id="download-pdf" href="${objectUrl}" download="${safeFilename}">تحميل PDF</a>` +
    `</div>`;

  const iframe = doc.getElementById("pdf-frame") as HTMLIFrameElement | null;
  const fallback = doc.getElementById("fallback-actions") as HTMLElement | null;
  const manualPrint = doc.getElementById("manual-print") as HTMLButtonElement | null;

  const showFallback = () => {
    if (fallback) fallback.style.display = "block";
  };

  const runPrint = () => {
    try {
      iframe?.contentWindow?.focus();
      iframe?.contentWindow?.print();
      // Show fallback buttons after a delay in case the user dismissed the print dialog.
      setTimeout(showFallback, 1500);
    } catch {
      showFallback();
    }
  };

  // When the iframe (PDF) finishes loading, trigger the print dialog.
  iframe?.addEventListener("load", () => {
    // Small delay to allow the browser's PDF viewer to fully initialize.
    setTimeout(runPrint, 500);
  });

  // Manual print button as a fallback.
  manualPrint?.addEventListener("click", runPrint);

  // Show fallback actions after 3 seconds if print hasn't triggered or user needs options.
  setTimeout(showFallback, 3000);

  // Clean up the object URL after a generous delay.
  setTimeout(() => {
    try {
      window.URL.revokeObjectURL(objectUrl);
    } catch {
      // Ignore — URL may already be revoked.
    }
  }, 120_000); // 2 minutes
}

/**
 * Opens a PDF from the specified API endpoint in a new browser tab.
 * Sends Authorization token via the api axios instance interceptor.
 * Throws an Error with an Arabic message if the request fails.
 * @param url The endpoint URL.
 */
export async function openPdfFromApi(url: string): Promise<void> {
  const blob = await fetchPdfBlob(url);
  const objectUrl = window.URL.createObjectURL(blob);
  window.open(objectUrl, "_blank");

  // Clean up the object URL after a delay to allow the browser to load it.
  setTimeout(() => {
    try {
      window.URL.revokeObjectURL(objectUrl);
    } catch {
      // Ignore — URL may already be revoked or still in use
    }
  }, 60_000);
}
