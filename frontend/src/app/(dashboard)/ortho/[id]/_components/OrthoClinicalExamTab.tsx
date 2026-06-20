"use client";

import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { useClinicalExam, useSaveClinicalExam } from "@/hooks/useOrtho";
import type { ClinicalExam } from "@/types/ortho";
import {
  ANGLE_CLASS_LABELS,
  ARCH_FORM_LABELS,
  CHIN_POSITION_LABELS,
  CROSSBITE_TYPE_LABELS,
  CURVE_OF_SPEE_LABELS,
  HABIT_LABELS,
  INCISOR_RELATION_LABELS,
  LIP_COMPETENCE_LABELS,
  NASOLABIAL_LABELS,
  ORAL_HYGIENE_LABELS,
} from "@/types/ortho";
import { Field, SaveButton } from "./_shared";
import { inputCls, HABIT_FLAG_KEYS } from "../_lib/types";

/* ------------------------------------------------------------------ */
/*  Exam helpers                                                       */
/* ------------------------------------------------------------------ */

/** قسم قابل للطي في نموذج الفحص السريري (RTL) */
function ExamSection({
  title,
  defaultOpen = true,
  children,
}: {
  title: string;
  defaultOpen?: boolean;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="rounded-lg border border-gray-200 bg-white">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between px-5 py-3 text-sm font-semibold text-clinic-navy"
      >
        <span>{title}</span>
        <ChevronDown
          className={cn(
            "h-4 w-4 text-gray-400 transition-transform",
            open && "rotate-180"
          )}
        />
      </button>
      {open && <div className="border-t border-gray-100 p-5">{children}</div>}
    </div>
  );
}

/** قائمة منسدلة لقيمة معيارية (القيمة المخزنة → تسمية عربية) */
function ExamEnumSelect({
  label,
  value,
  labels,
  onChange,
  className,
}: {
  label: string;
  value?: string;
  labels: Record<string, string>;
  onChange: (value?: string) => void;
  className?: string;
}) {
  return (
    <Field label={label} className={className}>
      <select
        className={inputCls}
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value || undefined)}
      >
        <option value="">اختر</option>
        {Object.entries(labels).map(([v, l]) => (
          <option key={v} value={v}>
            {l}
          </option>
        ))}
      </select>
    </Field>
  );
}

function ExamNumberInput({
  label,
  value,
  onChange,
  step = 0.1,
  min,
  max,
}: {
  label: string;
  value?: number;
  onChange: (value?: number) => void;
  step?: number;
  min?: number;
  max?: number;
}) {
  return (
    <Field label={label}>
      <input
        type="number"
        step={step}
        min={min}
        max={max}
        className={inputCls}
        value={value ?? ""}
        onChange={(e) =>
          onChange(e.target.value === "" ? undefined : Number(e.target.value))
        }
      />
    </Field>
  );
}

