
import { useState, useEffect } from "react";
import { ClipboardList, Save, Loader2, Lock } from "lucide-react";
import type { JointPlanDto } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

interface JointPlanPanelProps {
  orthoSurgicalCaseId: string;
  jointPlan: JointPlanDto | null;
  canEdit: boolean;
  onSaved: () => void;
}

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] transition";
const textareaCls = `${inputCls} resize-y`;

interface JointPlanForm {
  procedureType: string;
  timing: string;
  orthodonticObjectives: string;
  surgicalObjectives: string;
  preSurgicalRequirements: string;
  postSurgicalPlan: string;
  risks: string;
  patientExplanation: string;
}

const emptyForm: JointPlanForm = {
  procedureType: "", timing: "", orthodonticObjectives: "", surgicalObjectives: "",
  preSurgicalRequirements: "", postSurgicalPlan: "", risks: "", patientExplanation: "",
};

/** Sprint A5 — the joint plan editor. Editable until both sides approve (LockedAt), then read-only. */
export function JointPlanPanel({ orthoSurgicalCaseId, jointPlan, canEdit, onSaved }: JointPlanPanelProps) {
  const locked = !!jointPlan?.lockedAt;
  const [form, setForm] = useState<JointPlanForm>(emptyForm);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setForm({
      procedureType: jointPlan?.procedureType ?? "",
      timing: jointPlan?.timing ?? "",
      orthodonticObjectives: jointPlan?.orthodonticObjectives ?? "",
      surgicalObjectives: jointPlan?.surgicalObjectives ?? "",
      preSurgicalRequirements: jointPlan?.preSurgicalRequirements ?? "",
      postSurgicalPlan: jointPlan?.postSurgicalPlan ?? "",
      risks: jointPlan?.risks ?? "",
      patientExplanation: jointPlan?.patientExplanation ?? "",
    });
  }, [jointPlan]);

  const save = async () => {
    setSaving(true);
    try {
      await api.put(`/api/ortho-surgical-cases/${orthoSurgicalCaseId}/joint-plan`, form);
      toast.success("تم حفظ الخطة المشتركة");
      onSaved();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ الخطة");
    } finally {
      setSaving(false);
    }
  };

  const field = (key: keyof JointPlanForm, label: string, rows = 2) => (
    <div>
      <label className="block text-xs font-medium text-gray-600 mb-1">{label}</label>
      {locked ? (
        <p className="text-sm text-gray-700 bg-gray-50 rounded-lg px-3 py-2 min-h-[2.5rem] whitespace-pre-wrap">
          {form[key] || "—"}
        </p>
      ) : rows > 1 ? (
        <textarea value={form[key]} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))}
          rows={rows} className={textareaCls} />
      ) : (
        <input value={form[key]} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))} className={inputCls} />
      )}
    </div>
  );

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
          <ClipboardList className="w-4 h-4 text-[#3d7ab5]" /> الخطة المشتركة
        </h2>
        {locked && (
          <span className="flex items-center gap-1.5 text-xs font-medium text-green-700 bg-green-50 px-2.5 py-1 rounded-full">
            <Lock className="w-3.5 h-3.5" /> معتمدة ومقفلة — {formatArabicDate(jointPlan!.lockedAt!)}
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {field("procedureType", "نوع الإجراء الجراحي المقترح", 1)}
        {field("timing", "التوقيت المتوقع", 1)}
      </div>
      {field("orthodonticObjectives", "أهداف التقويم")}
      {field("surgicalObjectives", "أهداف الجراحة")}
      {field("preSurgicalRequirements", "متطلبات ما قبل الجراحة")}
      {field("postSurgicalPlan", "خطة ما بعد الجراحة")}
      {field("risks", "المخاطر والحدود")}
      {field("patientExplanation", "شرح مبسّط للمريض")}

      {!locked && canEdit && (
        <div className="flex justify-end pt-2 border-t border-[#e8f0f9]">
          <button onClick={save} disabled={saving}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition">
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            حفظ الخطة
          </button>
        </div>
      )}
    </div>
  );
}
