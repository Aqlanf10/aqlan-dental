"use client";
import { useState } from "react";
import { Plus, Activity } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface PerioRecord {
  id?: string;
  patientId: string;
  toothNumber: number;
  probingDepth: number;   // mm
  clinicalAttachment: number; // mm
  bleedingOnProbing: boolean;
  plaqueIndex: number;   // 0-3
  gingivalIndex: number; // 0-3
  furcation: number;     // 0-3
  mobility: number;      // 0-3
  notes?: string;
}

interface PerioAssessmentProps {
  patientId: string;
  existingRecords?: PerioRecord[];
  onSave: (record: PerioRecord) => void;
}

export function PerioAssessment({ patientId, existingRecords, onSave }: PerioAssessmentProps) {
  const [toothNumber, setToothNumber] = useState("");
  const [probing, setProbing] = useState("");
  const [attachment, setAttachment] = useState("");
  const [bleeding, setBleeding] = useState(false);
  const [plaque, setPlaque] = useState(0);
  const [gingival, setGingival] = useState(0);
  const [furcation, setFurcation] = useState(0);
  const [mobility, setMobility] = useState(0);
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [records, setRecords] = useState<PerioRecord[]>(existingRecords || []);

  const handleSave = async () => {
    if (!toothNumber) return;
    setSaving(true);
    try {
      const { data } = await api.post("/api/general/perio", {
        patientId,
        toothNumber: parseInt(toothNumber),
        probingDepth: parseFloat(probing) || 0,
        clinicalAttachment: parseFloat(attachment) || 0,
        bleedingOnProbing: bleeding,
        plaqueIndex: plaque,
        gingivalIndex: gingival,
        furcation,
        mobility,
        notes: notes || undefined,
      });
      const newRecord = { ...data, patientId };
      setRecords((prev) => [newRecord, ...prev]);
      onSave(newRecord);
      // Reset form
      setToothNumber(""); setProbing(""); setAttachment(""); setBleeding(false);
      setPlaque(0); setGingival(0); setFurcation(0); setMobility(0); setNotes("");
    } catch {
      // Error handling
    } finally {
      setSaving(false);
    }
  };

  const IndexButton = ({ value, selected, onClick, label }: { value: number; selected: number; onClick: () => void; label: string }) => (
    <button
      onClick={onClick}
      className={cn(
        "w-10 h-10 text-xs font-bold rounded-lg border transition",
        selected === value ? "border-clinic-blue bg-clinic-blue-50 text-clinic-blue" : "border-gray-200 text-gray-500 hover:bg-gray-50"
      )}
      title={label}
    >
      {value}
    </button>
  );

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <h3 className="font-bold text-gray-900 text-sm mb-3 flex items-center gap-2">
          <Activity className="w-4 h-4 text-clinic-blue" />
          تقييم لثة ونسج داعمة
        </h3>

        <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">رقم السن</label>
            <input type="number" value={toothNumber} onChange={(e) => setToothNumber(e.target.value)}
              className="w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
              placeholder="FDI" dir="ltr" />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">عمق الجيب (مم)</label>
            <input type="number" value={probing} onChange={(e) => setProbing(e.target.value)}
              className="w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
              placeholder="0" dir="ltr" step="0.5" />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">الارتباط السريري (مم)</label>
            <input type="number" value={attachment} onChange={(e) => setAttachment(e.target.value)}
              className="w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
              placeholder="0" dir="ltr" step="0.5" />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">نزيف عند الجس</label>
            <button onClick={() => setBleeding(!bleeding)}
              className={cn("w-full py-1.5 text-sm rounded-lg border transition",
                bleeding ? "border-red-500 bg-red-50 text-red-700 font-medium" : "border-gray-200 text-gray-500")}>
              {bleeding ? "نعم" : "لا"}
            </button>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">مؤشر اللويحة</label>
            <div className="flex gap-1">
              {[0, 1, 2, 3].map((v) => (
                <IndexButton key={v} value={v} selected={plaque} onClick={() => setPlaque(v)} label={["لا يوجد", "حافة اللثة", "تاج السن", "شامل"][v]} />
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">مؤشر اللثة</label>
            <div className="flex gap-1">
              {[0, 1, 2, 3].map((v) => (
                <IndexButton key={v} value={v} selected={gingival} onClick={() => setGingival(v)} label={["طبيعي", "التهاب خفيف", "التهاب متوسط", "التهاب شديد"][v]} />
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">تشعب الجذور</label>
            <div className="flex gap-1">
              {[0, 1, 2, 3].map((v) => (
                <IndexButton key={v} value={v} selected={furcation} onClick={() => setFurcation(v)} label={["لا يوجد", "درجة 1", "درجة 2", "درجة 3"][v]} />
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">حركة السن</label>
            <div className="flex gap-1">
              {[0, 1, 2, 3].map((v) => (
                <IndexButton key={v} value={v} selected={mobility} onClick={() => setMobility(v)} label={["ثابت", "أقل من 1مم", "1-2مم", "أكثر من 2مم"][v]} />
              ))}
            </div>
          </div>

          <div className="col-span-2 md:col-span-3">
            <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
            <input value={notes} onChange={(e) => setNotes(e.target.value)}
              className="w-full px-2 py-1.5 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
              placeholder="ملاحظات إضافية..." />
          </div>
        </div>

        <div className="flex justify-end mt-3">
          <button onClick={handleSave} disabled={saving || !toothNumber}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:bg-clinic-navy-700 disabled:opacity-60">
            <Plus className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "إضافة تسجيل"}
          </button>
        </div>
      </div>

      {/* Records Table */}
      {records.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100">
            <h4 className="text-sm font-bold text-gray-900">سجلات التقييم ({records.length})</h4>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["السن", "الجيب", "الارتباط", "نزيف", "لويحة", "لثة", "تشعب", "حركة"].map((h) => (
                    <th key={h} className="text-start px-3 py-2 font-semibold text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {records.map((r, i) => (
                  <tr key={i} className="hover:bg-gray-50">
                    <td className="px-3 py-2 font-mono font-bold">{r.toothNumber}</td>
                    <td className="px-3 py-2">{r.probingDepth}مم</td>
                    <td className="px-3 py-2">{r.clinicalAttachment}مم</td>
                    <td className="px-3 py-2">{r.bleedingOnProbing ? "🔴" : "—"}</td>
                    <td className="px-3 py-2">{r.plaqueIndex}</td>
                    <td className="px-3 py-2">{r.gingivalIndex}</td>
                    <td className="px-3 py-2">{r.furcation}</td>
                    <td className="px-3 py-2">{r.mobility}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
