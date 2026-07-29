
import { useCallback, useEffect, useState } from "react";
import { CalendarRange, LockKeyhole, Plus, RefreshCw } from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { extractErrorMessage, safeFormatDateTime } from "./FinanceHelpers";
import {
  ConfirmDialog,
  EmptyState,
  LoadingSkeleton,
  Modal,
  SectionHeader,
  StatusBadge,
  btnGhost,
  btnPrimary,
  inputStyle,
  labelStyle,
  tokens,
} from "./FinanceSharedUI";

interface AccountingPeriod {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: "Open" | "Closed";
  closedAt: string | null;
  notes: string | null;
  isAnnual: boolean;
  closingEntryCount: number;
}

const currentYear = new Date().getFullYear();

export function AccountingPeriodsTab() {
  const [periods, setPeriods] = useState<AccountingPeriod[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [periodToClose, setPeriodToClose] = useState<AccountingPeriod | null>(null);
  const [name, setName] = useState(`السنة المالية ${currentYear}`);
  const [startDate, setStartDate] = useState(`${currentYear}-01-01`);
  const [endDate, setEndDate] = useState(`${currentYear}-12-31`);
  const [notes, setNotes] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await api.get<{ data: AccountingPeriod[] }>("/api/finance-v3/accounting-periods");
      setPeriods(data.data ?? []);
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر تحميل الفترات المالية"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const createPeriod = async () => {
    if (!name.trim() || !startDate || !endDate) {
      toast.error("أدخل اسم الفترة وتاريخ البداية والنهاية");
      return;
    }
    setSubmitting(true);
    try {
      await api.post("/api/finance-v3/accounting-periods", {
        name: name.trim(),
        startDate,
        endDate,
        notes: notes.trim() || null,
      });
      toast.success("تم إنشاء الفترة المالية");
      setShowCreate(false);
      setNotes("");
      await load();
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر إنشاء الفترة المالية"));
    } finally {
      setSubmitting(false);
    }
  };

  const closePeriod = async () => {
    if (!periodToClose) return;
    setSubmitting(true);
    try {
      if (periodToClose.isAnnual) {
        const nextYear = Number(periodToClose.endDate.slice(0, 4)) + 1;
        await api.post(`/api/finance-v3/accounting-periods/${periodToClose.id}/year-end-close`, {
          nextPeriodName: `السنة المالية ${nextYear}`,
        });
        toast.success("تم إقفال السنة وترحيل النتيجة وفتح السنة التالية");
      } else {
        await api.post(`/api/finance-v3/accounting-periods/${periodToClose.id}/close`);
        toast.success("تم إقفال الفترة المالية");
      }
      setPeriodToClose(null);
      await load();
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر إقفال الفترة المالية"));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="p-6 space-y-4">
      <SectionHeader
        title="الفترات المالية"
        action={(
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => void load()} style={btnGhost} title="تحديث">
              <RefreshCw className="w-4 h-4" />
            </button>
            <button type="button" onClick={() => setShowCreate(true)} style={btnPrimary}>
              <Plus className="w-4 h-4" /> فترة جديدة
            </button>
          </div>
        )}
      />

      {periods.length === 0 ? (
        <EmptyState icon={CalendarRange} message="لا توجد فترات مالية" />
      ) : (
        <div className="overflow-x-auto border rounded-md" style={{ borderColor: tokens.border, backgroundColor: tokens.card }}>
          <table className="w-full text-sm">
            <thead style={{ backgroundColor: tokens.cardHover, color: tokens.textSecondary }}>
              <tr>
                <th className="text-right px-4 py-3 font-semibold">الفترة</th>
                <th className="text-right px-4 py-3 font-semibold">البداية</th>
                <th className="text-right px-4 py-3 font-semibold">النهاية</th>
                <th className="text-right px-4 py-3 font-semibold">الحالة</th>
                <th className="text-right px-4 py-3 font-semibold">قيود الإقفال</th>
                <th className="text-right px-4 py-3 font-semibold">أُقفلت في</th>
                <th className="w-12" />
              </tr>
            </thead>
            <tbody>
              {periods.map((period) => (
                <tr key={period.id} className="border-t" style={{ borderColor: tokens.border }}>
                  <td className="px-4 py-3 font-semibold" style={{ color: tokens.textPrimary }}>{period.name}</td>
                  <td className="px-4 py-3" dir="ltr">{period.startDate}</td>
                  <td className="px-4 py-3" dir="ltr">{period.endDate}</td>
                  <td className="px-4 py-3"><StatusBadge status={period.status} /></td>
                  <td className="px-4 py-3">{period.closingEntryCount}</td>
                  <td className="px-4 py-3">{period.closedAt ? safeFormatDateTime(period.closedAt) : "-"}</td>
                  <td className="px-3 py-2">
                    {period.status === "Open" && (
                      <button
                        type="button"
                        onClick={() => setPeriodToClose(period)}
                        className="w-8 h-8 rounded-md flex items-center justify-center"
                        style={{ color: tokens.dangerBorder }}
                        title={period.isAnnual ? "إقفال السنة وفتح التالية" : "إقفال الفترة"}
                      >
                        <LockKeyhole className="w-4 h-4" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal open={showCreate} onClose={() => setShowCreate(false)} title="إنشاء فترة مالية">
        <div className="space-y-4">
          <div>
            <label style={labelStyle}>اسم الفترة</label>
            <input value={name} onChange={(event) => setName(event.target.value)} style={inputStyle} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label style={labelStyle}>تاريخ البداية</label>
              <input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>تاريخ النهاية</label>
              <input type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} style={inputStyle} />
            </div>
          </div>
          <div>
            <label style={labelStyle}>ملاحظات</label>
            <input value={notes} onChange={(event) => setNotes(event.target.value)} style={inputStyle} />
          </div>
          <div className="flex gap-3 pt-3 border-t" style={{ borderColor: tokens.border }}>
            <button type="button" onClick={() => setShowCreate(false)} style={btnGhost}>إلغاء</button>
            <button type="button" onClick={() => void createPeriod()} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>
              إنشاء الفترة
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!periodToClose}
        onClose={() => setPeriodToClose(null)}
        onConfirm={() => void closePeriod()}
        title={periodToClose?.isAnnual ? "إقفال السنة المالية" : "إقفال الفترة المالية"}
        message={periodToClose?.isAnnual
          ? "سيتم إنشاء قيود إقفال مستقلة لكل عملة، وترحيل صافي النتيجة إلى حقوق الملكية، ثم فتح السنة التالية."
          : "سيتم منع أي قيد جديد بتاريخ يقع داخل هذه الفترة."}
        confirmLabel={submitting ? "جارٍ الإقفال..." : "تأكيد الإقفال"}
        danger
      />
    </div>
  );
}
