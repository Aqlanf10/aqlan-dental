import { useState } from "react";
import { ClipboardList, Plus, Check, Clock, X, ChevronDown, ChevronUp } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface TreatmentPlanItem {
  id: string;
  toothNumber?: string;
  treatment: string;
  priority: "high" | "medium" | "low";
  status: "planned" | "in_progress" | "completed" | "cancelled";
  estimatedCost?: number;
  notes?: string;
  createdAt: string;
  completedAt?: string;
}

interface TreatmentPlanPanelProps {
  patientId: string;
  existingPlans?: TreatmentPlanItem[];
  onUpdate: () => void;
}

const TREATMENT_OPTIONS = [
  "فحص وتشخيص", "تنظيف أسنان", "حشو مؤقت", "حشو أملغم", "حشو كومبوزيت",
  "حشو زجاجي أيونومر", "معالجة جذر", "تاج بورسلين", "تاج زركون", "جسر",
  "خلع بسيط", "خلع جراحي", "خلع ضرس عقل", "زرعة", "طقم جزئي",
  "طقم كامل", "تبييض", "ختم فلورايد", "ترميم", "أخرى",
];

const PRIORITY_MAP = { high: { label: "عاجل", color: "bg-red-100 text-red-700" }, medium: { label: "متوسط", color: "bg-yellow-100 text-yellow-700" }, low: { label: "عادي", color: "bg-green-100 text-green-700" } };
const STATUS_MAP = {
  planned: { label: "مخطط", color: "bg-gray-100 text-gray-600", icon: Clock },
  in_progress: { label: "قيد التنفيذ", color: "bg-blue-100 text-blue-700", icon: Clock },
  completed: { label: "مكتمل", color: "bg-green-100 text-green-700", icon: Check },
  cancelled: { label: "ملغي", color: "bg-red-100 text-red-700", icon: X },
};

