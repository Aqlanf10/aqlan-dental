"use client";

import { useState, useEffect, useCallback } from "react";
import {
  FileText,
  RefreshCw,
  XCircle,
  Loader2,
  RotateCcw,
  HandCoins,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InvoiceListItem, InvoiceDetail, CreditNoteDto, CreateCreditNoteRequest, ProcessRefundRequest } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, btnDanger, btnPrimary } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate } from "./FinanceHelpers";
import CreateCreditNoteModal from "./CreateCreditNoteModal";
import ProcessRefundModal from "./ProcessRefundModal";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 3: Invoices — with Credit Notes & Refund integration
   ═══════════════════════════════════════════════════════════════════════════════ */
export function InvoicesTab() {
  const [data, setData] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  // Credit Note & Refund states
  const [showCreditNote, setShowCreditNote] = useState<InvoiceListItem | null>(null);
  const [creditNotes, setCreditNotes] = useState<CreditNoteDto[]>([]);
  const [showRefund, setShowRefund] = useState<CreditNoteDto | null>(null);

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: InvoiceListItem[]; total: number }>("/api/finance-v3/invoices"); setData(responseData?.data ?? []); } catch { toast.error("فشل في تحميل الفواتير"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const openDetail = async (inv: InvoiceListItem) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<InvoiceDetail>(`/api/invoices/${inv.id}`);
      setDetail(data);
      // Also fetch credit notes for this invoice
      try {
        const cnRes = await api.get<{ data: CreditNoteDto[] }>(`/api/finance-v3/credit-notes?invoiceId=${inv.id}`);
        setCreditNotes(cnRes.data?.data ?? (Array.isArray(cnRes.data) ? cnRes.data as unknown as CreditNoteDto[] : []));
      } catch {
        setCreditNotes([]);
      }
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

  // Create Credit Note handler
  const handleCreateCreditNote = async (req: CreateCreditNoteRequest) => {
    try {
      await api.post("/api/finance-v3/credit-notes", req);
      toast.success("تم إصدار إشعار الدائن بنجاح");
      setShowCreditNote(null);
      fetchData();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في إصدار إشعار الدائن"));
      throw err; // re-throw so modal knows it failed
    }
  };

  // Process Refund handler
  const handleProcessRefund = async (creditNoteId: string, req: ProcessRefundRequest) => {
    try {
      await api.post(`/api/finance-v3/credit-notes/${creditNoteId}/refund`, req);
      toast.success("تم تسليم المرتجع للمريض وخصمه من الخزينة");
      setShowRefund(null);
      setCreditNotes((prev) =>
        prev.map((cn) => (cn.id === creditNoteId ? { ...cn, status: "Refunded" as const } : cn))
      );
      fetchData();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في تسليم المرتجع"));
      throw err;
    }
  };

  const filtered = data.filter((i) =>
    i.invoiceNumber.includes(search) || i.patientName.includes(search) || i.patientNumber.includes(search)
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
          keyField="id"
          data={filtered}
          onRowClick={openDetail}
          columns={[
            { key: "invoiceNumber", label: "رقم الفاتورة" },
            { key: "patientName", label: "المريض" },
            { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount) },
            { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount) },
            { key: "balance", label: "المتبقي", render: (r) => <span style={{ color: r.balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.balance)}</span> },
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "issueDate", label: "التاريخ", render: (r) => safeFormatDate(r.issueDate) },
            { key: "refundAction", label: "", render: (r) => r.paidAmount > 0 && r.status !== "Cancelled" ? (
              <button
                onClick={(e) => { e.stopPropagation(); setShowCreditNote(r); }}
                className="w-7 h-7 rounded-md flex items-center justify-center"
                style={{ color: tokens.warningBorder }}
                title="إرجاع مبلغ"
              >
                <RotateCcw className="w-3.5 h-3.5" />
              </button>
            ) : null },
          ]}
        />
      )}

      {/* Invoice detail modal */}
      <Modal open={!!detail} onClose={() => { setDetail(null); setCreditNotes([]); }} title={`فاتورة ${detail?.invoiceNumber ?? ""}`} wide>
        {detailLoading ? <LoadingSkeleton rows={4} /> : detail ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المريض</p><p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{detail.patientName} ({detail.patientNumber})</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الحالة</p><StatusBadge status={detail.status} /></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإجمالي</p><p className="text-sm font-bold">{formatYER(detail.totalAmount)}</p></div>
              <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المدفوع</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.paidAmount)}</p></div>
            </div>

            {/* Line items */}
            {detail.lineItems && detail.lineItems.length > 0 && (
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
                      {detail.lineItems.map((li) => (
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

            {/* Credit Notes for this invoice */}
            {creditNotes.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold mb-2" style={{ color: tokens.textSecondary }}>إشعارات الدائن (المرتجعات)</h4>
                <div className="overflow-x-auto rounded-md border" style={{ borderColor: tokens.border }}>
                  <table className="w-full text-xs">
                    <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                      <th className="text-right px-3 py-2">المبلغ</th>
                      <th className="text-right px-3 py-2">السبب</th>
                      <th className="text-right px-3 py-2">الحالة</th>
                      <th className="text-right px-3 py-2">التاريخ</th>
                      <th className="text-right px-3 py-2">إجراء</th>
                    </tr></thead>
                    <tbody>
                      {creditNotes.map((cn) => (
                        <tr key={cn.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                          <td className="px-3 py-2 font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(cn.amount)}</td>
                          <td className="px-3 py-2">{cn.reason}</td>
                          <td className="px-3 py-2"><StatusBadge status={cn.status} /></td>
                          <td className="px-3 py-2">{safeFormatDate(cn.createdAt)}</td>
                          <td className="px-3 py-2">
                            {cn.status === "Approved" && (
                              <button
                                onClick={() => setShowRefund(cn)}
                                className="inline-flex items-center gap-1 px-2 py-1 rounded text-[11px] font-semibold"
                                style={{ backgroundColor: tokens.warningBg, color: tokens.warningBorder, border: `1px solid ${tokens.warningBorder}` }}
                              >
                                <HandCoins className="w-3 h-3" /> تسليم النقد
                              </button>
                            )}
                            {cn.status === "Refunded" && (
                              <span className="text-[11px]" style={{ color: tokens.successBorder }}>✓ تم التسليم</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex justify-between pt-2 border-t" style={{ borderColor: tokens.border }}>
              {/* Refund button */}
              {detail.paidAmount > 0 && detail.status !== "Cancelled" && (
                <button
                  onClick={() => {
                    const inv = data.find((i) => i.id === detail.id);
                    if (inv) setShowCreditNote(inv);
                  }}
                  style={{ ...btnPrimary, backgroundColor: tokens.warningBorder }}
                >
                  <RotateCcw className="w-4 h-4" /> إرجاع مبلغ
                </button>
              )}
              <div className="flex-1" />
              {/* Cancel button */}
              {detail.status === "Draft" && (
                <button onClick={() => setConfirmCancel(detail.id)} style={btnDanger}>
                  <XCircle className="w-4 h-4" /> إلغاء الفاتورة
                </button>
              )}
            </div>
          </div>
        ) : null}
      </Modal>

      <ConfirmDialog open={!!confirmCancel} onClose={() => setConfirmCancel(null)} onConfirm={handleCancel} title="إلغاء الفاتورة" message="هل أنت متأكد من إلغاء هذه الفاتورة؟ لا يمكن التراجع عن هذا الإجراء." confirmLabel="إلغاء الفاتورة" danger />
      {cancelling && <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/20"><Loader2 className="w-6 h-6 animate-spin" style={{ color: tokens.brand }} /></div>}

      {/* Create Credit Note Modal */}
      {showCreditNote && (
        <CreateCreditNoteModal
          invoiceId={showCreditNote.id}
          invoiceNumber={showCreditNote.invoiceNumber}
          invoiceTotal={showCreditNote.totalAmount}
          paidAmount={showCreditNote.paidAmount}
          isOpen={!!showCreditNote}
          onClose={() => setShowCreditNote(null)}
          onSubmit={handleCreateCreditNote}
        />
      )}

      {/* Process Refund Modal */}
      {showRefund && (
        <ProcessRefundModal
          creditNoteId={showRefund.id}
          refundAmount={showRefund.amount}
          isOpen={!!showRefund}
          onClose={() => setShowRefund(null)}
          onSubmit={handleProcessRefund}
        />
      )}
    </div>
  );
}
