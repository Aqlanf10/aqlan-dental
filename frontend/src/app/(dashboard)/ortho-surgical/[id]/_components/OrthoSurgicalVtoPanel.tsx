"use client";

import { useCallback, useEffect, useState } from "react";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Loader2,
  Lock,
  Plus,
  Ruler,
  ShieldCheck,
  Trash2,
} from "lucide-react";
import type {
  CreateOrthoSurgicalVtoRequest,
  OrthoSurgicalVto,
} from "@/types/orthoSurgical";
import { VTO_DISCLAIMER_AR } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";

interface OrthoSurgicalVtoPanelProps {
  /** The OrthoSurgicalCase this VTO panel belongs to (self-fetching — only needs the id). */
  orthoSurgicalCaseId: string;
}

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] transition";

/**
 * Sprint A9 — the Surgical VTO (Visual Treatment Objective) panel. Lets the orthodontist
 * sketch a jaw-movement scenario (maxilla/mandible/chin in mm + rotation in degrees) and
 * shows the resulting predicted SNA/SNB/ANB/Wits/Overjet computed server-side from the
 * approved CephAnalysis baseline using documented geometric relationships. The mandatory
 * Arabic disclaimer is rendered on every scenario card AND on the empty-state — no surface
 * shows a VTO without it.
 *
 * Backend gate: POST/PUT are rejected with 400 Arabic if the linked CephAnalysis is not
 * approved. We surface that message via the toast (same pattern as JointPlanPanel). VTO
 * approval is explicit and orthodontist-only — it never auto-flips on create, and the
 * scenario becomes immutable once approved (the PUT endpoint refuses with 400).
 */
