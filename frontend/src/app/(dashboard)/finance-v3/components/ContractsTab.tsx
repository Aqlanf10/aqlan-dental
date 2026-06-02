"use client";

import { useState, useEffect, useCallback } from "react";
import {
  HandCoins,
  RefreshCw,
  AlertTriangle,
  Plus,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { ContractListItem } from "./types";
import {
  SectionHeader,
  LoadingSkeleton,
  EmptyState,
  DataTable,
  StatusBadge,
  tokens,
  inputStyle,
  Modal,
  labelStyle,
  btnPrimary,
  btnGhost,
} from "./FinanceSharedUI";
import { formatYER, safeFormatDate } from "./FinanceHelpers";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

/* ═══════════════════════════════════════════════════════════════════════════════
   CreateContractModal Component
   ═══════════════════════════════════════════════════════════════════════════════ */
interface CreateContractModalProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  initialPatientId?: string;
  initialPatientName?: string;
}

export function CreateContractModal({
  open,
  onClose,
  onSuccess,
  initialPatientId,
  initialPatientName,
}: CreateContractModalProps) {
  const [selectedPatientId, setSelectedPatientId] = useState(initialPatientId ?? "");
  const [selectedPatientName, setSelectedPatientName] = useState(initialPatientName ?? "");
  const [specialty, setSpecialty] = useState("");
  const [totalAmount, setTotalAmount] = useState<number>(0);
  const [downPayment, setDownPayment] = useState<number>(0);
  const [installmentsCount, setInstallmentsCount] = useState<number>(1);
  const [discountAmount, setDiscountAmount] = useState<number>(0);
  const [discountReason, setDiscountReason] = useState("");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (open) {
      setSelectedPatientId(initialPatientId ?? "");
      setSelectedPatientName(initialPatientName ?? "");
      setSpecialty("");
      setTotalAmount(0);
      setDownPayment(0);
      setInstallmentsCount(1);
      setDiscountAmount(0);
      setDiscountReason("");
      setStartDate(new Date().toISOString().slice(0, 10));
      setNotes("");
      setError("");
    }
  }, [open, initialPatientId, initialPatientName]);

  const netAmount = totalAmount - discountAmount - downPayment;
  const calculatedInstallment = installmentsCount > 0 && netAmount > 0
    ? Math.ceil(netAmount / installmentsCount)
    : 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId) {
      setError("الرجاء اختيار مريض");
      return;
    }
    if (totalAmount <= 0) {
      setError("يجب أن يكون إجمالي العقد أكبر من الصفر");
      return;
    }
    if (discountAmount > totalAmount) {
      setError("الخصم لا يمكن أن يكون أكبر من إجمالي العقد");
      return;
    }
    if (downPayment > (totalAmount - discountAmount)) {
      setError("الدفعة الأولى لا يمكن أن تكون أكبر من صافي العقد");
      return;
    }

    setSaving(true);
    setError("");
    try {
      const payload = {
        patientId: selectedPatientId,
        specialty: specialty || null,
        totalAmount,
        downPayment,
        installmentsCount,
        installmentAmount: calculatedInstallment,
        startDate: startDate || null,
        discountAmount,
        discountReason: discountReason || null,
        notes: notes || null,
      };
      await api.post("/api/contracts", payload);
      toast.success("تم إنشاء العقد بنجاح");
      onSuccess();
      onClose();
    } catch (err: unknown) {
      let msg = "حدث خطأ أثناء حفظ العقد";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) msg = resp.data.message;
      }
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title="إنشاء عقد جديد" wide>
      <form onSubmit={handleSubmit} className="space-y-4" style={{ direction: "rtl" }}>
        {error && (
          <div className="p-3 text-xs rounded" style={{ backgroundColor: tokens.dangerBg, color: tokens.dangerText, border: `1px solid ${tokens.dangerBorder}` }}>
            {error}
          </div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="md:col-span-2">
            <label style={labelStyle}>المريض <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <PatientCombobox
              defaultDisplayValue={selectedPatientName}
              onSelect={(p) => {
                setSelectedPatientId(p.id);
                setSelectedPatientName(`${p.fullName} (${p.patientNumber})`);
              }}
            />
          </div>

          <div>
            <label style={labelStyle}>التخصص</label>
            <select
              value={specialty}
              onChange={(e) => setSpecialty(e.target.value)}
              style={inputStyle}
            >
              <option value="">اختر التخصص...</option>
              <option value="orthodontics">تقويم أسنان</option>
              <option value="general">طب أسنان عام</option>
              <option value="surgery">جراحة فم وزراعة</option>
              <option value="other">أخرى</option>
            </select>
          </div>

          <div>
            <label style={labelStyle}>تاريخ بدء العقد</label>
            <input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>الإجمالي المالي (ريال) <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              type="number"
              min={0}
              value={totalAmount || ""}
              onChange={(e) => setTotalAmount(Number(e.target.value))}
              style={inputStyle}
              placeholder="0"
            />
          </div>

          <div>
            <label style={labelStyle}>الخصم (ريال)</label>
            <input
              type="number"
              min={0}
              value={discountAmount || ""}
              onChange={(e) => setDiscountAmount(Number(e.target.value))}
              style={inputStyle}
              placeholder="0"
            />
          </div>

          {discountAmount > 0 && (
            <div className="md:col-span-2">
              <label style={labelStyle}>سبب الخصم</label>
              <input
                type="text"
                value={discountReason}
                onChange={(e) => setDiscountReason(e.target.value)}
                style={inputStyle}
                placeholder="سبب منح الخصم..."
              />
            </div>
          )}

          <div>
            <label style={labelStyle}>الدفعة الأولى (ريال)</label>
            <input
              type="number"
              min={0}
              value={downPayment || ""}
              onChange={(e) => setDownPayment(Number(e.target.value))}
              style={inputStyle}
              placeholder="0"
            />
          </div>

          <div>
            <label style={labelStyle}>عدد الأقساط</label>
            <input
              type="number"
              min={1}
              value={installmentsCount}
              onChange={(e) => setInstallmentsCount(Number(e.target.value))}
              style={inputStyle}
            />
          </div>

          {totalAmount > 0 && (
            <div className="md:col-span-2 p-3 rounded" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <div className="grid grid-cols-3 gap-3 text-center text-xs font-bold" style={{ color: tokens.infoText }}>
                <div>
                  <p className="opacity-75">المبلغ الصافي</p>
                  <p className="text-sm font-black mt-0.5">{(totalAmount - discountAmount).toLocaleString()} ر.ي</p>
                </div>
                <div>
                  <p className="opacity-75">المتبقي للأقساط</p>
                  <p className="text-sm font-black mt-0.5">{netAmount.toLocaleString()} ر.ي</p>
                </div>
                <div>
                  <p className="opacity-75">قيمة القسط الشهري</p>
                  <p className="text-sm font-black mt-0.5">{calculatedInstallment.toLocaleString()} ر.ي</p>
                </div>
              </div>
            </div>
          )}

          <div className="md:col-span-2">
            <label style={labelStyle}>ملاحظات العقد</label>
            <textarea
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              style={inputStyle}
              placeholder="أية تفاصيل إضافية..."
            />
          </div>
        </div>

        <div className="flex items-center justify-end gap-2 pt-4 border-t" style={{ borderColor: tokens.border }}>
          <button type="button" onClick={onClose} style={btnGhost} disabled={saving}>إلغاء</button>
          <button type="submit" style={btnPrimary} disabled={saving}>
            {saving ? "جاري الحفظ..." : "إنشاء العقد"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 5: Contracts Component
   ═══════════════════════════════════════════════════════════════════════════════ */
interface ContractsTabProps {
  patientId?: string;
  patientName?: string;
}

export function ContractsTab({ patientId, patientName }: ContractsTabProps) {
  const [data, setData] = useState<ContractListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // Modal prefills
  const [initialPatientId, setInitialPatientId] = useState(patientId ?? "");
  const [initialPatientName, setInitialPatientName] = useState(patientName ?? "");
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (statusFilter) params.status = statusFilter;
      const { data } = await api.get<{ data: ContractListItem[]; total: number }>("/api/finance-v3/contracts", { params });
      const contracts = data?.data ?? (Array.isArray(data) ? data as unknown as ContractListItem[] : []);
      setData(contracts);
    } catch {
      toast.error("فشل في تحميل العقود");
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Handle auto-open when patientId is passed down
  useEffect(() => {
    if (patientId) {
      setInitialPatientId(patientId);
      setIsCreateModalOpen(true);
      if (patientName) {
        setInitialPatientName(patientName);
      } else {
        // Fetch patient fallback to display name correctly
        api.get(`/api/patients/${patientId}`)
          .then((res) => {
            const p = res.data as { firstName: string; lastName: string; patientNumber: string };
            setInitialPatientName(`${p.firstName} ${p.lastName} (${p.patientNumber})`);
          })
          .catch(() => {
            setInitialPatientName("");
          });
      }
    }
  }, [patientId, patientName]);

  const filtered = data.filter((c) =>
    c.patientName.includes(search) || c.contractNumber.includes(search) || c.patientNumber.includes(search)
  );

  return (
    <div className="p-6 space-y-4">
      <SectionHeader
        title="العقود"
        action={
          <div className="flex items-center gap-2">
            <button
              onClick={() => {
                setInitialPatientId("");
                setInitialPatientName("");
                setIsCreateModalOpen(true);
              }}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded text-xs font-semibold text-white transition-opacity hover:opacity-90"
              style={{ backgroundColor: tokens.brand }}
            >
              <Plus className="w-3.5 h-3.5" />
              عقد جديد
            </button>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث..."
              style={{ ...inputStyle, width: 200, fontSize: 13 }}
            />
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              style={{ ...inputStyle, width: 140, fontSize: 13 }}
            >
              <option value="">جميع الحالات</option>
              <option value="Active">نشط</option>
              <option value="Completed">مكتمل</option>
              <option value="Overdue">متأخرة</option>
            </select>
            <button
              onClick={fetchData}
              className="w-8 h-8 rounded-md flex items-center justify-center"
              style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }}
              title="تحديث"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        }
      />

      {loading ? (
        <LoadingSkeleton />
      ) : filtered.length === 0 ? (
        <EmptyState icon={HandCoins} message="لا توجد عقود" />
      ) : (
        <DataTable<ContractListItem>
          keyField="id"
          data={filtered}
          columns={[
            { key: "contractNumber", label: "رقم العقد" },
            { key: "patientName", label: "المريض" },
            { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount) },
            { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount) },
            {
              key: "outstandingAmount",
              label: "المستحق",
              render: (r) => (
                <span
                  style={{
                    color: r.outstandingAmount > 0 ? tokens.dangerBorder : tokens.successBorder,
                    fontWeight: 700,
                  }}
                >
                  {formatYER(r.outstandingAmount)}
                </span>
              ),
            },
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            {
              key: "isOverdue",
              label: "متأخرة",
              render: (r) =>
                r.isOverdue ? (
                  <AlertTriangle className="w-4 h-4" style={{ color: tokens.dangerBorder }} />
                ) : (
                  "—"
                ),
            },
            { key: "startDate", label: "تاريخ البداية", render: (r) => safeFormatDate(r.startDate) },
          ]}
        />
      )}

      {/* Contract Creation Modal */}
      <CreateContractModal
        open={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSuccess={fetchData}
        initialPatientId={initialPatientId}
        initialPatientName={initialPatientName}
      />
    </div>
  );
}
