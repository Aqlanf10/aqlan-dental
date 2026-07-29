import { useEffect, useState, useCallback } from "react";
import {
  Plus, Search, Edit3, Trash2, X, Save, Building2,
  Phone, MessageCircle, User, Mail, MapPin, FileText,
  CheckCircle2, AlertTriangle, Loader2,
} from "lucide-react";
import Link from "@/lib/nextLinkCompat";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import type { Lab } from "@/types/lab";

// ─── Types ────────────────────────────────────────────────────────────────────

interface LabForm {
  name: string;
  phone: string;
  whatsApp: string;
  address: string;
  contactPerson: string;
  email: string;
  notes: string;
  isActive: boolean;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const EMPTY_FORM: LabForm = {
  name: "",
  phone: "",
  whatsApp: "",
  address: "",
  contactPerson: "",
  email: "",
  notes: "",
  isActive: true,
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Helper ───────────────────────────────────────────────────────────────────
// FE-11: getApiErrorMessage removed — use extractErrorMessage from @/lib/errors.

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function LabsSettingsPage() {
  const [labs, setLabs] = useState<Lab[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchText, setSearchText] = useState("");

  // Dialog state
  const [showForm, setShowForm] = useState(false);
  const [editingLab, setEditingLab] = useState<Lab | null>(null);
  const [form, setForm] = useState<LabForm>(EMPTY_FORM);
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

  // Load labs
  const load = () => {
    setLoading(true);
    api
      .get<{ data: Lab[] }>("/api/labs")
      .then((r) => setLabs(r.data.data ?? r.data as unknown as Lab[]))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  // Filtered labs
  const filteredLabs = labs.filter((lab) => {
    if (!searchText) return true;
    const q = searchText.toLowerCase();
    return (
      lab.name.toLowerCase().includes(q) ||
      (lab.phone?.toLowerCase() ?? "").includes(q) ||
      (lab.contactPerson?.toLowerCase() ?? "").includes(q) ||
      (lab.email?.toLowerCase() ?? "").includes(q)
    );
  });

  // Form handlers
  const handleOpenAdd = () => {
    setForm(EMPTY_FORM);
    setEditingLab(null);
    setFormError("");
    setShowForm(true);
  };

  const handleOpenEdit = (lab: Lab) => {
    setForm({
      name: lab.name,
      phone: lab.phone ?? "",
      whatsApp: lab.whatsApp ?? "",
      address: lab.address ?? "",
      contactPerson: lab.contactPerson ?? "",
      email: lab.email ?? "",
      notes: lab.notes ?? "",
      isActive: lab.isActive,
    });
    setEditingLab(lab);
    setFormError("");
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingLab(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) {
      setFormError("اسم المعمل مطلوب");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload = {
        name: form.name.trim(),
        phone: form.phone.trim() || undefined,
        whatsApp: form.whatsApp.trim() || undefined,
        address: form.address.trim() || undefined,
        contactPerson: form.contactPerson.trim() || undefined,
        email: form.email.trim() || undefined,
        notes: form.notes.trim() || undefined,
        ...(editingLab ? { isActive: form.isActive } : {}),
      };

      if (editingLab) {
        await api.put(`/api/labs/${editingLab.id}`, payload);
        showToast("تم تحديث المعمل بنجاح");
      } else {
        await api.post("/api/labs", payload);
        showToast("تم إضافة المعمل بنجاح");
      }

      handleCloseForm();
      load();
    } catch (err) {
      setFormError(extractErrorMessage(err, "حدث خطأ أثناء الحفظ"));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (lab: Lab) => {
    setConfirmDialog({
      open: true,
      title: "حذف المعمل",
      message: `هل أنت متأكد من حذف المعمل "${lab.name}"؟ سيتم تعطيله ولن يظهر في القوائم.`,
      variant: "danger",
      onConfirm: async () => {
        setConfirmDialog((prev) => ({ ...prev, open: false }));
        try {
          await api.delete(`/api/labs/${lab.id}`);
          showToast("تم حذف المعمل");
          load();
        } catch {
          showToast("حدث خطأ أثناء الحذف", "error");
        }
      },
    });
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-6xl">
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
        <h1 className="text-2xl font-extrabold text-gray-900">المعامل</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">إدارة بيانات المعامل الخارجية وجهات الاتصال</p>

      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
        <div className="relative flex-1 w-full sm:max-w-xs">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="بحث باسم المعمل أو الهاتف..."
            className={cn(inputCls, "pr-9")}
          />
        </div>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          معمل جديد
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
      {!loading && labs.length === 0 && (
        <div className="text-center py-16">
          <Building2 className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد معامل</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول معمل بالضغط على زر &quot;معمل جديد&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && labs.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "اسم المعمل",
                  "الهاتف",
                  "واتساب",
                  "شخص الاتصال",
                  "البريد",
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
              {filteredLabs.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-gray-400">
                    لا توجد نتائج مطابقة للبحث
                  </td>
                </tr>
              ) : (
                filteredLabs.map((lab) => (
                  <tr key={lab.id} className={cn("hover:bg-gray-50 transition", !lab.isActive && "opacity-60")}>
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-900">{lab.name}</div>
                      {lab.address && (
                        <div className="text-xs text-gray-400 mt-0.5 flex items-center gap-1">
                          <MapPin className="w-3 h-3" />
                          {lab.address}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-600" dir="ltr">
                      {lab.phone || "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600" dir="ltr">
                      {lab.whatsApp || "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {lab.contactPerson || "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs" dir="ltr">
                      {lab.email || "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          "text-xs px-2 py-0.5 rounded-full font-medium",
                          lab.isActive
                            ? "bg-green-50 text-green-700"
                            : "bg-gray-100 text-gray-500"
                        )}
                      >
                        {lab.isActive ? "نشط" : "معطّل"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleOpenEdit(lab)}
                          title="تعديل المعمل"
                          className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
                        >
                          <Edit3 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleDelete(lab)}
                          title="حذف المعمل"
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

      {/* Add / Edit Lab Modal */}
      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={handleCloseForm} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
                <Building2 className="w-5 h-5 text-clinic-blue" />
                {editingLab ? "تعديل المعمل" : "إضافة معمل جديد"}
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
                  اسم المعمل <span className="text-red-500">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  className={inputCls}
                  placeholder="معمل النور لطقم الأسنان"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {/* Phone */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    <Phone className="w-3 h-3 inline ml-1" />
                    الهاتف
                  </label>
                  <input
                    value={form.phone}
                    onChange={(e) => setForm({ ...form, phone: e.target.value })}
                    className={inputCls}
                    placeholder="04-253028"
                    dir="ltr"
                  />
                </div>

                {/* WhatsApp */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    <MessageCircle className="w-3 h-3 inline ml-1" />
                    واتساب
                  </label>
                  <input
                    value={form.whatsApp}
                    onChange={(e) => setForm({ ...form, whatsApp: e.target.value })}
                    className={inputCls}
                    placeholder="967770XXXXXX"
                    dir="ltr"
                  />
                </div>

                {/* Contact Person */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    <User className="w-3 h-3 inline ml-1" />
                    شخص الاتصال
                  </label>
                  <input
                    value={form.contactPerson}
                    onChange={(e) => setForm({ ...form, contactPerson: e.target.value })}
                    className={inputCls}
                    placeholder="أحمد محمد"
                  />
                </div>

                {/* Email */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    <Mail className="w-3 h-3 inline ml-1" />
                    البريد الإلكتروني
                  </label>
                  <input
                    value={form.email}
                    onChange={(e) => setForm({ ...form, email: e.target.value })}
                    type="email"
                    className={inputCls}
                    placeholder="lab@example.com"
                    dir="ltr"
                  />
                </div>
              </div>

              {/* Address */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  <MapPin className="w-3 h-3 inline ml-1" />
                  العنوان
                </label>
                <input
                  value={form.address}
                  onChange={(e) => setForm({ ...form, address: e.target.value })}
                  className={inputCls}
                  placeholder="تعز، شارع المطار"
                />
              </div>

              {/* Notes */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  <FileText className="w-3 h-3 inline ml-1" />
                  ملاحظات
                </label>
                <textarea
                  value={form.notes}
                  onChange={(e) => setForm({ ...form, notes: e.target.value })}
                  className={cn(inputCls, "resize-none")}
                  rows={3}
                  placeholder="ملاحظات إضافية عن المعمل..."
                />
              </div>

              {/* Active Toggle (edit only) */}
              {editingLab && (
                <label className="flex items-center gap-2 text-xs font-medium text-gray-700 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                    className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
                  />
                  معمل نشط
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
                    : editingLab
                    ? "تحديث المعمل"
                    : "إضافة المعمل"}
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