export function OrthoSurgicalVtoPanel({ orthoSurgicalCaseId }: OrthoSurgicalVtoPanelProps) {
  const { user } = useAuthStore();
  const role = user?.role;
  const isOrtho = role === "Admin" || role === "Orthodontist";
  const isEditor =
    role === "Admin" || role === "Orthodontist" || role === "OralSurgeon";

  const [scenarios, setScenarios] = useState<OrthoSurgicalVto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);

  // Form state — kept as strings so the user can clear / type intermediate values
  // (e.g. "-", "1.", "1.5") without the input fighting them. Parsed to number on submit.
  const [form, setForm] = useState({
    maxillaMoveMm: "",
    mandibleMoveMm: "",
    chinMoveMm: "",
    rotationDegree: "",
    notes: "",
  });

  const fetchScenarios = useCallback(async () => {
    try {
      const res = await api.get<{ data: OrthoSurgicalVto[] }>(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/vto`
      );
      setScenarios(res.data?.data ?? []);
    } catch {
      /* silent — an empty list is a reasonable fallback */
    } finally {
      setLoading(false);
    }
  }, [orthoSurgicalCaseId]);

  useEffect(() => {
    fetchScenarios();
  }, [fetchScenarios]);

  const parseNum = (s: string): number | null => {
    const t = s.trim();
    if (t === "" || t === "-" || t === ".") return null;
    const n = Number(t);
    return Number.isFinite(n) ? n : null;
  };

  const buildPayload = (): CreateOrthoSurgicalVtoRequest => ({
    maxillaMoveMm: parseNum(form.maxillaMoveMm),
    mandibleMoveMm: parseNum(form.mandibleMoveMm),
    chinMoveMm: parseNum(form.chinMoveMm),
    rotationDegree: parseNum(form.rotationDegree),
    notes: form.notes.trim() || null,
  });

  const create = async () => {
    const payload = buildPayload();
    if (
      payload.maxillaMoveMm === null &&
      payload.mandibleMoveMm === null &&
      payload.chinMoveMm === null &&
      payload.rotationDegree === null
    ) {
      toast.error("أدخل قيمة حركة واحدة على الأقل");
      return;
    }
    setBusy("create");
    try {
      await api.post(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/vto`,
        payload
      );
      toast.success("تم إنشاء سيناريو المحاكاة");
      setForm({ maxillaMoveMm: "", mandibleMoveMm: "", chinMoveMm: "", rotationDegree: "", notes: "" });
      await fetchScenarios();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response
        ?.data?.message;
      toast.error(msg ?? "فشل إنشاء المحاكاة");
    } finally {
      setBusy(null);
    }
  };

  const approve = async (vtoId: string) => {
    setBusy(`approve-${vtoId}`);
    try {
      await api.post(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/vto/${vtoId}/approve`
      );
      toast.success("تم اعتماد السيناريو");
      await fetchScenarios();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response
        ?.data?.message;
      toast.error(msg ?? "فشل اعتماد المحاكاة");
    } finally {
      setBusy(null);
    }
  };

  const remove = async (vtoId: string) => {
    if (!confirm("حذف هذا السيناريو؟ لا يمكن التراجع.")) return;
    setBusy(`del-${vtoId}`);
    try {
      await api.delete(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/vto/${vtoId}`
      );
      toast.success("تم حذف السيناريو");
      await fetchScenarios();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response
        ?.data?.message;
      toast.error(msg ?? "فشل حذف المحاكاة");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
          <Activity className="w-4 h-4 text-[#3d7ab5]" /> محاكاة VTO جراحية (ثنائية الأبعاد)
        </h2>
        <span className="text-[11px] text-gray-400 flex items-center gap-1">
          <Ruler className="w-3 h-3" /> مبنية على التحليل السيفالومتري المعتمد
        </span>
      </div>

      {/* MANDATORY DISCLAIMER — rendered once at the top of the panel */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-6 text-amber-800 flex items-start gap-2">
        <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
        <span>{VTO_DISCLAIMER_AR}</span>
      </div>

      {isEditor && (
        <div className="rounded-lg border border-gray-200 bg-gray-50/60 p-4 space-y-3">
          <p className="text-xs font-medium text-gray-600">
            أدخل حركة الأفواه المخططة (بالملم — القيم الموجبة = تحريك للأمام، السالبة = تراجع):
          </p>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <MovementField
              label="الفك العلوي (mm)"
              value={form.maxillaMoveMm}
              onChange={(v) => setForm((f) => ({ ...f, maxillaMoveMm: v }))}
              step={0.5}
              min={-15}
              max={15}
            />
            <MovementField
              label="الفك السفلي (mm)"
              value={form.mandibleMoveMm}
              onChange={(v) => setForm((f) => ({ ...f, mandibleMoveMm: v }))}
              step={0.5}
              min={-15}
              max={15}
            />
            <MovementField
              label="الذقن (mm)"
              value={form.chinMoveMm}
              onChange={(v) => setForm((f) => ({ ...f, chinMoveMm: v }))}
              step={0.5}
              min={-12}
              max={12}
            />
            <MovementField
              label="الدوران (°)"
              value={form.rotationDegree}
              onChange={(v) => setForm((f) => ({ ...f, rotationDegree: v }))}
              step={0.5}
              min={-10}
              max={10}
            />
          </div>
          <textarea
            value={form.notes}
            onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
            rows={2}
            maxLength={4000}
            placeholder="ملاحظات السيناريو (اختياري) — مثال: Le Fort I + BSSO advancement"
            className={cn(inputCls, "resize-y")}
          />
          <div className="flex justify-end">
            <button
              type="button"
              onClick={create}
              disabled={busy === "create"}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition"
            >
              {busy === "create" ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Plus className="w-4 h-4" />
              )}
              إنشاء سيناريو محاكاة
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <div className="space-y-2 animate-pulse">
          <div className="h-24 bg-gray-100 rounded-lg" />
          <div className="h-24 bg-gray-100 rounded-lg" />
        </div>
      ) : scenarios.length === 0 ? (
        <p className="text-xs text-gray-400 py-4 text-center">
          لا توجد سيناريوهات محاكاة بعد. أنشئ أول سيناريو بالأعلى.
          <br />
          <span className="text-amber-700">{VTO_DISCLAIMER_AR}</span>
        </p>
      ) : (
        <div className="space-y-3">
          {scenarios.map((s) => (
            <VtoCard
              key={s.id}
              vto={s}
              isOrtho={isOrtho}
              isEditor={isEditor}
              busy={busy}
              onApprove={() => approve(s.id)}
              onDelete={() => remove(s.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ── Movement input (number + slider combo) ──────────────────────────────────────
function MovementField({
  label,
  value,
  onChange,
  step,
  min,
  max,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  step: number;
  min: number;
  max: number;
}) {
  const num = value === "" || value === "-" || value === "." ? null : Number(value);
  return (
    <div>
      <label className="block text-[11px] font-medium text-gray-600 mb-1">{label}</label>
      <input
        type="number"
        inputMode="decimal"
        step={step}
        min={min}
        max={max}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className={inputCls}
      />
      {num !== null && Number.isFinite(num) && (
        <input
          type="range"
          step={step}
          min={min}
          max={max}
          value={Math.max(min, Math.min(max, num))}
          onChange={(e) => onChange(e.target.value)}
          className="w-full mt-1.5 accent-[#3d7ab5]"
          aria-label={`${label} slider`}
        />
      )}
    </div>
  );
}

// ── VTO scenario card ───────────────────────────────────────────────────────────
function VtoCard({
  vto,
  isOrtho,
  isEditor,
  busy,
  onApprove,
  onDelete,
}: {
  vto: OrthoSurgicalVto;
  isOrtho: boolean;
  isEditor: boolean;
  busy: string | null;
  onApprove: () => void;
  onDelete: () => void;
}) {
  return (
    <div
      className={cn(
        "rounded-lg border p-4 space-y-3",
        vto.isApprovedByOrthodontist
          ? "border-emerald-200 bg-emerald-50/40"
          : "border-gray-200 bg-white"
      )}
    >
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <div className="flex items-center gap-2">
          {vto.isApprovedByOrthodontist ? (
            <span className="flex items-center gap-1 text-xs font-medium text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded-full">
              <Lock className="w-3 h-3" /> معتمد — {formatArabicDate(vto.approvedAt ?? vto.createdAt)}
            </span>
          ) : (
            <span className="text-[11px] text-gray-400">
              أُنشئ في {formatArabicDate(vto.createdAt)}
            </span>
          )}
        </div>
        {isEditor && !vto.isApprovedByOrthodontist && (
          <button
            type="button"
            onClick={onDelete}
            disabled={busy === `del-${vto.id}`}
            className="flex items-center gap-1 text-xs font-medium text-red-600 hover:text-red-700 disabled:opacity-60"
          >
            {busy === `del-${vto.id}` ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <Trash2 className="w-3.5 h-3.5" />
            )}
            حذف
          </button>
        )}
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
        <MovementDisplay label="الفك العلوي" value={vto.maxillaMoveMm} unit="mm" />
        <MovementDisplay label="الفك السفلي" value={vto.mandibleMoveMm} unit="mm" />
        <MovementDisplay label="الذقن" value={vto.chinMoveMm} unit="mm" />
        <MovementDisplay label="الدوران" value={vto.rotationDegree} unit="°" />
      </div>

      <div className="rounded-lg border border-[#e8f0f9] bg-[#f6faff] p-3">
        <p className="text-[11px] font-semibold text-gray-600 mb-2">
          القياسات السيفالومترية المتوقعة بعد الحركة
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
          <PredictedDisplay label="SNA" value={vto.predictedSNA} unit="°" />
          <PredictedDisplay label="SNB" value={vto.predictedSNB} unit="°" />
          <PredictedDisplay label="ANB" value={vto.predictedANB} unit="°" />
          <PredictedDisplay label="Wits" value={vto.predictedWits} unit="mm" />
          <PredictedDisplay label="Overjet" value={vto.predictedOverjet} unit="mm" />
        </div>
      </div>

      {vto.notes && (
        <p className="text-xs text-gray-600 whitespace-pre-wrap">{vto.notes}</p>
      )}

      {/* MANDATORY DISCLAIMER — rendered on every VTO card */}
      <div className="rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1.5 text-[11px] leading-5 text-amber-800 flex items-start gap-1.5">
        <AlertTriangle className="w-3 h-3 mt-0.5 flex-shrink-0" />
        <span>{vto.disclaimer ?? VTO_DISCLAIMER_AR}</span>
      </div>

      {isOrtho && !vto.isApprovedByOrthodontist && (
        <div className="flex justify-end pt-1 border-t border-gray-100">
          <button
            type="button"
            onClick={onApprove}
            disabled={busy === `approve-${vto.id}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-60 transition"
          >
            {busy === `approve-${vto.id}` ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <ShieldCheck className="w-3.5 h-3.5" />
            )}
            اعتماد السيناريو
          </button>
        </div>
      )}
    </div>
  );
}

function MovementDisplay({
  label,
  value,
  unit,
}: {
  label: string;
  value: number | null;
  unit: string;
}) {
  return (
    <div className="rounded-md bg-gray-50 px-2 py-1.5">
      <div className="text-[10px] text-gray-500">{label}</div>
      <div className="text-sm font-semibold text-gray-800">
        {value === null || value === undefined ? "—" : `${value} ${unit}`}
      </div>
    </div>
  );
}

function PredictedDisplay({
  label,
  value,
  unit,
}: {
  label: string;
  value: number | null;
  unit: string;
}) {
  const has = value !== null && value !== undefined;
  return (
    <div className="text-center">
      <div className="text-[10px] text-gray-500">{label}</div>
      <div
        className={cn(
          "text-sm font-bold",
          has ? "text-[#2d5e8e]" : "text-gray-300"
        )}
      >
        {has ? `${value} ${unit}` : "—"}
      </div>
      {has && <CheckCircle2 className="w-3 h-3 text-emerald-500 mx-auto mt-0.5" />}
    </div>
  );
}
