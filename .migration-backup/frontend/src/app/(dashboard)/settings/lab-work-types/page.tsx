"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Plus, Search, Edit3, Trash2, X, Save, Wrench,
  CheckCircle2, AlertTriangle, Loader2, ArrowUpDown,
} from "lucide-react";
import Link from "next/link";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import type { LabWorkType } from "@/types/lab";

// ─── Types ────────────────────────────────────────────────────────────────────

interface LabWorkTypeForm {
  name: string;
  nameAr: string;
  category: string;
  sortOrder: number;
  isActive: boolean;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const CATEGORY_LABELS: Record<string, string> = {
  Crown: "تاج",
  Bridge: "جسر",
  Denture: "طقم أسنان",
  Implant: "زراعة",
  Orthodontic: "تقويم",
  Veneer: "قشور",
  Inlay: "حشوة داخلية",
  Other: "أخرى",
};

const CATEGORIES = Object.keys(CATEGORY_LABELS);

const CATEGORY_COLORS: Record<string, string> = {
  Crown: "bg-amber-50 text-amber-700",
  Bridge: "bg-emerald-50 text-emerald-700",
  Denture: "bg-purple-50 text-purple-700",
  Implant: "bg-sky-50 text-sky-700",
  Orthodontic: "bg-pink-50 text-pink-700",
  Veneer: "bg-rose-50 text-rose-700",
  Inlay: "bg-cyan-50 text-cyan-700",
  Other: "bg-gray-50 text-gray-700",
};

const EMPTY_FORM: LabWorkTypeForm = {
  name: "",
  nameAr: "",
  category: "",
  sortOrder: 0,
  isActive: true,
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Helper ───────────────────────────────────────────────────────────────────
// FE-11: getApiErrorMessage removed — use extractErrorMessage from @/lib/errors.

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function LabWorkTypesSettingsPage() {
  const [workTypes, setWorkTypes] = useState<LabWorkType[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchText, setSearchText] = useState("");

  // Dialog state
  const [showForm, setShowForm] = useState(false);
  const [editingType, setEditingType] = useState<LabWorkType | null>(null);
  const [form, setForm] = useState<LabWorkTypeForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  // Confirm dialog
  const [confirmDialog, setConfirmDialog] = useState<{
    open: boolean;
    title: string;
    message: string;
    variant?: "danger" | "warning";
    onConfirm: () => void;
  }>({ open: false, title: "", message: "", onConfirm: () => {} });

  // Toast
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const showToast = useCallback((message: string, type: "success" | "error" = "success") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  }, []);

  // Load work types
  const load = () => {
    setLoading(true);
    api
      .get<{ data: LabWorkType[] }>("/api/lab-work-types")
      .then((r) => {
        const items = r.data.data ?? (r.data as unknown as LabWorkType[]);
        // Sort by sortOrder by default
        items.sort((a: LabWorkType, b: LabWorkType) => a.sortOrder - b.sortOrder);
        setWorkTypes(items);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  // Filtered work types
  const filteredTypes = workTypes.filter((wt) => {
    if (!searchText) return true;
    const q = searchText.toLowerCase();
    return (
      wt.name.toLowerCase().includes(q) ||
      (wt.nameAr?.toLowerCase() ?? "").includes(q) ||
      (wt.category?.toLowerCase() ?? "").includes(q)
    );
  });

  // Form handlers
  const handleOpenAdd = () => {
    setForm({ ...EMPTY_FORM, sortOrder: workTypes.length });
    setEditingType(null);
    setFormError("");
    setShowForm(true);
  };

  const handleOpenEdit = (wt: LabWorkType) => {
    setForm({
      name: wt.name,
      nameAr: wt.nameAr ?? "",
      category: wt.category ?? "",
      sortOrder: wt.sortOrder,
      isActive: wt.isActive,
    });
    setEditingType(wt);
    setFormError("");
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingType(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) {
      setFormError("اسم نوع العمل مطلوب");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload = {
        name: form.name.trim(),
        nameAr: form.nameAr.trim() || undefined,
        category: form.category.trim() || undefined,
        sortOrder: form.sortOrder,
        ...(editingType ? { isActive: form.isActive } : {}),
      };

      if (editingType) {
        await api.put(`/api/lab-work-types/${editingType.id}`, payload);
        showToast("تم تحديث نوع العمل بنجاح");
      } else {
        await api.post("/api/lab-work-types", payload);
        showToast("تم إضافة نوع العمل بنجاح");
      }

      handleCloseForm();
      load();
    } catch (err) {
      setFormError(extractErrorMessage(err, "حدث خطأ أثناء الحفظ"));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (wt: LabWorkType) => {
    setConfirmDialog({
      open: true,
      title: "حذف نوع العمل",
      message: `هل أنت متأكد من حذف نوع العمل "${wt.nameAr || wt.name}"؟ سيتم تعطيله ولن يظهر في القوائم.`,
      variant: "danger",
      onConfirm: async () => {
        setConfirmDialog((prev) => ({ ...prev, open: false }));
        try {
          await api.delete(`/api/lab-work-types/${wt.id}`);
          showToast("تم حذف نوع العمل");
          load();
        } catch {
          showToast("حدث خطأ أثناء الحذف", "error");
        }
      },
    });
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Toast */}
      {toast && (
        <div
          className={cn(
            "fixed top-4 left-1/2 -translate-x-1/2 z-[100] px-4 py-2.5 rounded-lg shadow-lg text-sm font-medium flex items-center gap-2 animate-in fade-in",
            toast.type === "success" ? "bg-green-600 text-white" : "bg-red-600 text-white"
          )}
        >
          {toast.type === "success" ? <CheckCircle2 className="w-4 h-4" /> : <AlertTriangle className="w-4 h-4" />}
          {toast.message}
        </div>
      )}

      {/* Header */}
      <div className="flex items-center gap-3">
        <Link
          href="/settings"
          className="text-gray-400 hover:text-gray-700 transition text-sm"
        >
          الإعدادات
        </Link>
        <span className="text-gray-300">/</span>
        <h1 className="text-2xl font-extrabold text-gray-900">أنواع أعمال المعمل</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">إدارة تصنيفات الأعمال التي يتم إرسالها للمعامل الخارجية</p>

      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
        <div className="relative flex-1 w-full sm:max-w-xs">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="بحث بالاسم أو التصنيف..."
            className={cn(inputCls, "pr-9")}
          />
        </div>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          نوع عمل جديد
        </button>
      </div>

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && workTypes.length === 0 && (
        <div className="text-center py-16">
          <Wrench className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد أنواع أعمال</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول نوع عمل بالضغط على زر &quot;نوع عمل جديد&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && workTypes.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "الاسم",
                  "الاسم بالعربية",
                  "التصنيف",
                  "ترتيب العرض",
                  "الحالة",
                  "إجراءات",
                ].map((h) => (
                  <th
                    key={h}
                    className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filteredTypes.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                    لا توجد نتائج مطابقة للبحث
                  </td>
                </tr>
              ) : (
                filteredTypes.map((wt) => (
                  <tr key={wt.id} className={cn("hover:bg-gray-50 transition", !wt.isActive && "opacity-60")}>
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {wt.name}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {wt.nameAr || "—"}
                    </td>
                    <td className="px-4 py-3">
                      {wt.category ? (
                        <span
                          className={cn(
                            "text-xs px-2 py-0.5 rounded-full font-medium",
                            CATEGORY_COLORS[wt.category] ?? "bg-gray-50 text-gray-700"
                          )}
                        >
                          {CATEGORY_LABELS[wt.category] ?? wt.category}
                        </span>
                      ) : (
                        <span className="text-gray-400">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      <span className="flex items-center gap-1">
                        <ArrowUpDown className="w-3 h-3 text-gray-400" />
                        {wt.sortOrder}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          "text-xs px-2 py-0.5 rounded-full font-medium",
                          wt.isActive
                            ? "bg-green-50 text-green-700"
                            : "bg-gray-100 text-gray-500"
                        )}
                      >
                        {wt.isActive ? "نشط" : "معطّل"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleOpenEdit(wt)}
                          title="تعديل نوع العمل"
                          className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
                        >
                          <Edit3 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleDelete(wt)}
                          title="حذف نوع العمل"
                          className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Add / Edit LabWorkType Modal */}
      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={handleCloseForm} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
                <Wrench className="w-5 h-5 text-clinic-blue" />
                {editingType ? "تعديل نوع العمل" : "إضافة نوع عمل جديد"}
              </h3>
              <button onClick={handleCloseForm} className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition">
                <X className="w-5 h-5" />
              </button>
            </div>

            {formError && (
              <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{formError}</p>
            )}

            <form onSubmit={handleSubmit} className="space-y-3">
              {/* Name */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  اسم نوع العمل (إنجليزي) <span className="text-red-500">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  className={inputCls}
                  placeholder="Porcelain Crown"
                  dir="ltr"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {/* Name Arabic */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    الاسم بالعربية
                  </label>
                  <input
                    value={form.nameAr}
                    onChange={(e) => setForm({ ...form, nameAr: e.target.value })}
                    className={inputCls}
                    placeholder="تاج بورسلين"
                  />
                </div>

                {/* Category */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">التصنيف</label>
                  <select
                    value={form.category}
                    onChange={(e) => setForm({ ...form, category: e.target.value })}
                    className={inputCls}
                  >
                    <option value="">بدون تصنيف</option>
                    {CATEGORIES.map((c) => (
                      <option key={c} value={c}>
                        {CATEGORY_LABELS[c]}
                      </option>
                    ))}
                  </select>
                </div>

                {/* Sort Order */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">ترتيب العرض</label>
                  <input
                    type="number"
                    min={0}
                    value={form.sortOrder}
                    onChange={(e) =>
                      setForm({ ...form, sortOrder: parseInt(e.target.value) || 0 })
                    }
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
              </div>

              {/* Active Toggle (edit only) */}
              {editingType && (
                <label className="flex items-center gap-2 text-xs font-medium text-gray-700 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                    className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
                  />
                  نوع عمل نشط
                </label>
              )}

              {/* Actions */}
              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  onClick={handleCloseForm}
                  className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
                >
                  {saving ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <Save className="w-4 h-4" />
                  )}
                  {saving
                    ? "جارٍ الحفظ..."
                    : editingType
                    ? "تحديث نوع العمل"
                    : "إضافة نوع العمل"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Confirm Dialog */}
      <ConfirmDialog
        open={confirmDialog.open}
        title={confirmDialog.title}
        message={confirmDialog.message}
        variant={confirmDialog.variant}
        onConfirm={confirmDialog.onConfirm}
        onCancel={() => setConfirmDialog((prev) => ({ ...prev, open: false }))}
      />
    </div>
  );
}
