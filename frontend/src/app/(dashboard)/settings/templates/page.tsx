"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowRight, Copy, Eye, FileText, Plus, Save, Trash2 } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import { QueryErrorBanner } from "@/components/shared/QueryErrorBanner";

interface DocumentTemplate {
  id: string;
  title: string;
  category: string;
  description?: string | null;
  content: string;
  requiresSignature: boolean;
  isActive: boolean;
  updatedAt: string;
}

interface TemplateForm {
  title: string;
  category: string;
  description: string;
  content: string;
  requiresSignature: boolean;
  isActive: boolean;
}

const emptyForm: TemplateForm = {
  title: "",
  category: "general",
  description: "",
  content: "",
  requiresSignature: false,
  isActive: true,
};

const categories = [
  { value: "general", label: "عام" },
  { value: "consent", label: "موافقات" },
  { value: "instructions", label: "تعليمات" },
  { value: "prescription", label: "وصفات" },
  { value: "finance", label: "مالية" },
  { value: "ortho", label: "تقويم" },
];

const placeholders = [
  "{{PatientName}}",
  "{{PatientFileNumber}}",
  "{{PatientPhone}}",
  "{{Today}}",
  "{{ClinicName}}",
  "{{DoctorName}}",
];

export default function DocumentTemplatesPage() {
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<TemplateForm>(emptyForm);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState("");

  const selected = useMemo(
    () => templates.find((template) => template.id === selectedId) ?? null,
    [selectedId, templates]
  );

  const loadTemplates = () => {
    setLoading(true);
    setLoadError(null);
    api
      .get<DocumentTemplate[]>("/api/document-templates", { params: { includeInactive: true } })
      .then((res) => setTemplates(res.data ?? []))
      .catch((loadFailure) =>
        setLoadError(extractErrorMessage(loadFailure, "تعذر تحميل قوالب الوثائق"))
      )
      .finally(() => setLoading(false));
  };

  useEffect(loadTemplates, []);

  useEffect(() => {
    if (!selected) return;
    setForm({
      title: selected.title,
      category: selected.category,
      description: selected.description ?? "",
      content: selected.content,
      requiresSignature: selected.requiresSignature,
      isActive: selected.isActive,
    });
    setPreview("");
    setError("");
  }, [selected]);

  const startNew = () => {
    setSelectedId(null);
    setForm(emptyForm);
    setPreview("");
    setError("");
  };

  const save = async () => {
    if (!form.title.trim()) {
      setError("اسم القالب مطلوب");
      return;
    }
    if (!form.content.trim()) {
      setError("محتوى القالب مطلوب");
      return;
    }

    setSaving(true);
    setError("");
    try {
      const payload = {
        title: form.title.trim(),
        category: form.category.trim() || "general",
        description: form.description.trim() || null,
        content: form.content.trim(),
        requiresSignature: form.requiresSignature,
        isActive: form.isActive,
      };

      if (selectedId) {
        const { data } = await api.put<DocumentTemplate>(`/api/document-templates/${selectedId}`, payload);
        setTemplates((prev) => prev.map((template) => (template.id === data.id ? data : template)));
      } else {
        const { data } = await api.post<DocumentTemplate>("/api/document-templates", payload);
        setTemplates((prev) => [...prev, data]);
        setSelectedId(data.id);
      }
    } catch {
      setError("فشل حفظ القالب");
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (id: string) => {
    if (!window.confirm("هل تريد تعطيل هذا القالب؟")) return;
    await api.delete(`/api/document-templates/${id}`);
    setTemplates((prev) =>
      prev.map((template) => (template.id === id ? { ...template, isActive: false } : template))
    );
  };

  const renderPreview = async () => {
    if (!selectedId) {
      setPreview(form.content);
      return;
    }
    const { data } = await api.post<{ content: string }>(`/api/document-templates/${selectedId}/render`, {
      values: { DoctorName: "اسم الطبيب" },
    });
    setPreview(data.content);
  };

  const insertPlaceholder = (value: string) => {
    setForm((prev) => ({
      ...prev,
      content: `${prev.content}${prev.content.endsWith(" ") || prev.content.length === 0 ? "" : " "}${value}`,
    }));
  };

  return (
    <div className="space-y-5 max-w-7xl">
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
            <Link href="/settings" className="hover:text-clinic-blue inline-flex items-center gap-1">
              <ArrowRight className="w-4 h-4" />
              الإعدادات
            </Link>
            <span>/</span>
            <span>قوالب الوثائق</span>
          </div>
          <h1 className="text-2xl font-extrabold text-gray-900">قوالب الوثائق والنماذج</h1>
          <p className="text-sm text-gray-500 mt-1">
            أنشئ نماذج موافقة وتعليمات ووصفات قابلة لإعادة الاستخدام مع متغيرات المريض والمركز.
          </p>
        </div>
        <button
          onClick={startNew}
          className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-clinic-blue-600"
        >
          <Plus className="w-4 h-4" />
          قالب جديد
        </button>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {loadError && <QueryErrorBanner text={loadError} onRetry={loadTemplates} />}

      <div className="grid grid-cols-1 lg:grid-cols-[340px_1fr] gap-4">
        <aside className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <span className="font-bold text-gray-900">القوالب</span>
            <span className="text-xs text-gray-500">{templates.length}</span>
          </div>
          <div className="divide-y divide-gray-100 max-h-[680px] overflow-y-auto">
            {loading && templates.length === 0 ? (
              <div className="p-4 text-sm text-gray-500">جاري التحميل...</div>
            ) : !loadError && templates.length === 0 ? (
              <div className="p-4 text-sm text-gray-500">لا توجد قوالب بعد</div>
            ) : templates.length > 0 ? (
              templates.map((template) => (
                <button
                  key={template.id}
                  onClick={() => setSelectedId(template.id)}
                  className={cn(
                    "w-full text-start p-4 hover:bg-gray-50 transition",
                    selectedId === template.id && "bg-blue-50"
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="font-semibold text-gray-900">{template.title}</p>
                      <p className="text-xs text-gray-500 mt-1">
                        {categories.find((c) => c.value === template.category)?.label ?? template.category}
                      </p>
                    </div>
                    <span
                      className={cn(
                        "rounded-full px-2 py-0.5 text-xs",
                        template.isActive ? "bg-emerald-50 text-emerald-700" : "bg-gray-100 text-gray-500"
                      )}
                    >
                      {template.isActive ? "فعّال" : "معطل"}
                    </span>
                  </div>
                  {template.description && (
                    <p className="text-xs text-gray-500 mt-2 line-clamp-2">{template.description}</p>
                  )}
                </button>
              ))
            ) : null}
          </div>
        </aside>

        <main className="grid grid-cols-1 xl:grid-cols-[1fr_360px] gap-4">
          <section className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <FileText className="w-5 h-5 text-clinic-blue" />
                <h2 className="font-bold text-gray-900">{selectedId ? "تعديل القالب" : "قالب جديد"}</h2>
              </div>
              {selectedId && (
                <button
                  onClick={() => deactivate(selectedId)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-red-200 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50"
                >
                  <Trash2 className="w-4 h-4" />
                  تعطيل
                </button>
              )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <label className="space-y-1">
                <span className="text-sm font-medium text-gray-700">اسم القالب</span>
                <input
                  value={form.title}
                  onChange={(e) => setForm({ ...form, title: e.target.value })}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue"
                />
              </label>
              <label className="space-y-1">
                <span className="text-sm font-medium text-gray-700">التصنيف</span>
                <select
                  value={form.category}
                  onChange={(e) => setForm({ ...form, category: e.target.value })}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue"
                >
                  {categories.map((category) => (
                    <option key={category.value} value={category.value}>{category.label}</option>
                  ))}
                </select>
              </label>
            </div>

            <label className="space-y-1 block">
              <span className="text-sm font-medium text-gray-700">وصف مختصر</span>
              <input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue"
              />
            </label>

            <div className="space-y-2">
              <div className="flex flex-wrap gap-2">
                {placeholders.map((placeholder) => (
                  <button
                    key={placeholder}
                    onClick={() => insertPlaceholder(placeholder)}
                    className="inline-flex items-center gap-1 rounded-full border border-gray-200 px-2.5 py-1 text-xs text-gray-600 hover:bg-gray-50"
                  >
                    <Copy className="w-3 h-3" />
                    {placeholder}
                  </button>
                ))}
              </div>
              <textarea
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
                rows={16}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm leading-7 focus:outline-none focus:ring-2 focus:ring-clinic-blue"
                placeholder="اكتب نص القالب هنا..."
              />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
              <label className="inline-flex items-center gap-2 text-sm text-gray-700">
                <input
                  type="checkbox"
                  checked={form.requiresSignature}
                  onChange={(e) => setForm({ ...form, requiresSignature: e.target.checked })}
                  className="rounded border-gray-300"
                />
                يتطلب توقيع المريض
              </label>
              <label className="inline-flex items-center gap-2 text-sm text-gray-700">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  className="rounded border-gray-300"
                />
                القالب فعّال
              </label>
              <div className="flex gap-2">
                <button
                  onClick={renderPreview}
                  className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
                >
                  <Eye className="w-4 h-4" />
                  معاينة
                </button>
                <button
                  onClick={save}
                  disabled={saving}
                  className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-clinic-blue-600 disabled:opacity-60"
                >
                  <Save className="w-4 h-4" />
                  {saving ? "جاري الحفظ..." : "حفظ"}
                </button>
              </div>
            </div>
          </section>

          <aside className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
            <h2 className="font-bold text-gray-900">المعاينة</h2>
            <div className="min-h-[300px] rounded-lg border border-dashed border-gray-200 bg-gray-50 p-4 whitespace-pre-wrap text-sm leading-7 text-gray-700">
              {preview || form.content || "اضغط معاينة لرؤية القالب بعد استبدال المتغيرات."}
            </div>
            <div className="rounded-lg bg-amber-50 border border-amber-200 p-3 text-xs leading-6 text-amber-800">
              هذه القوالب تنظم النصوص فقط. المستند النهائي المرتبط بالمريض يتم حفظه لاحقاً من شاشة المريض أو رحلة العلاج عند استخدام القالب.
            </div>
          </aside>
        </main>
      </div>
    </div>
  );
}