function ExamCheckbox({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50">
      <input
        type="checkbox"
        className="h-4 w-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
        checked={checked ?? false}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}

/* ------------------------------------------------------------------ */
/*  ClinicalExamPanel                                                  */
/* ------------------------------------------------------------------ */

export function OrthoClinicalExamTab({ caseId }: { caseId: string }) {
  const { data } = useClinicalExam(caseId);
  const save = useSaveClinicalExam(caseId);
  const [form, setForm] = useState<ClinicalExam>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof ClinicalExam>(
    key: K,
    value: ClinicalExam[K]
  ) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      {/* ١) الفحص خارج الفم */}
      <ExamSection title="الفحص خارج الفم">
        <div className="grid gap-4 md:grid-cols-3">
          <Field label="تاريخ الفحص">
            <input
              type="date"
              className={inputCls}
              value={form.examDate ?? ""}
              onChange={(e) => set("examDate", e.target.value)}
            />
          </Field>
          <Field label="البروفايل">
            <select
              className={inputCls}
              value={form.profile ?? ""}
              onChange={(e) => set("profile", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Convex</option>
              <option>Concave</option>
            </select>
          </Field>
          <Field label="التماثل الوجهي">
            <select
              className={inputCls}
              value={form.facialSymmetry ?? ""}
              onChange={(e) => set("facialSymmetry", e.target.value)}
            >
              <option value="">اختر</option>
              <option>متماثل</option>
              <option>غير متماثل</option>
            </select>
          </Field>
          <Field label="انطباق الشفاه (نعم/لا)">
            <select
              className={inputCls}
              value={form.lipsCompetence ? "true" : form.lipsCompetence === false ? "false" : ""}
              onChange={(e) =>
                set(
                  "lipsCompetence",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">منطبقة</option>
              <option value="false">غير منطبقة</option>
            </select>
          </Field>
          <ExamEnumSelect
            label="درجة انطباق الشفاه"
            value={form.lipCompetenceGrade}
            labels={LIP_COMPETENCE_LABELS}
            onChange={(v) => set("lipCompetenceGrade", v)}
          />
          <ExamEnumSelect
            label="الزاوية الأنفية الشفوية"
            value={form.nasolabialAngle}
            labels={NASOLABIAL_LABELS}
            onChange={(v) => set("nasolabialAngle", v)}
          />
          <ExamEnumSelect
            label="وضع الذقن"
            value={form.chinPosition}
            labels={CHIN_POSITION_LABELS}
            onChange={(v) => set("chinPosition", v)}
          />
          <Field label="خط الابتسامة">
            <input
              className={inputCls}
              value={form.smileLine ?? ""}
              onChange={(e) => set("smileLine", e.target.value)}
              placeholder="منخفض / متوسط / عالي"
            />
          </Field>
          <Field label="النسب العمودية">
            <input
              className={inputCls}
              value={form.verticalProportion ?? ""}
              onChange={(e) => set("verticalProportion", e.target.value)}
              placeholder="طبيعي / طويل / قصير"
            />
          </Field>
          <Field label="انزياح وظيفي">
            <input
              className={inputCls}
              value={form.functionalShift ?? ""}
              onChange={(e) => set("functionalShift", e.target.value)}
              placeholder="انزياح الفك عند الإطباق إن وجد"
            />
          </Field>
          <div className="flex items-end">
            <ExamCheckbox
              label="ابتسامة لثوية"
              checked={form.gummySmile}
              onChange={(v) => set("gummySmile", v)}
            />
          </div>
        </div>
      </ExamSection>

      {/* ٢) العادات الفموية */}
      <ExamSection title="العادات الفموية">
        <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-3">
          {HABIT_FLAG_KEYS.map((key) => (
            <ExamCheckbox
              key={key}
              label={HABIT_LABELS[key]}
              checked={form[key]}
              onChange={(v) => set(key, v)}
            />
          ))}
        </div>
        <div className="mt-4">
          <Field label="تفاصيل العادات (نص حر)">
            <textarea
              rows={2}
              className={inputCls}
              value={form.habits ?? ""}
              onChange={(e) => set("habits", e.target.value)}
              placeholder="تنفس فمي، مص إصبع..."
            />
          </Field>
        </div>
      </ExamSection>

      {/* ٣) الفحص داخل الفم */}
      <ExamSection title="الفحص داخل الفم">
        <div className="grid gap-4 md:grid-cols-3">
          <ExamEnumSelect
            label="نظافة الفم"
            value={form.oralHygiene}
            labels={ORAL_HYGIENE_LABELS}
            onChange={(v) => set("oralHygiene", v)}
          />
          <Field label="حالة اللثة">
            <input
              className={inputCls}
              value={form.gingivalCondition ?? ""}
              onChange={(e) => set("gingivalCondition", e.target.value)}
            />
          </Field>
          <Field label="مشاكل دواعم الأسنان">
            <input
              className={inputCls}
              value={form.periodontalConcerns ?? ""}
              onChange={(e) => set("periodontalConcerns", e.target.value)}
            />
          </Field>
          <Field label="أسنان مفقودة (FDI)">
            <input
              className={inputCls}
              value={form.missingTeethFdi ?? ""}
              onChange={(e) => set("missingTeethFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان لبنية متبقية (FDI)">
            <input
              className={inputCls}
              value={form.retainedDeciduousFdi ?? ""}
              onChange={(e) => set("retainedDeciduousFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان منطمرة (FDI)">
            <input
              className={inputCls}
              value={form.impactedTeethFdi ?? ""}
              onChange={(e) => set("impactedTeethFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان زائدة">
            <input
              className={inputCls}
              value={form.supernumeraryNote ?? ""}
              onChange={(e) => set("supernumeraryNote", e.target.value)}
            />
          </Field>
          <Field label="بزوغ منتبذ">
            <input
              className={inputCls}
              value={form.ectopicEruptionNote ?? ""}
              onChange={(e) => set("ectopicEruptionNote", e.target.value)}
            />
          </Field>
          <Field label="اللجام">
            <input
              className={inputCls}
              value={form.frenumNote ?? ""}
              onChange={(e) => set("frenumNote", e.target.value)}
            />
          </Field>
          <Field label="اللسان">
            <input
              className={inputCls}
              value={form.tongueNote ?? ""}
              onChange={(e) => set("tongueNote", e.target.value)}
            />
          </Field>
          <Field label="التسوس">
            <input
              className={inputCls}
              value={form.cariesNote ?? ""}
              onChange={(e) => set("cariesNote", e.target.value)}
            />
          </Field>
        </div>
      </ExamSection>

      {/* ٤) فحص الإطباق */}
      <ExamSection title="فحص الإطباق">
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid grid-cols-2 gap-3 rounded-lg bg-gray-50 p-3">
              <ExamEnumSelect
                label="علاقة الأرحاء — يمين"
                value={form.molarRelationRight}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("molarRelationRight", v)}
              />
              <ExamEnumSelect
                label="علاقة الأرحاء — يسار"
                value={form.molarRelationLeft}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("molarRelationLeft", v)}
              />
            </div>
            <div className="grid grid-cols-2 gap-3 rounded-lg bg-gray-50 p-3">
              <ExamEnumSelect
                label="علاقة الأنياب — يمين"
                value={form.canineRelationRight}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("canineRelationRight", v)}
              />
              <ExamEnumSelect
                label="علاقة الأنياب — يسار"
                value={form.canineRelationLeft}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("canineRelationLeft", v)}
              />
            </div>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <ExamEnumSelect
              label="العلاقة القاطعية"
              value={form.incisorRelation}
              labels={INCISOR_RELATION_LABELS}
              onChange={(v) => set("incisorRelation", v)}
            />
            <Field label="علاقة الأرحاء (عام)">
              <select
                className={inputCls}
                value={form.molarRelation ?? ""}
                onChange={(e) => set("molarRelation", e.target.value)}
              >
                <option value="">اختر</option>
                <option>Class I</option>
                <option>Class II Div 1</option>
                <option>Class II Div 2</option>
                <option>Class III</option>
              </select>
            </Field>
            <Field label="علاقة الأنياب (عام)">
              <select
                className={inputCls}
                value={form.canineRelation ?? ""}
                onChange={(e) => set("canineRelation", e.target.value)}
              >
                <option value="">اختر</option>
                <option>Class I</option>
                <option>Class II</option>
                <option>Class III</option>
              </select>
            </Field>
            <ExamNumberInput
              label="Overjet (mm)"
              min={-30}
              max={30}
              value={form.overjet}
              onChange={(v) => set("overjet", v)}
            />
            <ExamNumberInput
              label="Overbite (mm)"
              min={-30}
              max={30}
              value={form.overbite}
              onChange={(v) => set("overbite", v)}
            />
            <ExamNumberInput
              label="Overbite (%)"
              min={0}
              max={200}
              step={1}
              value={form.overbitePercent}
              onChange={(v) => set("overbitePercent", v)}
            />
          </div>
          <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-4">
            <ExamCheckbox
              label="عضة معكوسة (Crossbite)"
              checked={form.crossbite}
              onChange={(v) => set("crossbite", v)}
            />
            <ExamCheckbox
              label="عضة مفتوحة (Open Bite)"
              checked={form.openBite}
              onChange={(v) => set("openBite", v)}
            />
            <ExamCheckbox
              label="عضة عميقة (Deep Bite)"
              checked={form.deepBite}
              onChange={(v) => set("deepBite", v)}
            />
            <ExamCheckbox
              label="عضة مقصية (Scissor Bite)"
              checked={form.scissorBite}
              onChange={(v) => set("scissorBite", v)}
            />
          </div>
          {form.crossbite && (
            <div className="grid gap-4 md:grid-cols-3">
              <ExamEnumSelect
                label="نوع العضة المعكوسة"
                value={form.crossbiteType}
                labels={CROSSBITE_TYPE_LABELS}
                onChange={(v) => set("crossbiteType", v)}
              />
            </div>
          )}
          <div className="grid gap-4 md:grid-cols-3">
            <ExamNumberInput
              label="انحراف الخط الناصف العلوي (mm، + = يمين)"
              min={-30}
              max={30}
              value={form.midlineUpperShiftMm}
              onChange={(v) => set("midlineUpperShiftMm", v)}
            />
            <ExamNumberInput
              label="انحراف الخط الناصف السفلي (mm، + = يمين)"
              min={-30}
              max={30}
              value={form.midlineLowerShiftMm}
              onChange={(v) => set("midlineLowerShiftMm", v)}
            />
            <Field label="الخط الناصف العلوي (وصف)">
              <input
                className={inputCls}
                value={form.midlineUpper ?? ""}
                onChange={(e) => set("midlineUpper", e.target.value)}
                placeholder="متوافق / منحرف يمين / منحرف يسار"
              />
            </Field>
            <Field label="الخط الناصف السفلي (وصف)">
              <input
                className={inputCls}
                value={form.midlineLower ?? ""}
                onChange={(e) => set("midlineLower", e.target.value)}
                placeholder="متوافق / منحرف يمين / منحرف يسار"
              />
            </Field>
            <ExamNumberInput
              label="تكدس علوي (mm)"
              min={-30}
              max={30}
              value={form.upperCrowdingMm}
              onChange={(v) => set("upperCrowdingMm", v)}
            />
            <ExamNumberInput
              label="تكدس سفلي (mm)"
              min={-30}
              max={30}
              value={form.lowerCrowdingMm}
              onChange={(v) => set("lowerCrowdingMm", v)}
            />
            <Field label="تكدس علوي (وصف)">
              <input
                className={inputCls}
                value={form.upperCrowding ?? ""}
                onChange={(e) => set("upperCrowding", e.target.value)}
                placeholder="خفيف / متوسط / شديد"
              />
            </Field>
            <Field label="تكدس سفلي (وصف)">
              <input
                className={inputCls}
                value={form.lowerCrowding ?? ""}
                onChange={(e) => set("lowerCrowding", e.target.value)}
                placeholder="خفيف / متوسط / شديد"
              />
            </Field>
            <ExamNumberInput
              label="مسافات علوية (mm)"
              value={form.upperSpacing}
              onChange={(v) => set("upperSpacing", v)}
            />
            <ExamNumberInput
              label="مسافات سفلية (mm)"
              min={-30}
              max={30}
              value={form.lowerSpacingMm}
              onChange={(v) => set("lowerSpacingMm", v)}
            />
            <ExamEnumSelect
              label="منحنى شبي (Curve of Spee)"
              value={form.curveOfSpee}
              labels={CURVE_OF_SPEE_LABELS}
              onChange={(v) => set("curveOfSpee", v)}
            />
            <ExamEnumSelect
              label="شكل القوس العلوي"
              value={form.archFormUpper}
              labels={ARCH_FORM_LABELS}
              onChange={(v) => set("archFormUpper", v)}
            />
            <ExamEnumSelect
              label="شكل القوس السفلي"
              value={form.archFormLower}
              labels={ARCH_FORM_LABELS}
              onChange={(v) => set("archFormLower", v)}
            />
            <Field label="ملاحظة تحليل بولتون" className="md:col-span-2">
              <input
                className={inputCls}
                value={form.boltonDiscrepancyNote ?? ""}
                onChange={(e) => set("boltonDiscrepancyNote", e.target.value)}
              />
            </Field>
          </div>
        </div>
      </ExamSection>

      {/* ٥) وظيفي وملاحظات */}
      <ExamSection title="وظيفي وملاحظات">
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="تناقض Co-Cr">
            <select
              className={inputCls}
              value={
                form.coCrDiscrepancy
                  ? "true"
                  : form.coCrDiscrepancy === false
                    ? "false"
                    : ""
              }
              onChange={(e) =>
                set(
                  "coCrDiscrepancy",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">نعم</option>
              <option value="false">لا</option>
            </select>
          </Field>
          <Field label="ملاحظات TMJ">
            <textarea
              rows={2}
              className={inputCls}
              value={form.tmjFindings ?? ""}
              onChange={(e) => set("tmjFindings", e.target.value)}
            />
          </Field>
          <Field label="ملاحظات عامة" className="md:col-span-2">
            <textarea
              rows={3}
              className={inputCls}
              value={form.notes ?? ""}
              onChange={(e) => set("notes", e.target.value)}
            />
          </Field>
        </div>
      </ExamSection>

      <SaveButton saving={save.isPending}>حفظ الفحص</SaveButton>
    </form>
  );
}
