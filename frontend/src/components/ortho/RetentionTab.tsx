"use client";
import { useState, useEffect } from "react";
import { Save, Plus, CheckCircle, Clock, AlertTriangle } from "lucide-react";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import { cn } from "@/lib/utils";

interface RetentionData {
  id?: string;
  debondDate?: string;
  upperRetainer?: string;
  lowerRetainer?: string;
  instructions?: string;
  status?: string;
  visits?: RetentionVisitData[];
}

interface RetentionVisitData {
  id: string;
  visitDate: string;
  period?: string;
  toothStability?: string;
  retainerStatus?: string;
  notes?: string;
}

const RETAINER_TYPES = [
  "Hawley", "Essix", "Fixed (Bonded)", "Vivera", "Zendura", "أخرى"
];

const STABILITY_OPTIONS = [
  { value: "stable", label: "مستقر", color: "text-green-600 bg-green-50" },
  { value: "slight_movement", label: "حركة طفيفة", color: "text-yellow-600 bg-yellow-50" },
  { value: "relapse", label: "انتكاس", color: "text-red-600 bg-red-50" },
];

const RETAINER_STATUS_OPTIONS = [
  { value: "good", label: "جيد", color: "text-green-600 bg-green-50" },
  { value: "worn", label: "متآكل", color: "text-yellow-600 bg-yellow-50" },
  { value: "broken", label: "مكسور", color: "text-red-600 bg-red-50" },
  { value: "lost", label: "مفقود", color: "text-red-600 bg-red-50" },
];

const PERIOD_OPTIONS = ["شهر واحد", "3 أشهر", "6 أشهر", "سنة", "سنتان"];

interface Props {
  caseId: string;
}