export function TreatmentPlanPanel({ patientId, existingPlans, onUpdate }: TreatmentPlanPanelProps) {
  const [plans, setPlans] = useState<TreatmentPlanItem[]>(existingPlans || []);
  const [showForm, setShowForm] = useState(false);
  const [toothNumber, setToothNumber] = useState("");
  const [treatment, setTreatment] = useState("");
  const [priority, setPriority] = useState<"high" | "medium" | "low">("medium");
  const [estimatedCost, setEstimatedCost] = useState("");
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const handleAdd = async () => {
    if (!treatment) return;
    setSaving(true);
    try {
      const { data } = await api.post("/api/general/treatment-plans", {
        patientId,
        toothNumber: toothNumber || undefined,
        treatment,
        priority,
        estimatedCost: estimatedCost ? parseFloat(estimatedCost) : undefined,
        notes: notes || undefined,
      });
      setPlans((prev) => [...prev, data]);
      setToothNumber(""); setTreatment(""); setPriority("medium"); setEstimatedCost(""); setNotes("");
      setShowForm(false);
      onUpdate();
    } catch (e) { console.error("[TreatmentPlan] Failed to create plan:", e); } finally { setSaving(false); }
  };

  const handleStatusChange = async (planId: string, status: string) => {
    try {
      await api.patch(`/api/general/treatment-plans/${planId}/status`, { status });
      setPlans((prev) => prev.map((p) => p.id === planId ? { ...p, status: status as TreatmentPlanItem["status"] } : p));
      onUpdate();
    } catch (e) { console.error("[TreatmentPlan] Failed to update plan status:", e); }
  };

  const planned = plans.filter((p) => p.status === "planned").length;
  const inProgress = plans.filter((p) => p.status === "in_progress").length;
  const completed = plans.filter((p) => p.status === "completed").length;

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-bold text-gray-900 text-sm flex items-center gap-2">
            <ClipboardList className="w-4 h-4 text-clinic-blue" />
            خطة العلاج
          </h3>
          <button onClick={() => setShowForm(!showForm)}
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:bg-clinic-navy-700">
            <Plus className="w-3 h-3" /> إضافة
          </button>
        </div>

        {/* Summary */}
        <div className="flex gap-3 mb-4">
          <div className="bg-gray-50 rounded-lg px-3 py-1.5 text-center">
            <p className="text-lg font-bold text-gray-700">{planned}</p>
            <p className="text-[10px] text-gray-500">مخطط</p>
          </div>
          <div className="bg-blue-50 rounded-lg px-3 py-1.5 text-center">
            <p className="text-lg font-bold text-blue-700">{inProgress}</p>
            <p className="text-[10px] text-blue-500">قيد التنفيذ</p>
          </div>
          <div className="bg-green-50 rounded-lg px-3 py-1.5 text-center">
            <p className="text-lg font-bold text-green-700">{completed}</p>
            <p className="text-[10px] text-green-500">مكتمل</p>
          </div>
        </div>

        {/* Add Form */}
        {showForm && (
          <div className="bg-gray-50 rounded-lg p-3 mb-3 space-y-2">
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="block text-[10px] font-medium text-gray-500 mb-0.5">العلاج</label>
                <select value={treatment} onChange={(e) => setTreatment(e.target.value)}
                  className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none">
                  <option value="">اختر...</option>
                  {TREATMENT_OPTIONS.map((t) => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-[10px] font-medium text-gray-500 mb-0.5">السن</label>
                <input value={toothNumber} onChange={(e) => setToothNumber(e.target.value)}
                  className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
                  placeholder="FDI" dir="ltr" />
              </div>
              <div>
                <label className="block text-[10px] font-medium text-gray-500 mb-0.5">الأولوية</label>
                <select value={priority} onChange={(e) => setPriority(e.target.value as "high" | "medium" | "low")}
                  className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none">
                  <option value="high">عاجل</option>
                  <option value="medium">متوسط</option>
                  <option value="low">عادي</option>
                </select>
              </div>
              <div>
                <label className="block text-[10px] font-medium text-gray-500 mb-0.5">التكلفة التقديرية</label>
                <input type="number" value={estimatedCost} onChange={(e) => setEstimatedCost(e.target.value)}
                  className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
                  placeholder="0" dir="ltr" />
              </div>
            </div>
            <div>
              <label className="block text-[10px] font-medium text-gray-500 mb-0.5">ملاحظات</label>
              <input value={notes} onChange={(e) => setNotes(e.target.value)}
                className="w-full px-2 py-1 text-xs rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
                placeholder="ملاحظات..." />
            </div>
            <div className="flex gap-2 justify-end">
              <button onClick={() => setShowForm(false)}
                className="px-3 py-1 text-xs rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-100">إلغاء</button>
              <button onClick={handleAdd} disabled={saving || !treatment}
                className="px-3 py-1 text-xs rounded-lg bg-clinic-blue text-white hover:bg-clinic-navy-700 disabled:opacity-60">
                {saving ? "..." : "إضافة"}
              </button>
            </div>
          </div>
        )}

        {/* Plans List */}
        {plans.length === 0 ? (
          <p className="text-xs text-gray-400 text-center py-4">لا توجد خطة علاج بعد</p>
        ) : (
          <div className="space-y-1.5">
            {plans.map((plan) => {
              const priorityInfo = PRIORITY_MAP[plan.priority];
              const statusInfo = STATUS_MAP[plan.status as keyof typeof STATUS_MAP];
              const isExpanded = expandedId === plan.id;
              return (
                <div key={plan.id} className="border border-gray-100 rounded-lg">
                  <div className="flex items-center gap-2 p-2">
                    <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full font-medium", priorityInfo.color)}>
                      {priorityInfo.label}
                    </span>
                    <span className="text-xs font-medium text-gray-900 flex-1">
                      {plan.treatment} {plan.toothNumber && <span className="text-gray-400 font-mono">({plan.toothNumber})</span>}
                    </span>
                    <select value={plan.status} onChange={(e) => handleStatusChange(plan.id, e.target.value)}
                      className={cn("text-[10px] px-1.5 py-0.5 rounded-full border-0 font-medium", statusInfo.color)}>
                      {Object.entries(STATUS_MAP).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
                    </select>
                    <button onClick={() => setExpandedId(isExpanded ? null : plan.id)}>
                      {isExpanded ? <ChevronUp className="w-3 h-3 text-gray-400" /> : <ChevronDown className="w-3 h-3 text-gray-400" />}
                    </button>
                  </div>
                  {isExpanded && (
                    <div className="px-2 pb-2 text-xs text-gray-500 space-y-1">
                      {plan.estimatedCost && <p>التكلفة التقديرية: {plan.estimatedCost} ر.ي</p>}
                      {plan.notes && <p>{plan.notes}</p>}
                      <p>أُضيف: {formatArabicDate(plan.createdAt)}</p>
                    </div>
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
