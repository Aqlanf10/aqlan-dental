"use client";
import { useEffect, useState } from "react";
import { Plus, Edit3, Power, PowerOff, X, Save, Package, Trash2 } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type {
  TreatmentPackage,
  CreateTreatmentPackageRequest,
} from "@/types/appointment";

// ─── Constants ────────────────────────────────────────────────────────────────

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

interface PackageForm {
  name: string;
  description: string;
  totalPrice: string;
  sessionCount: number;
  color: string;
  isActive: boolean;
}

const EMPTY_FORM: PackageForm = {
  name: "",
  description: "",
  totalPrice: "0",
  sessionCount: 1,
  color: "#8b5cf6",
  isActive: true,
};

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function TreatmentPackagesPage() {
  const [packages, setPackages] = useState<TreatmentPackage[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<PackageForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [showInactive, setShowInactive] = useState(false);

  const load = () => {
    setLoading(true);
    api
      .get<TreatmentPackage[]>(`/api/treatment-packages?activeOnly=${!showInactive}`)
      .then((r) => setPackages(r.data ?? []))
      .catch(() => setPackages([]))
      .finally(() => setLoading(false));
  };

  useEffect(load, [showInactive]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleOpenAdd = () => {
    setForm(EMPTY_FORM);
    setEditingId(null);
    setFormError("");
    setShowForm(true);
  };

  const handleOpenEdit = (p: TreatmentPackage) => {
    setForm({
      name: p.name,
      description: p.description ?? "",
      totalPrice: String(p.totalPrice),
      sessionCount: p.sessionCount,
      color: p.color ?? "#8b5cf6",
      isActive: p.isActive,
    });
    setEditingId(p.id);
    setFormError("");
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) {
      setFormError("اسم الباقة مطلوب");
      return;
    }
    const totalPrice = parseFloat(form.totalPrice);
    if (isNaN(totalPrice) || totalPrice < 0) {
      setFormError("السعر الإجمالي يجب أن يكون رقمًا موجبًا");
      return;
    }
    if (form.sessionCount < 1) {
      setFormError("عدد الجلسات يجب أن يكون 1 على الأقل");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload: CreateTreatmentPackageRequest = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        totalPrice,
        sessionCount: form.sessionCount,
        color: form.color || null,
        isActive: form.isActive,
      };

      if (editingId) {
        await api.put(`/api/treatment-packages/${editingId}`, payload);
      } else {
        await api.post("/api/treatment-packages", payload);
      }

      handleCloseForm();
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setFormError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const handleToggle = async (p: TreatmentPackage) => {
    setTogglingId(p.id);
    try {
      // Toggle via PUT (update IsActive). The endpoint is admin-only.
      await api.put(`/api/treatment-packages/${p.id}`, {
        name: p.name,
        description: p.description ?? null,
        totalPrice: p.totalPrice,
        sessionCount: p.sessionCount,
        color: p.color ?? null,
        isActive: !p.isActive,
      });
      load();
    } catch {
      // ignore — settings page best-effort
    } finally {
      setTogglingId(null);
    }
  };

  const handleDelete = async (p: TreatmentPackage) => {
    if (!confirm(`هل أنت متأكد من حذف الباقة "${p.name}"؟`)) return;
    try {
      await api.delete(`/api/treatment-packages/${p.id}`);
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      alert(msg ?? "فشل حذف الباقة");
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link
          href="/settings"
          className="text-gray-400 hover:text-gray-700 transition text-sm"
        >
          الإعدادات
        </Link>
        <span className="text-gray-300">/</span>
        <h1 className="text-2xl font-extrabold text-gray-900">باقات العلاج</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">
        كتالوج باقات العلاج (تبييض، تقويم، إلخ) — تُستخدم لتعبئة نوع الموعد تلقائيًا ولون الموعد على التقويم
      </p>

      {/* Toolbar */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <p className="text-sm text-gray-500">{packages.length} باقة</p>
          <label className="flex items-center gap-1.5 text-xs text-gray-600 cursor-pointer">
            <input
              type="checkbox"
              checked={showInactive}
              onChange={(e) => setShowInactive(e.target.checked)}
              className="w-3.5 h-3.5"
            />
            إظهار المعطّلة
          </label>
        </div>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          باقة جديدة
        </button>
      </div>

      {/* Form Modal */}
      {showForm && (
        <form
          onSubmit={handleSubmit}
          className="bg-gray-50 rounded-xl border border-gray-200 p-5 space-y-4"
        >
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold text-gray-800">
              {editingId ? "تعديل الباقة" : "إضافة باقة جديدة"}
            </p>
            <button
              type="button"
              onClick={handleCloseForm}
              className="text-gray-400 hover:text-gray-700 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {formError && (
            <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{formError}</p>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            {/* Name */}
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اسم الباقة <span className="text-red-500">*</span>
              </label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className={inputCls}
                placeholder="مثال: باقة تبييض كاملة"
              />
            </div>
            {/* Color */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اللون (لعرض التقويم)
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={form.color || "#8b5cf6"}
                  onChange={(e) => setForm({ ...form, color: e.target.value })}
                  className="w-10 h-9 p-1 rounded-lg border border-gray-300 cursor-pointer bg-white"
                />
                <input
                  value={form.color}
                  onChange={(e) => setForm({ ...form, color: e.target.value })}
                  className={cn(inputCls, "w-28 font-mono text-xs")}
                  dir="ltr"
                  placeholder="#8b5cf6"
                />
              </div>
            </div>
            {/* Total Price */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                السعر الإجمالي <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={form.totalPrice}
                onChange={(e) => setForm({ ...form, totalPrice: e.target.value })}
                className={inputCls}
                dir="ltr"
              />
            </div>
            {/* Session Count */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                عدد الجلسات
              </label>
              <input
                type="number"
                min={1}
                value={form.sessionCount}
                onChange={(e) =>
                  setForm({ ...form, sessionCount: parseInt(e.target.value) || 1 })
                }
                className={inputCls}
                dir="ltr"
              />
            </div>
            {/* Active toggle */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الحالة</label>
              <label className="flex items-center gap-2 h-9 px-2 text-sm">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  className="w-4 h-4"
                />
                {form.isActive ? "نشطة" : "معطّلة"}
              </label>
            </div>
            {/* Description */}
            <div className="md:col-span-2 lg:col-span-3">
              <label className="block text-xs font-medium text-gray-600 mb-1">الوصف</label>
              <textarea
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className={inputCls}
                rows={2}
                placeholder="وصف اختياري لمحتوى الباقة..."
              />
            </div>
          </div>

          {/* Submit */}
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
              <Save className="w-4 h-4" />
              {saving ? "جارٍ الحفظ..." : editingId ? "تحديث الباقة" : "إضافة الباقة"}
            </button>
          </div>
        </form>
      )}

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-14 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && packages.length === 0 && (
        <div className="text-center py-16">
          <Package className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد باقات</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول باقة بالضغط على زر &quot;باقة جديدة&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && packages.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {["الباقة", "السعر", "الجلسات", "اللون", "الحالة", "إجراءات"].map((h) => (
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
              {packages.map((p) => (
                <tr key={p.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{p.name}</div>
                    {p.description && (
                      <div className="text-xs text-gray-400 mt-0.5 line-clamp-1">
                        {p.description}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-gray-700" dir="ltr">
                    {p.totalPrice.toLocaleString("en-US")}
                  </td>
                  <td className="px-4 py-3 text-gray-700">{p.sessionCount}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5">
                      <span
                        className="w-5 h-5 rounded-md border border-gray-200"
                        style={{ backgroundColor: p.color ?? "#cccccc" }}
                      />
                      <span className="text-xs font-mono text-gray-500" dir="ltr">
                        {p.color ?? "—"}
                      </span>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        p.isActive
                          ? "bg-green-50 text-green-700"
                          : "bg-gray-100 text-gray-500"
                      )}
                    >
                      {p.isActive ? "نشط" : "معطّل"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleOpenEdit(p)}
                        title="تعديل الباقة"
                        className="text-gray-400 hover:text-clinic-blue transition p-1"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleToggle(p)}
                        disabled={togglingId === p.id}
                        title={p.isActive ? "تعطيل الباقة" : "تفعيل الباقة"}
                        className="text-gray-400 hover:text-gray-700 transition p-1 disabled:opacity-50"
                      >
                        {p.isActive ? (
                          <PowerOff className="w-4 h-4" />
                        ) : (
                          <Power className="w-4 h-4 text-green-600" />
                        )}
                      </button>
                      <button
                        onClick={() => handleDelete(p)}
                        title="حذف الباقة"
                        className="text-gray-400 hover:text-red-600 transition p-1"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Info box */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 text-xs text-blue-800">
        <p>
          💡 الباقات كتالوج بسيط لاستخدامه عند إنشاء المواعيد. اختيار باقة يعبّئ نوع الموعد من اسمها ويستخدم
          لونها على التقويم. التكامل مع العقود والأقساط سيُضاف في Sprint 2.
        </p>
      </div>
    </div>
  );
}
