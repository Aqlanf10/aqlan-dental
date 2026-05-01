"use client";

import { useEffect, useState } from "react";
import { Stethoscope, Pencil, Save, X } from "lucide-react";
import type { DentalHistory } from "@/types/patient";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface DentalHistoryTabProps {
  patientId: string;
  initialData?: DentalHistory;
}

export function DentalHistoryTab({ patientId, initialData }: DentalHistoryTabProps) {
  const [data, setData] = useState<DentalHistory | null>(initialData ?? null);
  const [loading, setLoading] = useState(!initialData);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  const [form, setForm] = useState<Partial<DentalHistory>>({});

  useEffect(() => {
    if (!initialData) {
      api.get<DentalHistory>(`/api/patients/${patientId}/dental-history`)
        .then((r) => setData(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    }
  }, [patientId, initialData]);

  const startEdit = () => {
    if (data) {
      setForm({ ...data });
    } else {
      setForm({
        chiefComplaint: "",
        previousTreatments: "",
        mouthBreathing: false,
        bruxism: false,
        thumbSucking: false,
        tongueThrusing: false,
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
      await api.put(`/api/patients/${patientId}/dental-history`, form);
      setData({ ...form } as DentalHistory);
      setEditing(false);
      setForm({});
      toast.success("تم حفظ التاريخ السني بنجاح");
    } catch {
      toast.error("فشل حفظ التاريخ السني");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-12 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (editing) {
    return (
      <div className="space-y-4" dir="rtl">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
            <Stethoscope className="w-4 h-4 text-clinic-teal" />
            تعديل التاريخ السني
          </h3>
          <div className="flex gap-2">
            <button onClick={cancelEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">
              <X className="w-3.5 h-3.5" />
              إلغاء
            </button>
            <button onClick={saveEdit} disabled={saving} className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-50">
              <Save className="w-3.5 h-3.5" />
              {saving ? "جارٍ الحفظ..." : "حفظ"}
            </button>
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="md:col-span-2">
            <label className="text-xs text-gray-500 block mb-1">الشكوى الرئيسية</label>
            <textarea value={form.chiefComplaint ?? ""} onChange={(e) => setForm({ ...form, chiefComplaint: e.target.value })} className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal" rows={2} />
          </div>
          <div className="md:col-span-2">
            <label className="text-xs text-gray-500 block mb-1">العلاجات السابقة</label>
            <textarea value={form.previousTreatments ?? ""} onChange={(e) => setForm({ ...form, previousTreatments: e.target.value })} className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal" rows={2} />
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-gray-700">التنفس الفموي</label>
            <input type="checkbox" checked={form.mouthBreathing ?? false} onChange={(e) => setForm({ ...form, mouthBreathing: e.target.checked })} className="w-4 h-4 accent-clinic-teal" />
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-gray-700">صرير الأسنان</label>
            <input type="checkbox" checked={form.bruxism ?? false} onChange={(e) => setForm({ ...form, bruxism: e.target.checked })} className="w-4 h-4 accent-clinic-teal" />
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-gray-700">مص الإبهام</label>
            <input type="checkbox" checked={form.thumbSucking ?? false} onChange={(e) => setForm({ ...form, thumbSucking: e.target.checked })} className="w-4 h-4 accent-clinic-teal" />
          </div>
          <div className="flex items-center gap-3">
            <label className="text-sm text-gray-700">وضع اللسان الخاطئ</label>
            <input type="checkbox" checked={form.tongueThrusing ?? false} onChange={(e) => setForm({ ...form, tongueThrusing: e.target.checked })} className="w-4 h-4 accent-clinic-teal" />
          </div>
          <div className="md:col-span-2">
            <label className="text-xs text-gray-500 block mb-1">ملاحظات</label>
            <textarea value={form.notes ?? ""} onChange={(e) => setForm({ ...form, notes: e.target.value })} className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal" rows={2} />
          </div>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="text-center py-12" dir="rtl">
        <Stethoscope className="w-10 h-10 mx-auto mb-2 text-gray-300" />
        <p className="text-sm text-gray-400 mb-3">لا يوجد تاريخ سني مسجّل</p>
        <button onClick={startEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 mx-auto">
          <Pencil className="w-3.5 h-3.5" />
          إضافة تاريخ سني
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-3" dir="rtl">
      <div className="flex justify-end">
        <button onClick={startEdit} className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">
          <Pencil className="w-3.5 h-3.5" />
          تعديل
        </button>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {[
          ["الشكوى الرئيسية", data.chiefComplaint],
          ["العلاجات السابقة", data.previousTreatments],
          ["التنفس الفموي", data.mouthBreathing ? "نعم" : "لا"],
          ["صرير الأسنان", data.bruxism ? "نعم" : "لا"],
          ["مص الإبهام", data.thumbSucking ? "نعم" : "لا"],
          ["وضع اللسان الخاطئ", data.tongueThrusing ? "نعم" : "لا"],
          ["ملاحظات", data.notes],
        ].map(([label, value]) => (
          <div key={label} className="border-b border-gray-50 pb-3">
            <p className="text-xs text-gray-400 mb-0.5">{label}</p>
            <p className="text-sm font-medium text-gray-900">{value ?? "—"}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
