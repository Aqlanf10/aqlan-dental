"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Save } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import { STUDY_TYPES, type RadiologyStudyType } from "@/types/radiologyOrder";

interface Props {
  defaultPatientId?: string;
  defaultPatientName?: string;
}

// Spec 010 (RX-REQ-001): create an outgoing imaging order (OPG/CBCT/…) that
// prints as an English referral the patient carries to the radiology center.
export function RadiologyOrderForm({ defaultPatientId, defaultPatientName }: Props) {
  const router = useRouter();
  const [patientId, setPatientId] = useState(defaultPatientId ?? "");
  const [studyType, setStudyType] = useState<RadiologyStudyType>("panoramic");
  const [region, setRegion] = useState("");
  const [clinicalNotes, setClinicalNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!patientId) {
      setError("يرجى اختيار المريض");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const { data: created } = await api.post<{ id: string }>("/api/radiology-orders", {
        patientId,
        studyType,
        region: region.trim() || undefined,
        clinicalNotes: clinicalNotes.trim() || undefined,
      });
      router.push(`/radiology-orders/${created.id}`);
    } catch (err) {
      setError(extractErrorMessage(err, "تعذر حفظ طلب الأشعة"));
      setSaving(false);
    }
  };

  return (
    <form onSubmit={onSubmit} className="bg-white rounded-xl border border-gray-200 shadow-sm p-6 space-y-5">
      {error && (
        <div role="alert" className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-semibold text-gray-700 mb-1.5">المريض *</label>
        <PatientCombobox
          defaultDisplayValue={defaultPatientName}
          readOnly={!!defaultPatientId}
          onSelect={(p) => setPatientId(p.id)}
        />
      </div>

      <div>
        <label className="block text-sm font-semibold text-gray-700 mb-1.5">نوع الأشعة المطلوبة *</label>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {STUDY_TYPES.map((t) => (
            <label
              key={t.value}
              className={`flex items-center gap-2.5 border rounded-lg px-3 py-2.5 cursor-pointer transition text-sm ${
                studyType === t.value
                  ? "border-clinic-blue bg-blue-50 font-semibold text-gray-900"
                  : "border-gray-200 hover:bg-gray-50 text-gray-700"
              }`}
            >
              <input
                type="radio"
                name="studyType"
                value={t.value}
                checked={studyType === t.value}
                onChange={() => setStudyType(t.value)}
                className="accent-blue-600"
              />
              {t.labelAr}
            </label>
          ))}
        </div>
      </div>

      <div>
        <label htmlFor="rx-region" className="block text-sm font-semibold text-gray-700 mb-1.5">
          منطقة التصوير <span className="text-gray-400 font-normal">(اختياري — مثال: الفك العلوي الأيمن، الأسنان 14-16)</span>
        </label>
        <input
          id="rx-region"
          value={region}
          onChange={(e) => setRegion(e.target.value)}
          maxLength={200}
          className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue/30"
          placeholder="تُطبع بالإنجليزية على الإحالة إن أدخلتها بالإنجليزية"
        />
      </div>

      <div>
        <label htmlFor="rx-notes" className="block text-sm font-semibold text-gray-700 mb-1.5">
          معلومات سريرية لمركز الأشعة <span className="text-gray-400 font-normal">(اختياري)</span>
        </label>
        <textarea
          id="rx-notes"
          value={clinicalNotes}
          onChange={(e) => setClinicalNotes(e.target.value)}
          maxLength={1000}
          rows={3}
          className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue/30"
        />
      </div>

      <div className="flex justify-end pt-1">
        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2.5 text-sm font-semibold rounded-lg bg-clinic-blue text-white hover:opacity-90 transition disabled:opacity-50"
        >
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          حفظ وعرض الإحالة
        </button>
      </div>
    </form>
  );
}
