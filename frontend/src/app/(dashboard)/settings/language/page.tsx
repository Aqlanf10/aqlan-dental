"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRight, Languages, Loader2, Check } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

type Lang = "ar" | "en" | "both";

interface ModuleDef {
  key: string;            // Settings key suffix → ui.lang.<key>
  label: string;
  hint: string;
  active: boolean;        // whether the preference currently changes output
}

// The orthodontics module language is honored now by the case-presentation generator
// (titles in Arabic/English/both). The rest are stored preferences that each module
// will honor as it gains localization.
const MODULES: ModuleDef[] = [
  { key: "ortho", label: "وحدة التقويم وعرض الحالة", hint: "لغة عناوين عرض الحالة (PowerPoint)", active: true },
  { key: "ceph", label: "وحدة السيفالومتري", hint: "تحليلات السيفالو (مصطلحاتها إنجليزية أصلًا)", active: false },
  { key: "lab", label: "وحدة المختبر", hint: "أوامر المختبر والتراكيب", active: false },
  { key: "finance", label: "الوحدة المالية", hint: "الفواتير والتقارير المالية", active: false },
  { key: "appointments", label: "المواعيد", hint: "جدولة المواعيد", active: false },
  { key: "patients", label: "ملفات المرضى", hint: "بيانات المرضى", active: false },
];

const OPTIONS: { value: Lang; label: string }[] = [
  { value: "ar", label: "عربي" },
  { value: "en", label: "English" },
  { value: "both", label: "كلاهما" },
];

const DEFAULTS: Record<string, Lang> = { ceph: "en" };

export default function LanguageSettingsPage() {
  const [values, setValues] = useState<Record<string, Lang>>({});
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    api
      .get<Record<string, string>>("/api/settings")
      .then(({ data }) => {
        if (!active) return;
        const next: Record<string, Lang> = {};
        for (const m of MODULES) {
          const v = data[`ui.lang.${m.key}`] as Lang | undefined;
          next[m.key] = v === "ar" || v === "en" || v === "both" ? v : DEFAULTS[m.key] ?? "ar";
        }
        setValues(next);
      })
      .catch(() => toast.error("تعذّر تحميل إعدادات اللغة"))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, []);

  const save = async (key: string, value: Lang) => {
    setValues((v) => ({ ...v, [key]: value }));
    setSavingKey(key);
    try {
      await api.put(`/api/settings/ui.lang.${key}`, { value, category: "ui" });
      toast.success("تم حفظ اللغة");
    } catch {
      toast.error("تعذّر حفظ اللغة");
    } finally {
      setSavingKey(null);
    }
  };

  return (
    <div className="mx-auto max-w-3xl p-4" dir="rtl">
      <div className="mb-5 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="grid h-10 w-10 place-items-center rounded-xl bg-sky-50">
            <Languages className="h-5 w-5 text-sky-600" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-clinic-navy">لغة الوحدات</h1>
            <p className="text-sm text-gray-500">اختر لغة كل وحدة: عربي، إنجليزي، أو كلاهما.</p>
          </div>
        </div>
        <Link href="/settings" className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50">
          <ArrowRight className="h-4 w-4" />
          الإعدادات
        </Link>
      </div>

      {loading ? (
        <div className="grid place-items-center py-16"><Loader2 className="h-6 w-6 animate-spin text-clinic-blue" /></div>
      ) : (
        <div className="space-y-2">
          {MODULES.map((m) => (
            <div key={m.key} className="flex items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white p-4">
              <div>
                <p className="flex items-center gap-2 font-semibold text-gray-900">
                  {m.label}
                  {m.active
                    ? <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-[11px] text-green-700"><Check className="h-3 w-3" />مفعّل</span>
                    : <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">يُحفظ تمهيدًا</span>}
                </p>
                <p className="text-xs text-gray-500">{m.hint}</p>
              </div>
              <div className="flex items-center gap-1.5">
                <div className="inline-flex overflow-hidden rounded-lg border border-gray-200">
                  {OPTIONS.map((o) => (
                    <button
                      key={o.value}
                      type="button"
                      onClick={() => save(m.key, o.value)}
                      disabled={savingKey === m.key}
                      className={[
                        "px-3 py-1.5 text-xs font-medium transition",
                        values[m.key] === o.value ? "bg-clinic-blue text-white" : "bg-white text-gray-600 hover:bg-gray-50",
                      ].join(" ")}
                    >
                      {o.label}
                    </button>
                  ))}
                </div>
                {savingKey === m.key && <Loader2 className="h-4 w-4 animate-spin text-clinic-blue" />}
              </div>
            </div>
          ))}
          <p className="px-1 pt-2 text-[11px] text-gray-400">
            وحدة التقويم تتحكم بلغة عناوين عرض الحالة فورًا. بقية الوحدات تُحفظ تفضيلاتها وتُطبَّق تدريجيًا مع توطين كل وحدة.
          </p>
        </div>
      )}
    </div>
  );
}
