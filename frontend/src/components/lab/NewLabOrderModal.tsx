"use client";

import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { X, Search } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { CreateLabOrderRequest, LabOrderPriority } from "@/types/lab";
import type { PatientListItem } from "@/types/patient";
import { cn } from "@/lib/utils";

interface Props {
  onClose: () => void;
}

export function NewLabOrderModal({ onClose }: Props) {
  const queryClient = useQueryClient();
  const [patientSearch, setPatientSearch] = useState("");
  const [selectedPatient, setSelectedPatient] = useState<PatientListItem | null>(null);
  const [form, setForm] = useState<Omit<CreateLabOrderRequest, "patientId">>({
    applianceType: "",
    labName: "",
    sentDate: "",
    expectedDate: "",
    priority: "normal",
    instructions: "",
  });

  const { data: patients } = useQuery({
    queryKey: ["patients-search", patientSearch],
    queryFn: async () => {
      if (patientSearch.trim().length < 2) return [];
      const res = await api.get<{ data: PatientListItem[] }>(
        `/api/patients?search=${encodeURIComponent(patientSearch)}&pageSize=8`
      );
      return res.data.data;
    },
    enabled: patientSearch.trim().length >= 2,
  });

  const mutation = useMutation({
    mutationFn: async (data: CreateLabOrderRequest) => {
      const res = await api.post<{ id: string; orderNumber: string }>("/api/lab-orders", data);
      return res.data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders-pending-count"] });
      toast.success(`تم إنشاء الطلب ${data.orderNumber} بنجاح`);
      onClose();
    },
    onError: () => toast.error("فشل إنشاء الطلب"),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!selectedPatient) return;
    mutation.mutate({
      ...form,
      patientId: selectedPatient.id,
      sentDate: form.sentDate || undefined,
      expectedDate: form.expectedDate || undefined,
    });
  };

  const set = (k: keyof typeof form, v: string) => setForm((f) => ({ ...f, [k]: v }));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-lg font-bold text-gray-900">طلب مختبر جديد</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {/* Patient search */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">المريض *</label>
            {selectedPatient ? (
              <div className="flex items-center justify-between border border-green-300 bg-green-50 rounded-lg px-3 py-2">
                <div>
                  <p className="text-sm font-medium text-gray-900">{selectedPatient.fullName}</p>
                  <p className="text-xs text-gray-500">{selectedPatient.patientNumber}</p>
                </div>
                <button
                  type="button"
                  onClick={() => { setSelectedPatient(null); setPatientSearch(""); }}
                  className="text-gray-400 hover:text-gray-600"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
            ) : (
              <div className="relative">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  value={patientSearch}
                  onChange={(e) => setPatientSearch(e.target.value)}
                  placeholder="ابحث باسم المريض أو رقمه..."
                  className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                />
                {patients && patients.length > 0 && (
                  <ul className="absolute z-10 top-full mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
                    {patients.map((p) => (
                      <li key={p.id}>
                        <button
                          type="button"
                          onClick={() => { setSelectedPatient(p); setPatientSearch(""); }}
                          className="w-full text-right px-3 py-2.5 hover:bg-gray-50 text-sm"
                        >
                          <span className="font-medium">{p.fullName}</span>
                          <span className="text-gray-400 text-xs mr-2">{p.patientNumber}</span>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </div>

          {/* Appliance type */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">نوع الجهاز / الشغل *</label>
            <input
              required
              value={form.applianceType}
              onChange={(e) => set("applianceType", e.target.value)}
              placeholder="مثال: براكيت معدني، أطقم، بريدج..."
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          {/* Lab name */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">اسم المختبر</label>
            <input
              value={form.labName ?? ""}
              onChange={(e) => set("labName", e.target.value)}
              placeholder="اسم المختبر"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          {/* Dates */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">تاريخ الإرسال</label>
              <input
                type="date"
                value={form.sentDate ?? ""}
                onChange={(e) => set("sentDate", e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">تاريخ الاستلام المتوقع</label>
              <input
                type="date"
                value={form.expectedDate ?? ""}
                onChange={(e) => set("expectedDate", e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
          </div>

          {/* Priority */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">الأولوية</label>
            <div className="flex gap-2">
              {(["urgent", "normal", "low"] as LabOrderPriority[]).map((p) => (
                <button
                  key={p}
                  type="button"
                  onClick={() => set("priority", p)}
                  className={cn(
                    "flex-1 py-2 rounded-lg border text-sm font-medium transition-colors",
                    form.priority === p
                      ? "border-cyan-500 bg-cyan-50 text-cyan-700"
                      : "border-gray-200 text-gray-500 hover:border-gray-300"
                  )}
                >
                  {p === "urgent" ? "عاجل" : p === "normal" ? "عادي" : "منخفض"}
                </button>
              ))}
            </div>
          </div>

          {/* Instructions */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">التعليمات</label>
            <textarea
              value={form.instructions ?? ""}
              onChange={(e) => set("instructions", e.target.value)}
              rows={3}
              placeholder="تعليمات إضافية للمختبر..."
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 resize-none"
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={!selectedPatient || !form.applianceType || mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الإنشاء..." : "إنشاء الطلب"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
