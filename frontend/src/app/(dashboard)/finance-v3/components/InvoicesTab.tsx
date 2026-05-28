"use client";

import { useState, useEffect, useCallback } from "react";
import {
  FileText,
  RefreshCw,
  XCircle,
  Loader2,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InvoiceListItem, InvoiceDetail } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, StatusBadge, ConfirmDialog, tokens, inputStyle, btnDanger } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 3: Invoices
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function InvoicesTab() {
  const [data, setData] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data: responseData } = await api.get<{ data: InvoiceListItem[]; total: number }>("/api/finance-v3/invoices");
      setData(safeArray(responseData?.data));
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل الفواتير"));
      toast.error("فشل في تحميل الفواتير");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (inv: InvoiceListItem) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<InvoiceDetail>(`/api/invoices/${inv.id}`);
      setDetail(data);
    } catch { toast.error("فشل في تحميل تفاصيل الفاتورة"); } finally { setDetailLoading(false); }
  };

  const handleCancel = async () => {
    if (!confirmCancel) return;
    try {
      setCancelling(true);
      await api.patch(`/api/finance-v3/invoices/${confirmCancel}/cancel`);
      toast.success("تم إلغاء الفاتورة بنجاح");
      setConfirmCancel(null);
      setDetail(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إلغاء الفاتورة")); } finally { setCancelling(false); }
  };

  const filtered = data.filter((i) =>
    (i.invoiceNumber ?? "").includes(search) || (i.patientName ?? "").includes(search) || (i.patientNumber ?? "").includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الفواتير" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالرقم أو الاسم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : error ? (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
          <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
          <button onClick={fetchData} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
        </div>
      ) : filtered.length === 0 ? <EmptyState icon={FileText} message="لا توجد فواتير" /> : (
        <DataTable<InvoiceListItem>
          keyField="id"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "invoiceNumber", label: "رقم الفاتورة" },
            { key: "patientName", label: "المريض" },
            { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount ?? 0) },
            { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount ?? 0) },
            { key: "balance", label: "المتبقي", render: (r) => <span style={{ color: (r.balance ?? 0) > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.balance ?? 0)}</span> },
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "issueDate", label: "التاريخ", render: (r) => safeFormatDate(r.issueDate ?? "") },
          ]}
        />
      )}

      {/* Invoice detail modal */}
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`فاتورة ${detail?.invoiceNumber ?? ""}`} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : detail ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{detail.patientName} ({detail.patientNumber})</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الحالة</p><StatusBadge status={detail.status} /></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإجمالي</p><p className="text-sm font-bold">{formatYER(detail.totalAmount ?? 0)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.paidAmount ?? 0)}</p></div>
            </div>

            {/* Line items */}
            {(detail.lineItems ?? []).length > 0 && (
              <div>
                <h4 className="text-xs font-semibold mb-2" style={{ color: tokens.textSecondary }}>البنود</h4>
                <div className="overflow-x-auto rounded-md border" style={{ borderColor: tokens.border }}>
                  <table className="w-full text-xs">
                    <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                      <th className="text-right px-3 py-2">العلاج</th>
                      <th className="text-right px-3 py-2">السن</th>
                      <th className="text-right px-3 py-2">الكمية</th>
                      <th className="text-right px-3 py-2">سعر الوحدة</th>
                      <th className="text-right px-3 py-2">الخصم</th>
                      <th className="text-right px-3 py-2">الإجمالي</th>
                    </tr></thead>
                    <tbody>
                      {(detail.lineItems ?? []).map((li) => (
                        <tr key={li.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                          <td className="px-3 py-2">{li.treatmentName}</td>
                          <td className="px-3 py-2">{li.toothNumber ?? "—"}</td>
                          <td className="px-3 py-2">{li.quantity}</td>
                          <td className="px-3 py-2">{formatYER(li.unitPrice)}</td>
                          <td className="px-3 py-2">{formatYER(li.discountAmount)}</td>
                          <td className="px-3 py-2 font-bold">{formatYER(li.totalPrice)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Cancel action */}
            {detail.status === "Draft" && (
              <div className="flex justify-end pt-2 border-t" style={{ borderColor: tokens.border }}>
                <button onClick={() => setConfirmCancel(detail.id)} style={btnDanger}>
                  <XCircle className="w-4 h-4" /> إلغاء الفاتورة
                </button>
              </div>
            )}
          </div>
        ) : null}
      </Modal>

      <ConfirmDialog open={!!confirmCancel} onClose={() => setConfirmCancel(null)} onConfirm={handleCancel} title="إلغاء الفاتورة" message="هل أنت متأكد من إلغاء هذه الفاتورة؟ لا يمكن التراجع عن هذا الإجراء." confirmLabel="إلغاء الفاتورة" danger />
      {cancelling && <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/20"><Loader2 className="w-6 h-6 animate-spin" style={{ color: tokens.brand }} /></div>}
    </div>
  );
}
