/**
 * FE-27: Centralized print/PDF utilities.
 *
 * The codebase had 9 window.print() calls — some for receipts (should use backend
 * PDF via printPdfFromApi), some for on-screen reports (no backend PDF available).
 * This module provides:
 *
 * 1. printReceipt(url, filename) — for receipts/invoices/lab orders that HAVE a
 *    backend PDF endpoint. Uses printPdfFromApi (opens print window with PDF).
 *
 * 2. printScreen() — for on-screen reports that DON'T have a backend PDF (ceph
 *    compare, VTO, day schedule, daily-ops summary, patient-journey summary).
 *    Uses window.print() but with a print-only CSS class to hide dashboard chrome.
 *
 * 3. PRINT_ONLY_CLASS — CSS class to add to elements that should only appear in print.
 *
 * Usage:
 *   import { printReceipt, printScreen } from "@/lib/printUtils";
 *   <button onClick={() => printReceipt(`/api/payments/${id}/pdf`, `receipt-${id}`)}>طباعة</button>
 *   <button onClick={printScreen}>طباعة الصفحة</button>
 */

import { printPdfFromApi } from "@/lib/pdfDownload";

/**
 * Print a receipt/invoice/lab-order PDF from the backend.
 * Use this for any document that has a backend PDF endpoint.
 */
export async function printReceipt(url: string, filename: string): Promise<void> {
  try {
    await printPdfFromApi(url, filename);
  } catch (err) {
    console.error("Print receipt failed:", err);
    // Fallback to window.print() if the PDF endpoint fails
    window.print();
  }
}

/**
 * Print the current screen (for on-screen reports without backend PDF).
 * Adds a 'printing' class to body so print CSS can hide dashboard chrome.
 */
export function printScreen(): void {
  document.body.classList.add("printing");
  window.print();
  // Remove the class after printing completes
  setTimeout(() => document.body.classList.remove("printing"), 1000);
}

/**
 * CSS class for elements that should only be visible during print.
 * Add to print-only headers, page numbers, etc.
 */
export const PRINT_ONLY_CLASS = "print-only";

/**
 * CSS class for elements that should be hidden during print.
 * Add to sidebar, topbar, action buttons, etc.
 */
export const PRINT_HIDDEN_CLASS = "print-hidden";
