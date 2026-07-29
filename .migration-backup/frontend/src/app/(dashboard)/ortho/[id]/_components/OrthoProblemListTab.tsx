"use client";

import { useState } from "react";
import { Trash2 } from "lucide-react";
import {
  useAddProblem,
  useDeleteProblem,
  useProblemList,
} from "@/hooks/useOrtho";
import type { ProblemListItem } from "@/types/ortho";
import { Field, EmptyState, QueryErrorState, SaveButton } from "./_shared";
import { inputCls } from "../_lib/types";

export function OrthoProblemListTab({ caseId }: { caseId: string }) {
  const {
    data: problems = [] as ProblemListItem[],
    isError,
    refetch,
  } = useProblemList(caseId);
  const add = useAddProblem(caseId);
  const remove = useDeleteProblem(caseId);
  const [form, setForm] = useState({
    category: "skeletal",
    description: "",
    severity: "moderate",
  });
  const categories = {
    skeletal: "هيكلية",
    dental: "سنية",
    soft_tissue: "أنسجة رخوة",
    functional: "وظيفية",
    space: "مسافات",
    esthetic: "جمالية",
  };

  return (
    <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!form.description.trim()) return;
          add.mutate(form, {
            onSuccess: () =>
              setForm({
                category: "skeletal",
                description: "",
                severity: "moderate",
              }),
          });
        }}
        className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
      >
        <h2 className="font-semibold text-gray-900">
          إضافة مشكلة تشخيصية
        </h2>
        <Field label="التصنيف">
          <select
            className={inputCls}
            value={form.category}
            onChange={(e) =>
              setForm((f) => ({ ...f, category: e.target.value }))
            }
          >
            {Object.entries(categories).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </Field>
        <Field label="الوصف">
          <textarea
            rows={3}
            className={inputCls}
            value={form.description}
            onChange={(e) =>
              setForm((f) => ({ ...f, description: e.target.value }))
            }
          />
        </Field>
        <Field label="الشدة">
          <select
            className={inputCls}
            value={form.severity}
            onChange={(e) =>
              setForm((f) => ({ ...f, severity: e.target.value }))
            }
          >
            <option value="mild">خفيفة</option>
            <option value="moderate">متوسطة</option>
            <option value="severe">شديدة</option>
          </select>
        </Field>
        <SaveButton saving={add.isPending}>إضافة المشكلة</SaveButton>
      </form>
      <div className="space-y-3">
        {isError ? (
          <QueryErrorState
            text="تعذر تحميل قائمة المشاكل التشخيصية — تحقق من الاتصال وحاول مجددًا"
            onRetry={() => refetch()}
          />
        ) : problems.length === 0 ? (
          <EmptyState text="لم يتم تسجيل مشاكل تشخيصية بعد." />
        ) : (
          problems.map((p: ProblemListItem) => (
            <div
              key={p.id}
              className="flex items-start justify-between gap-3 rounded-lg border border-gray-200 bg-white p-4"
            >
              <div>
                <p className="font-medium text-gray-900">{p.description}</p>
                <p className="mt-1 text-xs text-gray-500">
                  {categories[p.category as keyof typeof categories] ??
                    p.category}{" "}
                  · {p.severity ?? "غير محدد"}
                </p>
              </div>
              <button
                type="button"
                onClick={() => remove.mutate(p.id)}
                className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-600"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
