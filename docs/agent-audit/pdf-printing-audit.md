# PDF & Printing Audit — Aqlan Dental Pro
**التاريخ:** 2026-06-12

## Architecture (verified)
- **Engine:** QuestPDF (Community license, set in `PdfService.cs:45`).
- **Arabic font:** Noto Naskh Arabic Regular + Bold, registered thread-safely with multi-path fallback (`PdfService.EnsureFontsRegistered`, lines 39–115): app `Fonts/` dir → cwd → 3 system locations. Falls back to system fonts with stderr warning. Font family name is correctly **"Noto Naskh Arabic"** (with spaces) — the no-spaces regression was fixed in commits f07929d/c0f0dab.
- **RTL:** every document uses `page.ContentFromRightToLeft()`.

## Documents
| Document | File | Layout |
|---|---|---|
| سند قبض (receipt) | `PdfDocuments.cs:11-182` | 105×148mm compact: clinic name, patient + file number, doctor, method, highlighted amount, notes, cashier/patient/clinic signature lines, footer (phone + address + thanks) |
| فاتورة (invoice) | `PdfDocuments.cs:378-638` | A4: clinic header + logo, line-items table, subtotal/discount/tax/total, payment status, signatures |
| كشف حساب (statement) | `PdfDocuments.cs:184-373` | A4: cost summary + payment history tables |
| أمر مختبر (lab order) | `API/Services/LabOrderPdfGenerator.cs` | A4: tracking barcode, patient/lab info, work-items table, totals, doctor instructions/signature; resilient to null relations with Arabic fallback "غير محدد" + schema-tolerant fallback query |

## Endpoints
- `GET /api/payments/{id}/pdf` (FinanceAccess) · `GET /api/invoices/{id}/pdf` (FinanceAccess) · `GET /api/patients/{id}/financial-statement/pdf` · `GET /api/reports/patient/{id}/financial-statement-pdf` · `GET /api/lab-orders/{id}/print` (StaffOnly).
- All return Arabic 404/500 messages.

## Frontend Download vs Print (verified strictly separated)
`frontend/src/lib/pdfDownload.ts`:
- **`downloadPdfFromApi`** — `<a download>` only; never opens windows or prints.
- **`printPdfFromApi`** — opens popup *before* any await (popup-blocker safe), loads PDF in iframe, triggers `contentWindow.print()`, shows manual fallback buttons after 3s.
- **`openPdfFromApi`** — new-tab view.
- `extractPdfError` parses blob error bodies → Arabic message.
- Enforced by static-analysis tests (`__tests__/lib/pdfDownload.test.ts`) that forbid cross-contamination between the three functions.

## Fixed in This Sprint
- **Exception detail leak removed** from all PDF/lab 500 responses (see security-audit.md). Responses now carry only the Arabic `message`; full details stay in server logs.

## Remaining Recommendations
1. Logo upload support for receipt header (invoice already supports logo; receipt shows clinic name text).
2. Optional: daily-operations report PDF export (data endpoint exists; no PDF yet).
3. Keep `backend/Fonts/` deployed with the container (Dockerfile already copies it — verified).
