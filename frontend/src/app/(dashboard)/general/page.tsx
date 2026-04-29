"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Stethoscope, UserPlus, Plus, X, Save } from "lucide-react";
import type { GeneralTreatment } from "@/types/dental";
import type { PatientListItem } from "@/types/patient";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import { EnhancedDentalChart } from "@/components/dental/EnhancedDentalChart";
import { ToothEditorModal } from "@/components/dental/ToothEditorModal";
import { PerioAssessment } from "@/components/dental/PerioAssessment";
import { TreatmentPlanPanel } from "@/components/dental/TreatmentPlanPanel";

interface Doctor { id: string; name: string; }

const TREATMENT_TYPES = [
  "فحص", "تنظيف", "حشو أمامي", "حشو خلفي", "حشو مؤقت",
  "قلع", "قلع جراحي", "معالجة جذر", "تاج", "جسر", "طقم أسنان كامل",
  "طقم أسنان جزئي", "تبييض", "تقرح فموي", "خلع ضرس عقل", "أخرى",
];
const ANESTHESIA_TYPES = ["موضعي", "عام", "تخدير سطحي", "بدون تخدير"];

const inputCls = (err?: boolean) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

export default function GeneralPage() {
  const [treatments, setTreatments] = useState<GeneralTreatment[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [doctors,    setDoctors]    = useState<Doctor[]>([]);

  // Tab navigation
  const [activeTab, setActiveTab] = useState<"treatments" | "chart" | "perio" | "plan">("treatments");

  // Quick navigator
  const [selectedPatient, setSelectedPatient] = useState<PatientListItem | null>(null);

  // Dental chart state
  const [selectedTooth, setSelectedTooth] = useState<number | null>(null);
  const [chartTeeth, setChartTeeth] = useState<any[]>([]);

  // New treatment form
  const [showForm, setShowForm]   = useState(false);
  const [formPatient, setFormPatient] = useState<PatientListItem | null>(null);
  const [treatmentType, setTreatmentType] = useState("");
  const [toothNumber,   setToothNumber]   = useState("");
  const [material,      setMaterial]      = useState("");
  const [anesthesia,    setAnesthesia]    = useState("");
  const [cost,          setCost]          = useState("");
  const [doctorId,      setDoctorId]      = useState("");
  const [notes,         setNotes]         = useState("");
  const [saving,        setSaving]        = useState(false);
  const [formErr,       setFormErr]       = useState("");

  const fetchTreatments = () => {
    setLoading(true);
    api.get<GeneralTreatment[]>("/api/general/recent-treatments?limit=50")
      .then((r) => setTreatments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchTreatments();
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  const resetForm = () => {
    setFormPatient(null); setTreatmentType(""); setToothNumber("");
    setMaterial(""); setAnesthesia(""); setCost(""); setDoctorId("");
    setNotes(""); setFormErr(""); setShowForm(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formPatient) { setFormErr("اختر مريضاً"); return; }
    if (!treatmentType) { setFormErr("نوع العلاج مطلوب"); return; }
    setSaving(true); setFormErr("");
    try {
      await api.post("/api/general-treatments", {
        patientId:    formPatient.id,
        treatmentType,
        toothNumber:  toothNumber || undefined,
        materialUsed: material    || undefined,
        anesthesiaType: anesthesia || undefined,
        cost:         cost ? parseFloat(cost) : undefined,
        doctorId:     doctorId || undefined,
        notes:        notes    || undefined,
      });
      resetForm();
      fetchTreatments();
    } catch {
      setFormErr("حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">طب الأسنان العام</h1>
          <p className="text-sm text-gray-500 mt-0.5">آخر المعالجات والمخططات السنية</p>
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          {showForm ? <X className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
          {showForm ? "إغلاق" : "تسجيل معالجة"}
        </button>
      </div>

      {/* Tab navigation */}
      <div className="flex gap-2 border-b border-gray-200 mb-5">
        {[
          { key: "treatments" as const, label: "المعالجات" },
          { key: "chart" as const, label: "المخطط السني" },
          { key: "perio" as const, label: "تقييم اللثة" },
          { key: "plan" as const, label: "خطة العلاج" },
        ].map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={cn(
              "px-4 py-2.5 text-sm font-medium border-b-2 transition",
              activeTab === tab.key
                ? "border-teal-600 text-teal-700"
                : "border-transparent text-gray-500 hover:text-gray-700"
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Quick patient navigator */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <p className="text-sm font-medium text-gray-700 mb-2">الانتقال السريع إلى ملف مريض</p>
        <div className="flex items-center gap-3">
          <div className="flex-1">
            <PatientCombobox
              onSelect={(p: PatientListItem) => setSelectedPatient(p)}
              placeholder="ابحث بالاسم أو رقم المريض..."
            />
          </div>
          {selectedPatient && (
            <Link
              href={`/patients/${selectedPatient.id}?tab=chart`}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition whitespace-nowrap"
            >
              <UserPlus className="w-4 h-4" />
              {selectedPatient.fullName}
            </Link>
          )}
        </div>
      </div>

      {activeTab === "treatments" && (<>
      {/* New treatment form */}
      {showForm && (
        <form onSubmit={handleSubmit} className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <h2 className="font-bold text-gray-900 mb-4 flex items-center gap-2">
            <Stethoscope className="w-4 h-4 text-clinic-teal" />
            تسجيل معالجة جديدة
          </h2>

          {formErr && (
            <div className="mb-4 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg p-3">{formErr}</div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Patient */}
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1.5">المريض <span className="text-red-500">*</span></label>
              <PatientCombobox
                onSelect={(p: PatientListItem) => setFormPatient(p)}
                defaultDisplayValue={formPatient ? `${formPatient.fullName} (${formPatient.patientNumber})` : ""}
                error={!formPatient && formErr ? formErr : undefined}
              />
            </div>

            {/* Treatment type */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع العلاج <span className="text-red-500">*</span></label>
              <select value={treatmentType} onChange={(e) => setTreatmentType(e.target.value)}
                className={inputCls(!treatmentType && !!formErr)}>
                <option value="">اختر...</option>
                {TREATMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>

            {/* Tooth number */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">رقم السن (FDI)</label>
              <input value={toothNumber} onChange={(e) => setToothNumber(e.target.value)}
                className={inputCls()} placeholder="مثلاً: 16، 36" dir="ltr" />
            </div>

            {/* Anesthesia */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع التخدير</label>
              <select value={anesthesia} onChange={(e) => setAnesthesia(e.target.value)} className={inputCls()}>
                <option value="">بدون / غير محدد</option>
                {ANESTHESIA_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>

            {/* Material */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">المادة المستخدمة</label>
              <input value={material} onChange={(e) => setMaterial(e.target.value)}
                className={inputCls()} placeholder="مثلاً: كومبوزيت، أملغم..." />
            </div>

            {/* Doctor */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب</label>
              <select value={doctorId} onChange={(e) => setDoctorId(e.target.value)} className={inputCls()}>
                <option value="">اختر...</option>
                {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </div>

            {/* Cost */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">التكلفة (ر.ي)</label>
              <input type="number" value={cost} onChange={(e) => setCost(e.target.value)}
                className={inputCls()} placeholder="0" min="0" dir="ltr" />
            </div>

            {/* Notes */}
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
              <textarea value={notes} onChange={(e) => setNotes(e.target.value)}
                rows={2} className={cn(inputCls(), "resize-none")} placeholder="ملاحظات إضافية..." />
            </div>
          </div>

          <div className="flex justify-end gap-3 mt-4 pt-4 border-t border-gray-100">
            <button type="button" onClick={resetForm}
              className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
              إلغاء
            </button>
            <button type="submit" disabled={saving}
              className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition">
              <Save className="w-4 h-4" />
              {saving ? "جارٍ الحفظ..." : "حفظ المعالجة"}
            </button>
          </div>
        </form>
      )}

      {/* Recent treatments */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر المعالجات</h2>
          <span className="text-xs text-gray-400">{treatments.length} معالجة</span>
        </div>

        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}
          </div>
        ) : treatments.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <Stethoscope className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد معالجات بعد</p>
            <p className="text-xs mt-1">سجّل معالجة جديدة من الزر أعلاه</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["التاريخ", "المريض", "نوع العلاج", "السن", "الطبيب", "التكلفة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {treatments.map((t) => (
                  <tr key={t.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 text-gray-600 text-xs">{formatArabicDate(t.createdAt)}</td>
                    <td className="px-4 py-3 font-medium text-gray-900">{t.patientName}</td>
                    <td className="px-4 py-3 text-gray-700">{t.treatmentType}</td>
                    <td className="px-4 py-3 font-mono text-gray-700">{t.toothNumber ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{t.doctorName ?? "—"}</td>
                    <td className="px-4 py-3 font-mono text-green-700">
                      {t.cost ? formatYemeniRiyal(t.cost) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <Link href={`/patients/${t.patientId}`}
                        className="text-xs text-clinic-teal hover:underline font-medium">
                        ملف المريض
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      </>)}

      {activeTab === "chart" && selectedPatient && (
        <div className="space-y-4">
          <EnhancedDentalChart
            teeth={chartTeeth}
            onToothClick={(num) => setSelectedTooth(num)}
            selectedTooth={selectedTooth}
          />
          {selectedTooth && (
            <ToothEditorModal
              toothNumber={selectedTooth}
              data={chartTeeth.find((t: any) => t.toothNumber === selectedTooth)}
              onSave={async (data) => {
                try {
                  await api.put(`/api/general/${selectedPatient.id}/teeth`, data);
                  // Refresh chart data
                  const { data: newChart } = await api.get(`/api/general/${selectedPatient.id}/chart`);
                  setChartTeeth(newChart?.teeth || []);
                  setSelectedTooth(null);
                } catch {}
              }}
              onClose={() => setSelectedTooth(null)}
            />
          )}
        </div>
      )}

      {activeTab === "perio" && selectedPatient && (
        <PerioAssessment patientId={selectedPatient.id} onSave={() => {}} />
      )}

      {activeTab === "plan" && selectedPatient && (
        <TreatmentPlanPanel patientId={selectedPatient.id} onUpdate={fetchTreatments} />
      )}
    </div>
  );
}
