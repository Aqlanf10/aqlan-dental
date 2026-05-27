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
import { formatYER, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 3: Invoices
   ═══════════════════════════════════════════════════════════════════════════════ */
export function InvoicesTab() {
  const [data, setData] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: InvoiceListItem[]; total: number }>("/api/finance-v3/invoices"); setData(responseData.data); } catch { toast.error("فشل في تحميل الفواتير"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (inv: InvoiceListItem) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<InvoiceDetail>(`/api/invoices/${inv.Id}`);
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
    i.InvoiceNumber.includes(search) || i.PatientName.includes(search) || i.PatientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الفواتير" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالرقم أو الاسم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : filtered.length === 0 ? <EmptyState icon={FileText} message="لا توجد فواتير" /> : (
        <DataTable<InvoiceListItem>
          keyField="Id"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "InvoiceNumber", label: "رقم الفاتورة" },
            { key: "PatientName", label: "المريض" },
            { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
            { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
            { key: "Balance", label: "المتبقي", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
            { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
            { key: "IssueDate", label: "التاريخ", render: (r) => new Date(r.IssueDate).toLocaleDateString("ar-SA") },
          ]}
        />
      )}

      {/* Invoice detail modal */}
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`فاتورة ${detail?.InvoiceNumber ?? ""}`} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : detail ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{detail.PatientName} ({detail.PatientNumber})</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الحالة</p><StatusBadge status={detail.Status} /></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإجمالي</p><p className="text-sm font-bold">{formatYER(detail.TotalAmount)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.PaidAmount)}</p></div>
            </div>

            {/* Line items */}
            {detail.LineItems && detail.LineItems.length > 0 && (
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
                      {detail.LineItems.map((li) => (
                        <tr key={li.Id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                          <td className="px-3 py-2">{li.TreatmentName}</td>
                          <td className="px-3 py-2">{li.ToothNumber ?? "—"}</td>
                          <td className="px-3 py-2">{li.Quantity}</td>
                          <td className="px-3 py-2">{formatYER(li.UnitPrice)}</td>
                          <td className="px-3 py-2">{formatYER(li.DiscountAmount)}</td>
                          <td className="px-3 py-2 font-bold">{formatYER(li.TotalPrice)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Cancel action */}
            {detail.Status === "Draft" && (
              <div className="flex justify-end pt-2 border-t" style={{ borderColor: tokens.border }}>
                <button onClick={() => setConfirmCancel(detail.Id)} style={btnDanger}>
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
