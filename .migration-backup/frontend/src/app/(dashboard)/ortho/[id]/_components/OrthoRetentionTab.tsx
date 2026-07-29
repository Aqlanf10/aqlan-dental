"use client";

import { useEffect, useState } from "react";
import { formatArabicDate } from "@/lib/utils";
import {
  useAddRetentionVisit,
  useRetention,
  useSaveRetention,
} from "@/hooks/useOrtho";
import type { RetentionRecord, RetentionVisit } from "@/types/ortho";
import { Field, EmptyState, QueryErrorState, SaveButton } from "./_shared";
import { inputCls } from "../_lib/types";

export function OrthoRetentionTab({ caseId }: { caseId: string }) {
  const { data, isError, refetch } = useRetention(caseId);
  const save = useSaveRetention(caseId);
  const addVisit = useAddRetentionVisit(caseId);
  const [form, setForm] = useState<RetentionRecord>({});
  const [visit, setVisit] = useState({
    visitDate: "",
    period: "",
    toothStability: "",
    retainerStatus: "",
    notes: "",
  });
  useEffect(() => setForm(data ?? {}), [data]);

  // ORTHO-REQ-006: a failed fetch must not render the blank form — the doctor
  // could unknowingly overwrite an existing retention record.
  if (isError) {
    return (
      <QueryErrorState
        text="تعذر تحميل سجل الاحتفاظ — تحقق من الاتصال وحاول مجددًا"
        onRetry={() => refetch()}
      />
    );
  }

  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate(form);
        }}
        className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
      >
        <h2 className="font-semibold text-gray-900">سجل الاحتفاظ</h2>
        <Field label="تاريخ فك الجهاز">
          <input
            type="date"
            className={inputCls}
            value={form.debondDate ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, debondDate: e.target.value }))
            }
          />
        </Field>
        <Field label="Retainer علوي">
          <input
            className={inputCls}
            value={form.upperRetainer ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, upperRetainer: e.target.value }))
            }
          />
        </Field>
        <Field label="Retainer سفلي">
          <input
            className={inputCls}
            value={form.lowerRetainer ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, lowerRetainer: e.target.value }))
            }
          />
        </Field>
        <Field label="تعليمات">
          <textarea
            rows={3}
            className={inputCls}
            value={form.instructions ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, instructions: e.target.value }))
            }
          />
        </Field>
        <SaveButton saving={save.isPending}>حفظ الاحتفاظ</SaveButton>
      </form>
      <div className="space-y-4">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            addVisit.mutate(visit, {
              onSuccess: () =>
                setVisit({
                  visitDate: "",
                  period: "",
                  toothStability: "",
                  retainerStatus: "",
                  notes: "",
                }),
            });
          }}
          className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
        >
          <h2 className="font-semibold text-gray-900">زيارة احتفاظ</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="التاريخ">
              <input
                type="date"
                className={inputCls}
                value={visit.visitDate}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, visitDate: e.target.value }))
                }
              />
            </Field>
            <Field label="الفترة">
              <input
                className={inputCls}
                value={visit.period}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, period: e.target.value }))
                }
              />
            </Field>
            <Field label="ثبات الأسنان">
              <input
                className={inputCls}
                value={visit.toothStability}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, toothStability: e.target.value }))
                }
              />
            </Field>
            <Field label="حالة الجهاز">
              <input
                className={inputCls}
                value={visit.retainerStatus}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, retainerStatus: e.target.value }))
                }
              />
            </Field>
          </div>
          <Field label="ملاحظات">
            <textarea
              rows={2}
              className={inputCls}
              value={visit.notes}
              onChange={(e) =>
                setVisit((v) => ({ ...v, notes: e.target.value }))
              }
            />
          </Field>
          <SaveButton saving={addVisit.isPending}>إضافة زيارة</SaveButton>
        </form>
        {(data?.visits?.length ?? 0) === 0 ? (
          <EmptyState text="لا توجد زيارات احتفاظ." />
        ) : (
          data?.visits?.map((v: RetentionVisit) => (
            <div
              key={v.id}
              className="rounded-lg border border-gray-200 bg-white p-4 text-sm"
            >
              <p className="font-semibold">
                {v.visitDate ? formatArabicDate(v.visitDate) : "بدون تاريخ"} ·{" "}
                {v.period}
              </p>
              <p className="mt-1 text-gray-500">
                {v.retainerStatus}{" "}
                {v.toothStability ? `· ${v.toothStability}` : ""}
              </p>
              {v.notes && <p className="mt-2 text-gray-700">{v.notes}</p>}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
