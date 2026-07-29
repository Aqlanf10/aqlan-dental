/**
 * Canonical Finance V3 navigation helpers.
 * Replaces removed /finance/* routes (V1/V2 UI).
 */

const FINANCE_V3 = "/finance-v3";

export type FinanceV3Tab =
  | "overview"
  | "patient-acct"
  | "invoices"
  | "collections"
  | "contracts"
  | "cashier"
  | "treasuries"
  | "expenses"
  | "suppliers"
  | "journal"
  | "audit";

/** Build Finance V3 URL with optional tab and query params. */
export function financeV3Url(
  tab?: FinanceV3Tab,
  params?: Record<string, string | undefined>
): string {
  const qs = new URLSearchParams();
  if (tab) qs.set("tab", tab);
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value != null && value !== "") qs.set(key, value);
    }
  }
  const query = qs.toString();
  return query ? `${FINANCE_V3}?${query}` : FINANCE_V3;
}

/** Daily checkout / quick payment — Reception uses daily-operations. */
export function dailyOperationsFinanceUrl(patientId?: string): string {
  if (patientId) return `/daily-operations?patientId=${encodeURIComponent(patientId)}`;
  return "/daily-operations";
}

export function financeV3CollectionsUrl(patientId?: string): string {
  return financeV3Url("collections", patientId ? { patientId } : undefined);
}

export function financeV3InvoicesUrl(patientId?: string): string {
  return financeV3Url("invoices", patientId ? { patientId } : undefined);
}

export function financeV3ContractsUrl(
  patientId?: string,
  extra?: Record<string, string | undefined>
): string {
  return financeV3Url("contracts", { ...extra, patientId });
}

/** Patient receipt print (legacy payments API PDF). */
export function patientPaymentReceiptUrl(patientId: string, paymentId: string): string {
  return `/patients/${patientId}/payments/${paymentId}/receipt`;
}
