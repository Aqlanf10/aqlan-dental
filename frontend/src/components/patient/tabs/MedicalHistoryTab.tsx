"use client";

import { useEffect, useState } from "react";
import { FileText, Pencil, Save, X } from "lucide-react";
import type { MedicalHistory } from "@/types/patient";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface MedicalHistoryTabProps {
  patientId: string;
  initialData?: MedicalHistory;
}

export function MedicalHistoryTab({ patientId, initialData }: MedicalHistoryTabProps) {
  const [data, setData] = useState<MedicalHistory | null>(initialData ?? null);
  const [loading, setLoading] = useState(!initialData);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  // Edit form state
  const [form, setForm] = useState<Partial<MedicalHistory>>({});

  useEffect(() => {
    if (!initialData) {
      setFetchError(false);
      api.get<MedicalHistory>(`/api/patients/${patientId}/medical-history`)
        .then((r) => setData(r.data))
        .catch(() => { setFetchError(true); })
        .finally(() => setLoading(false));
    }
  }, [patientId, initialData, retryKey]);

  const startEdit = () => {
    if (data) {
      setForm({ ...data });
    } else {
      setForm({
        chronicDiseases: "",
        currentMedications: "",
        drugAllergies: "",
        bleedingDisorders: false,
        isPregnant: "na",
        tmjProblems: false,
        previousSurgeries: "",
        notes: "",
      });
    }
    setEditing(true);
  };

  const cancelEdit = () => {
    setEditing(false);
    setForm({});
  };

  const saveEdit = async () => {
    setSaving(true);
    try {
      await api.put(`/api/patients/${patientId}/medical-history`, form);
      setData({ ...form } as MedicalHistory);
      setEditing(false);
      setForm({});
      toast.success("تم حفظ التاريخ الطبي بنجاح");
    } catch {
      toast.error("فشل حفظ التاريخ الطبي");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-12 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (editing) {
    return (
      <div className="space-y-4" dir="rtl">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-[#0d2137] flex items-center gap-2">
            <FileText className="w-4 h-4 text-clinic-blue" />
            تعديل التاريخ الطبي
          </h3>
          <div className="flex gap-2">
            <button onClick={cancelEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-[#e8f0f9] rounded-lg hover:bg-[#f7fafd] text-[#64748b]">
              <X className="w-3.5 h-3.5" />
              إلغاء
            </button>
            <button onClick={saveEdit} disabled={saving} className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50">
              <Save className="w-3.5 h-3.5" />
              {saving ? "جارٍ الحفظ..." : "حفظ"}
            </button>
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="text-xs text-[#64748b] block mb-1">الأمراض المزمنة</label>
            <textarea value={form.chronicDiseases ?? ""} onChange={(e) => setForm({ ...form, chronicDiseases: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue" rows={2} />
          </div>
          <div>
            <label className="text-xs text-[#64748b] block mb-1">الأدوية الحالية</label>
            <textarea value={form.currentMedications ?? ""} onChange={(e) => setForm({ ...form, currentMedications: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue" rows={2} />
          </div>
          <div>
            <label className="text-xs text-[#64748b] block mb-1">حساسية الأدوية</label>
            <textarea value={form.drugAllergies ?? ""} onChange={(e) => setForm({ ...form, drugAllergies: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue" rows={2} />
          </div>
          <div>
            <label className="text-xs text-[#64748b] block mb-1">العمليات السابقة</label>
            <textarea value={form.previousSurgeries ?? ""} onChange={(e) => setForm({ ...form, previousSurgeries: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue" rows={2} />
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-[#0d2137]">اضطرابات النزيف</label>
            <input type="checkbox" checked={form.bleedingDisorders ?? false} onChange={(e) => setForm({ ...form, bleedingDisorders: e.target.checked })} className="w-4 h-4 accent-clinic-blue" />
          </div>
          <div>
            <label className="text-xs text-[#64748b] block mb-1">الحمل</label>
            <select value={form.isPregnant ?? "na"} onChange={(e) => setForm({ ...form, isPregnant: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue bg-white">
              <option value="na">لا ينطبق</option>
              <option value="yes">نعم</option>
              <option value="no">لا</option>
            </select>
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-[#0d2137]">مشاكل TMJ</label>
            <input type="checkbox" checked={form.tmjProblems ?? false} onChange={(e) => setForm({ ...form, tmjProblems: e.target.checked })} className="w-4 h-4 accent-clinic-blue" />
          </div>
          <div className="md:col-span-2">
            <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
            <textarea value={form.notes ?? ""} onChange={(e) => setForm({ ...form, notes: e.target.value })} className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue" rows={2} />
          </div>
        </div>
      </div>
    );
  }

  if (fetchError) {
    return (
      <div className="p-4 text-center" dir="rtl">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="text-center py-12" dir="rtl">
        <FileText className="w-10 h-10 mx-auto mb-2 text-[#cbd5e1]" />
        <p className="text-sm text-[#94a3b8] mb-3">لا يوجد تاريخ طبي مسجّل</p>
        <button onClick={startEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 mx-auto">
          <Pencil className="w-3.5 h-3.5" />
          إضافة تاريخ طبي
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-3" dir="rtl">
      <div className="flex justify-end">
        <button onClick={startEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-[#e8f0f9] rounded-lg hover:bg-[#f7fafd] text-[#64748b]">
          <Pencil className="w-3.5 h-3.5" />
          تعديل
        </button>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {[
          ["الأمراض المزمنة", data.chronicDiseases],
          ["الأدوية الحالية", data.currentMedications],
          ["حساسية الأدوية", data.drugAllergies],
          ["اضطرابات النزيف", data.bleedingDisorders ? "نعم" : "لا"],
          ["الحمل", data.isPregnant === "yes" ? "نعم" : data.isPregnant === "no" ? "لا" : "لا ينطبق"],
          ["مشاكل TMJ", data.tmjProblems ? "نعم" : "لا"],
          ["العمليات السابقة", data.previousSurgeries],
          ["ملاحظات", data.notes],
        ].map(([label, value]) => (
          <div key={label} className="border-b border-[#f1f5f9] pb-3">
            <p className="text-xs text-[#94a3b8] mb-0.5">{label}</p>
            <p className="text-sm font-medium text-[#0d2137]">{value ?? "—"}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
