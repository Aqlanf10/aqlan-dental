import { api } from "./api";

/**
 * Downloads a PDF from the specified API endpoint and triggers a browser file download.
 * @param url The endpoint URL.
 * @param filename The default file name for the download.
 */
export async function downloadPdfFromApi(url: string, filename: string): Promise<void> {
  const { data } = await api.get(url, { responseType: "blob" });
  const blob = new Blob([data], { type: "application/pdf" });
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
 * @param url The endpoint URL.
 */
export async function openPdfFromApi(url: string): Promise<void> {
  const { data } = await api.get(url, { responseType: "blob" });
  const blob = new Blob([data], { type: "application/pdf" });
  const objectUrl = window.URL.createObjectURL(blob);
  window.open(objectUrl, "_blank");
}