export function RetentionTab({ caseId }: Props) {
  const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

  const [retention, setRetention] = useState<RetentionData | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [showVisitForm, setShowVisitForm] = useState(false);
  const [visitSaving, setVisitSaving] = useState(false);

  // Visit form state
  const [visitForm, setVisitForm] = useState({
    visitDate: new Date().toISOString().slice(0, 10),
    period: "",
    toothStability: "",
    retainerStatus: "",
    notes: "",
  });

  useEffect(() => {
    api.get<RetentionData | null>(`/api/ortho-cases/${caseId}/retention`)
      .then((r) => setRetention(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [caseId]);

  const handleCreate = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const { data } = await api.post(`/api/ortho-cases/${caseId}/retention`, {
        debondDate: retention?.debondDate,
        upperRetainer: retention?.upperRetainer,
        lowerRetainer: retention?.lowerRetainer,
        instructions: retention?.instructions,
      });
      setRetention((prev) => prev ? { ...prev, id: data.id } : null);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  const handleUpdate = async () => {
    setSaving(true);
    setSaved(false);
    try {
      await api.put(`/api/ortho-cases/${caseId}/retention`, {
        debondDate: retention?.debondDate,
        upperRetainer: retention?.upperRetainer,
        lowerRetainer: retention?.lowerRetainer,
        instructions: retention?.instructions,
        status: retention?.status,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  const handleAddVisit = async () => {
    setVisitSaving(true);
    try {
      const { data: newVisit } = await api.post(
        `/api/ortho-cases/${caseId}/retention/visits`,
        visitForm
      );
      setRetention((prev) =>
        prev
          ? {
              ...prev,
              visits: [newVisit, ...(prev.visits ?? [])],
            }
          : prev
      );
      setVisitForm({
        visitDate: new Date().toISOString().slice(0, 10),
        period: "",
        toothStability: "",
        retainerStatus: "",
        notes: "",
      });
      setShowVisitForm(false);
    } catch {
    } finally {
      setVisitSaving(false);
    }
  };

  if (loading) return <div className="animate-pulse h-64 bg-gray-100 rounded-xl" />;

  // No retention record yet — show creation form
  if (!retention?.id) {
    return (
      <div className="space-y-5">
        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4">
          <div className="flex items-center gap-2 text-amber-800">
            <AlertTriangle className="w-5 h-5" />
            <p className="text-sm font-medium">لم يتم إنشاء سجل الاحتفاظ بعد</p>
          </div>
          <p className="text-xs text-amber-600 mt-1">
            سجّل بيانات فك التقويم والجهاز الاحتفاظي لبدء مرحلة المتابعة
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">تاريخ فك التقويم (Debond)</label>
            <input
              type="date"
              className={inputCls}
              value={retention?.debondDate ?? ""}
              onChange={(e) => setRetention((prev) => ({ ...prev, debondDate: e.target.value }))}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الجهاز الاحتفاظي العلوي</label>
            <select
              className={inputCls}
              value={retention?.upperRetainer ?? ""}
              onChange={(e) => setRetention((prev) => ({ ...prev, upperRetainer: e.target.value || undefined }))}
            >
              <option value="">اختر...</option>
              {RETAINER_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الجهاز الاحتفاظي السفلي</label>
            <select
              className={inputCls}
              value={retention?.lowerRetainer ?? ""}
              onChange={(e) => setRetention((prev) => ({ ...prev, lowerRetainer: e.target.value || undefined }))}
            >
              <option value="">اختر...</option>
              {RETAINER_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div className="md:col-span-2">
            <label className="block text-xs font-medium text-gray-500 mb-1">تعليمات الاحتفاظ</label>
            <textarea
              rows={3}
              className={inputCls}
              value={retention?.instructions ?? ""}
              onChange={(e) => setRetention((prev) => ({ ...prev, instructions: e.target.value || undefined }))}
              placeholder="تعليمات ارتداء الجهاز الاحتفاظي، المدة، المتابعة..."
            />
          </div>
        </div>

        <button
          onClick={handleCreate}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جاري الإنشاء..." : "إنشاء سجل الاحتفاظ"}
        </button>
      </div>
    );
  }

  // Retention record exists — show details + visits
  return (
    <div className="space-y-6">
      {/* Retention Info */}
      <div className="bg-white rounded-xl border border-green-200 shadow-sm p-5">
        <div className="flex items-center gap-2 mb-4">
          <CheckCircle className="w-5 h-5 text-green-500" />
          <h3 className="font-semibold text-gray-900">سجل الاحتفاظ</h3>
          <span className={cn(
            "text-xs px-2 py-0.5 rounded-full font-medium",
            retention.status === "active" ? "bg-green-50 text-green-700" : "bg-gray-100 text-gray-500"
          )}>
            {retention.status === "active" ? "نشط" : retention.status === "completed" ? "مكتمل" : retention.status}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">تاريخ فك التقويم</label>
            <input
              type="date"
              className={inputCls}
              value={retention.debondDate ?? ""}
              onChange={(e) => {
                setRetention((prev) => prev ? { ...prev, debondDate: e.target.value } : prev);
                setSaved(false);
              }}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الجهاز العلوي</label>
            <select
              className={inputCls}
              value={retention.upperRetainer ?? ""}
              onChange={(e) => {
                setRetention((prev) => prev ? { ...prev, upperRetainer: e.target.value || undefined } : prev);
                setSaved(false);
              }}
            >
              <option value="">اختر...</option>
              {RETAINER_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">الجهاز السفلي</label>
            <select
              className={inputCls}
              value={retention.lowerRetainer ?? ""}
              onChange={(e) => {
                setRetention((prev) => prev ? { ...prev, lowerRetainer: e.target.value || undefined } : prev);
                setSaved(false);
              }}
            >
              <option value="">اختر...</option>
              {RETAINER_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div className="md:col-span-2">
            <label className="block text-xs font-medium text-gray-500 mb-1">تعليمات</label>
            <textarea
              rows={2}
              className={inputCls}
              value={retention.instructions ?? ""}
              onChange={(e) => {
                setRetention((prev) => prev ? { ...prev, instructions: e.target.value || undefined } : prev);
                setSaved(false);
              }}
            />
          </div>
        </div>

        <div className="flex items-center gap-3 pt-3 border-t border-gray-100 mt-4">
          <button onClick={handleUpdate} disabled={saving}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جاري الحفظ..." : "حفظ التعديلات"}
          </button>
          {saved && <span className="text-sm text-teal-600 font-medium">تم الحفظ بنجاح</span>}
        </div>
      </div>

      {/* Retention Visits */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-semibold text-gray-900">زيارات المتابعة</h3>
          <button
            onClick={() => setShowVisitForm(!showVisitForm)}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Plus className="w-4 h-4" />
            زيارة متابعة
          </button>
        </div>

        {/* New Visit Form */}
        {showVisitForm && (
          <div className="bg-teal-50 rounded-xl border border-teal-200 p-4 mb-4 space-y-3">
            <h4 className="text-sm font-semibold text-teal-900">تسجيل زيارة متابعة</h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">التاريخ *</label>
                <input
                  type="date"
                  className={inputCls}
                  value={visitForm.visitDate}
                  onChange={(e) => setVisitForm((f) => ({ ...f, visitDate: e.target.value }))}
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">الفترة</label>
                <select
                  className={inputCls}
                  value={visitForm.period}
                  onChange={(e) => setVisitForm((f) => ({ ...f, period: e.target.value }))}
                >
                  <option value="">اختر...</option>
                  {PERIOD_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">استقرار الأسنان</label>
                <select
                  className={inputCls}
                  value={visitForm.toothStability}
                  onChange={(e) => setVisitForm((f) => ({ ...f, toothStability: e.target.value }))}
                >
                  <option value="">اختر...</option>
                  {STABILITY_OPTIONS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">حالة الجهاز</label>
                <select
                  className={inputCls}
                  value={visitForm.retainerStatus}
                  onChange={(e) => setVisitForm((f) => ({ ...f, retainerStatus: e.target.value }))}
                >
                  <option value="">اختر...</option>
                  {RETAINER_STATUS_OPTIONS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
                </select>
              </div>
              <div className="md:col-span-2">
                <label className="block text-xs font-medium text-gray-700 mb-1">ملاحظات</label>
                <textarea
                  rows={2}
                  className={inputCls}
                  value={visitForm.notes}
                  onChange={(e) => setVisitForm((f) => ({ ...f, notes: e.target.value }))}
                />
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <button onClick={() => setShowVisitForm(false)}
                className="px-4 py-1.5 text-sm border border-gray-300 rounded-lg hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
              <button onClick={handleAddVisit} disabled={visitSaving}
                className="px-4 py-1.5 text-sm bg-clinic-teal text-white rounded-lg hover:opacity-90 disabled:opacity-60 transition"
              >
                {visitSaving ? "جاري الحفظ..." : "حفظ الزيارة"}
              </button>
            </div>
          </div>
        )}

        {/* Visits List */}
        {(!retention.visits || retention.visits.length === 0) ? (
          <div className="text-center py-10 text-gray-400 text-sm">
            <Clock className="w-10 h-10 mx-auto mb-2 opacity-30" />
            لا توجد زيارات متابعة مسجلة
          </div>
        ) : (
          <div className="space-y-2">
            {retention.visits.map((visit) => {
              const stability = STABILITY_OPTIONS.find((s) => s.value === visit.toothStability);
              const retStatus = RETAINER_STATUS_OPTIONS.find((s) => s.value === visit.retainerStatus);
              return (
                <div key={visit.id} className="bg-white border border-gray-200 rounded-lg px-4 py-3">
                  <div className="flex items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <span className="text-sm font-semibold text-gray-900">
                        {formatArabicDate(visit.visitDate)}
                      </span>
                      {visit.period && (
                        <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded">
                          {visit.period}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      {stability && (
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${stability.color}`}>
                          {stability.label}
                        </span>
                      )}
                      {retStatus && (
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${retStatus.color}`}>
                          {retStatus.label}
                        </span>
                      )}
                    </div>
                  </div>
                  {visit.notes && (
                    <p className="text-xs text-gray-500 mt-1.5">{visit.notes}</p>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
