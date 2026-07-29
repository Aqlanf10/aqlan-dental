"use client";
import { useEffect, useState } from "react";
import { Plus, Edit3, Power, PowerOff, X, Save, CreditCard, Hash } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";

// ─── Types ────────────────────────────────────────────────────────────────────

interface PaymentMethodRow {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  requiresReferenceNumber: boolean;
  accountName: string | null;
  accountNumber: string | null;
  notes: string | null;
  sortOrder: number;
  branchId: string | null;
}

interface PaymentMethodForm {
  name: string;
  code: string;
  isActive: boolean;
  requiresReferenceNumber: boolean;
  accountName: string;
  accountNumber: string;
  notes: string;
  sortOrder: number;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const EMPTY_FORM: PaymentMethodForm = {
  name: "",
  code: "",
  isActive: true,
  requiresReferenceNumber: false,
  accountName: "",
  accountNumber: "",
  notes: "",
  sortOrder: 0,
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PaymentMethodsSettingsPage() {
  const { user } = useAuthStore();
  const canManage = useHasPermission(PERMISSION_KEYS.SETTINGS_PAYMENT_METHODS_MANAGE);

  const [methods, setMethods] = useState<PaymentMethodRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<PaymentMethodForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const load = () => {
    setLoading(true);
    api
      .get<PaymentMethodRow[]>("/api/settings/payment-methods")
      .then((r) => setMethods(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  // Redirect if not authorized
  if (user?.role !== "Admin" && !canManage) {
    return (
      <div className="space-y-5 max-w-5xl">
        <div className="flex items-center gap-3">
          <Link href="/settings" className="text-gray-400 hover:text-gray-700 transition text-sm">
            الإعدادات
          </Link>
          <span className="text-gray-300">/</span>
          <h1 className="text-2xl font-extrabold text-gray-900">طرق الدفع</h1>
        </div>
        <div className="text-center py-16">
          <CreditCard className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">ليس لديك صلاحية الوصول لهذه الصفحة</p>
        </div>
      </div>
    );
  }

  const handleOpenAdd = () => {
    setForm(EMPTY_FORM);
    setEditingId(null);
    setFormError("");
    setShowForm(true);
  };

  const handleOpenEdit = (m: PaymentMethodRow) => {
    setForm({
      name: m.name,
      code: m.code,
      isActive: m.isActive,
      requiresReferenceNumber: m.requiresReferenceNumber,
      accountName: m.accountName ?? "",
      accountNumber: m.accountNumber ?? "",
      notes: m.notes ?? "",
      sortOrder: m.sortOrder,
    });
    setEditingId(m.id);
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
      setFormError("اسم طريقة الدفع مطلوب");
      return;
    }
    if (!form.code.trim()) {
      setFormError("كود طريقة الدفع مطلوب");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload = {
        name: form.name,
        code: form.code,
        isActive: form.isActive,
        requiresReferenceNumber: form.requiresReferenceNumber,
        accountName: form.accountName || null,
        accountNumber: form.accountNumber || null,
        notes: form.notes || null,
        sortOrder: form.sortOrder,
      };

      if (editingId) {
        await api.put(`/api/settings/payment-methods/${editingId}`, payload);
      } else {
        await api.post("/api/settings/payment-methods", payload);
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

  const handleToggle = async (id: string) => {
    setTogglingId(id);
    try {
      await api.post(`/api/settings/payment-methods/${id}/toggle`);
      load();
    } catch {
      // silent fail
    } finally {
      setTogglingId(null);
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
        <h1 className="text-2xl font-extrabold text-gray-900">طرق الدفع</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">إدارة طرق الدفع المتاحة في النظام</p>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{methods.length} طريقة دفع</p>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          طريقة دفع جديدة
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
              {editingId ? "تعديل طريقة الدفع" : "إضافة طريقة دفع جديدة"}
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
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الاسم <span className="text-red-500">*</span>
              </label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className={inputCls}
                placeholder="نقدي"
              />
            </div>
            {/* Code */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الكود <span className="text-red-500">*</span>
              </label>
              <input
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                className={inputCls}
                placeholder="cash"
                dir="ltr"
              />
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
            {/* Account Name */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم الحساب</label>
              <input
                value={form.accountName}
                onChange={(e) => setForm({ ...form, accountName: e.target.value })}
                className={inputCls}
                placeholder="اختياري"
              />
            </div>
            {/* Account Number */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">رقم الحساب</label>
              <input
                value={form.accountNumber}
                onChange={(e) => setForm({ ...form, accountNumber: e.target.value })}
                className={inputCls}
                placeholder="اختياري"
                dir="ltr"
              />
            </div>
            {/* Notes */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
              <input
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                className={inputCls}
                placeholder="اختياري"
              />
            </div>
          </div>

          {/* Checkboxes */}
          <div className="flex items-center gap-6">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                className="w-4 h-4 rounded border-gray-300 accent-clinic-blue"
              />
              <span className="text-sm text-gray-700">نشط</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.requiresReferenceNumber}
                onChange={(e) => setForm({ ...form, requiresReferenceNumber: e.target.checked })}
                className="w-4 h-4 rounded border-gray-300 accent-clinic-blue"
              />
              <span className="text-sm text-gray-700">يتطلب رقم مرجعي</span>
            </label>
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
              {saving ? "جارٍ الحفظ..." : editingId ? "تحديث طريقة الدفع" : "إضافة طريقة الدفع"}
            </button>
          </div>
        </form>
      )}

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && methods.length === 0 && (
        <div className="text-center py-16">
          <CreditCard className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد طرق دفع</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول طريقة دفع بالضغط على زر &quot;طريقة دفع جديدة&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && methods.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "الاسم",
                  "الكود",
                  "الحالة",
                  "رقم مرجعي",
                  "اسم الحساب",
                  "رقم الحساب",
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
              {methods.map((m) => (
                <tr key={m.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{m.name}</div>
                    {m.notes && (
                      <div className="text-xs text-gray-400 mt-0.5">{m.notes}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-600" dir="ltr">
                    {m.code}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        m.isActive
                          ? "bg-green-50 text-green-700"
                          : "bg-gray-100 text-gray-500"
                      )}
                    >
                      {m.isActive ? "نشط" : "معطّل"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    {m.requiresReferenceNumber ? (
                      <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 border border-amber-200">
                        <Hash className="w-3 h-3" />
                        الرقم المرجعي إلزامي
                      </span>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-600">
                    {m.accountName || "—"}
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-600 font-mono" dir="ltr">
                    {m.accountNumber || "—"}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleOpenEdit(m)}
                        title="تعديل طريقة الدفع"
                        className="text-gray-400 hover:text-clinic-blue transition p-1"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleToggle(m.id)}
                        disabled={togglingId === m.id}
                        title={m.isActive ? "تعطيل طريقة الدفع" : "تفعيل طريقة الدفع"}
                        className="text-gray-400 hover:text-gray-700 transition p-1 disabled:opacity-50"
                      >
                        {m.isActive ? (
                          <PowerOff className="w-4 h-4" />
                        ) : (
                          <Power className="w-4 h-4 text-green-600" />
                        )}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
