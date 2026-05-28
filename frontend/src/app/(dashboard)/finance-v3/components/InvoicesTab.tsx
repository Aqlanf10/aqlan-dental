"use client";

import { useState, useEffect, useCallback } from "react";
import {
  FileText,
  RefreshCw,
  XCircle,
  Loader2,
  Plus,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InvoiceListItem, InvoiceDetail } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, StatusBadge, ConfirmDialog, tokens, inputStyle, btnDanger, btnPrimary } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate, safeArray } from "./FinanceHelpers";
import CreateInvoiceModal from "./CreateInvoiceModal";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 3: Invoices — مع دعم إنشاء فواتير بالتأمين والضرائب
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */

// ── Minimal patient list type for patient selector ──
interface PatientOption {
  id: string;
  name: string;
  number: string;
}

export function InvoicesTab() {
  const [data, setData] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);

  // ── Create Invoice Modal state ──
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [invoicePatientId, setInvoicePatientId] = useState<string>("");
  const [invoicePatientName, setInvoicePatientName] = useState<string>("");

  // ── Patient selector modal ──
  const [showPatientPicker, setShowPatientPicker] = useState(false);
  const [patients, setPatients] = useState<PatientOption[]>([]);
  const [patientSearch, setPatientSearch] = useState("");
  const [patientsLoading, setPatientsLoading] = useState(false);

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

  // ── Fetch patients for selector ──
  const fetchPatients = useCallback(async () => {
    try {
      setPatientsLoading(true);
      const { data: responseData } = await api.get<{ data: PatientOption[] }>("/api/patients", {
        params: { search: patientSearch || undefined, pageSize: 20 },
      });
      const list = safeArray(responseData?.data ?? (Array.isArray(responseData) ? responseData as unknown as PatientOption[] : undefined));
      setPatients(list);
    } catch {
      setPatients([]);
    } finally { setPatientsLoading(false); }
  }, [patientSearch]);

  useEffect(() => {
    if (showPatientPicker) fetchPatients();
  }, [showPatientPicker, fetchPatients]);

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

  // ── Open create invoice flow ──
  const openCreateInvoice = (patient: PatientOption) => {
    setInvoicePatientId(patient.id);
    setInvoicePatientName(`${patient.name} (${patient.number})`);
    setShowPatientPicker(false);
    setShowCreateModal(true);
  };

  const filtered = data.filter((i) =>
    (i.invoiceNumber ?? "").includes(search) || (i.patientName ?? "").includes(search) || (i.patientNumber ?? "").includes(search)
  );

  const filteredPatients = patients.filter((p) =>
    (p.name ?? "").includes(patientSearch) || (p.number ?? "").includes(patientSearch)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="الفواتير" action={
        <div className="flex items-center gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="بحث بالرقم أو الاسم..." style={{ ...inputStyle, width: 240, fontSize: 13 }} />
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
          <button onClick={() => setShowPatientPicker(true)} style={btnPrimary}>
            <Plus className="w-4 h-4" /> فاتورة جديدة
          </button>
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

      {/* ── Patient Picker Modal ── */}
      <Modal open={showPatientPicker} onClose={() => setShowPatientPicker(false)} title="اختر المريض لإصدار الفاتورة">
        <div className="space-y-3">
          <input
            value={patientSearch}
            onChange={(e) => setPatientSearch(e.target.value)}
            placeholder="بحث بالاسم أو الرقم..."
            style={inputStyle}
            autoFocus
          />
          {patientsLoading ? (
            <LoadingSkeleton rows={5} />
          ) : filteredPatients.length === 0 ? (
            <p className="text-center text-sm py-6" style={{ color: tokens.textTertiary }}>
              لا يوجد مرضى
            </p>
          ) : (
            <div className="max-h-64 overflow-y-auto space-y-1">
              {filteredPatients.map((p) => (
                <button
                  key={p.id}
                  className="w-full text-right px-3 py-2.5 rounded-md text-sm transition-colors"
                  style={{ color: tokens.textPrimary, border: `1px solid ${tokens.border}`, backgroundColor: "transparent", cursor: "pointer" }}
                  onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; }}
                  onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                  onClick={() => openCreateInvoice(p)}
                >
                  <span className="font-semibold">{p.name}</span>
                  <span className="text-xs mr-2" style={{ color: tokens.textTertiary }}>({p.number})</span>
                </button>
              ))}
            </div>
          )}
        </div>
      </Modal>

      {/* ── Create Invoice Modal ── */}
      {invoicePatientId && (
        <CreateInvoiceModal
          patientId={invoicePatientId}
          patientName={invoicePatientName}
          open={showCreateModal}
          onClose={() => {
            setShowCreateModal(false);
            setInvoicePatientId("");
            setInvoicePatientName("");
          }}
          onCreated={fetchData}
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
