"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Settings, Users, Shield, Save, Plus, X, UserCheck, UserX,
  Building2, FileText, Lock, Search, Download, ChevronRight,
  ChevronLeft, RotateCcw, Trash2, Edit2, CheckCircle,
  Eye, EyeOff,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import {
  useBranches,
  useCreateBranch,
  useUpdateBranch,
  useDeleteBranch,
  useRolePermissions,
  useBulkUpdateRolePermissions,
  useCreateRolePermission,
  useDeleteRolePermission,
  useChangePassword,
  useAuditLogs,
  useExportAuditLogs,
  type BranchDto,
  type RolePermissionDto,
  type CreateBranchRequest,
  type CreateRolePermissionRequest,
  type BulkUpdateRolePermissionItem,
} from "@/hooks/useSettings";

type Tab = "clinic" | "users" | "roles" | "branches" | "audit" | "password";

const TABS: { key: Tab; label: string; icon: typeof Settings }[] = [
  { key: "clinic", label: "بيانات المركز", icon: Settings },
  { key: "users", label: "المستخدمون", icon: Users },
  { key: "roles", label: "الأدوار", icon: Shield },
  { key: "branches", label: "الفروع", icon: Building2 },
  { key: "audit", label: "سجل التدقيق", icon: FileText },
  { key: "password", label: "تغيير كلمة المرور", icon: Lock },
];

interface ClinicSettings {
  "clinic.name"?: string;
  "clinic.location"?: string;
  "clinic.phones"?: string;
  "clinic.currency"?: string;
  [key: string]: string | undefined;
}

interface UserRow {
  id: string;
  username: string;
  email?: string;
  role: string;
  isActive: boolean;
  lastLoginAt?: string;
  doctorName?: string;
}

const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";

const ALL_ROLES = [
  "Admin",
  "Orthodontist",
  "GeneralDentist",
  "OralSurgeon",
  "Reception",
  "Accountant",
  "Assistant",
  "BranchManager",
] as const;

// ─── Audit Tab Constants ──────────────────────────────────────────────────────

const ACTION_LABELS: Record<string, string> = {
  Create: "إنشاء",
  Update: "تحديث",
  Delete: "حذف",
  View: "عرض",
  Export: "تصدير",
  Login: "دخول",
  Logout: "خروج",
  Approve: "اعتماد",
};

const ACTION_COLORS: Record<string, string> = {
  Create: "bg-green-100 text-green-700 border-green-200",
  Update: "bg-blue-100 text-blue-700 border-blue-200",
  Delete: "bg-red-100 text-red-700 border-red-200",
  View: "bg-gray-100 text-gray-600 border-gray-200",
  Export: "bg-purple-100 text-purple-700 border-purple-200",
  Login: "bg-teal-100 text-teal-700 border-teal-200",
  Logout: "bg-orange-100 text-orange-700 border-orange-200",
  Approve: "bg-amber-100 text-amber-700 border-amber-200",
};

const RESOURCE_LABELS: Record<string, string> = {
  patients: "المرضى",
  appointments: "المواعيد",
  ortho_cases: "حالات التقويم",
  ortho: "التقويم",
  ceph: "السيفالومتري",
  surgery_cases: "حالات الجراحة",
  surgery: "الجراحة",
  general_treatments: "العلاجات العامة",
  general: "طب الأسنان العام",
  contracts: "العقود",
  payments: "الدفعات",
  finance: "المالية",
  expenses: "المصروفات",
  users: "المستخدمون",
  settings: "الإعدادات",
  branches: "الفروع",
  role_permissions: "صلاحيات الأدوار",
  inventory: "المخزون",
  prescriptions: "الوصفات الطبية",
  lab: "المختبر",
  referrals: "الإحالات",
  reports: "التقارير",
  auth: "المصادقة",
};

const ACTION_OPTIONS = [
  { value: "", label: "جميع الإجراءات" },
  { value: "Create", label: "إنشاء" },
  { value: "Update", label: "تحديث" },
  { value: "Delete", label: "حذف" },
  { value: "View", label: "عرض" },
  { value: "Export", label: "تصدير" },
  { value: "Login", label: "دخول" },
  { value: "Logout", label: "خروج" },
  { value: "Approve", label: "اعتماد" },
];

function beautifyResource(resource: string): string {
  return (
    RESOURCE_LABELS[resource] ??
    RESOURCE_LABELS[resource.toLowerCase()] ??
    resource
  );
}

function formatAuditDate(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString("ar-YE", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return dateStr;
  }
}

// ─── Clinic Info Tab ──────────────────────────────────────────────────────────
function ClinicTab() {
  const [settings, setSettings] = useState<ClinicSettings>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api
      .get<ClinicSettings>("/api/settings")
      .then((r) => setSettings(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const clinicFields = [
        "clinic.name",
        "clinic.location",
        "clinic.phones",
      ] as const;
      await Promise.all(
        clinicFields.map((key) =>
          api.put(`/api/settings/${encodeURIComponent(key)}`, {
            value: settings[key] ?? "",
            category: "clinic",
          })
        )
      );
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="animate-pulse space-y-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-10 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">
          اسم المركز
        </label>
        <input
          value={settings["clinic.name"] ?? ""}
          onChange={(e) =>
            setSettings({ ...settings, "clinic.name": e.target.value })
          }
          className={inputCls}
          placeholder="مركز د. عقلان الكامل لطب وتقويم الأسنان"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">
          العنوان
        </label>
        <input
          value={settings["clinic.location"] ?? ""}
          onChange={(e) =>
            setSettings({ ...settings, "clinic.location": e.target.value })
          }
          className={inputCls}
          placeholder="تعز، اليمن"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">
          أرقام الهاتف
        </label>
        <input
          value={settings["clinic.phones"] ?? ""}
          onChange={(e) =>
            setSettings({ ...settings, "clinic.phones": e.target.value })
          }
          className={inputCls}
          placeholder="04-253028، 770XXXXXX"
          dir="ltr"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">
          بادئة رقم المريض
        </label>
        <input
          value={settings["patient.number_prefix"] ?? "GM"}
          onChange={(e) =>
            setSettings({
              ...settings,
              "patient.number_prefix": e.target.value,
            })
          }
          className={inputCls}
          placeholder="GM"
          dir="ltr"
        />
      </div>

      <div className="flex items-center gap-3 pt-2">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
        </button>
        {saved && (
          <span className="text-sm text-green-600 font-medium">
            ✓ تم الحفظ
          </span>
        )}
      </div>
    </div>
  );
}

// ─── Users Tab ────────────────────────────────────────────────────────────────
function UsersTab() {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    username: "",
    password: "",
    role: "Reception",
    email: "",
    doctorName: "",
    doctorColor: "#0E7490",
  });

  const load = () => {
    setLoading(true);
    api
      .get<UserRow[]>("/api/users")
      .then((r) => setUsers(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.username || !form.password) {
      setFormError("اسم المستخدم وكلمة المرور مطلوبان");
      return;
    }
    setSaving(true);
    setFormError("");
    try {
      await api.post("/api/users", {
        username: form.username,
        password: form.password,
        role: form.role,
        email: form.email || undefined,
        doctorName: form.doctorName || undefined,
        doctorColor: form.doctorName ? form.doctorColor : undefined,
      });
      setShowForm(false);
      setForm({
        username: "",
        password: "",
        role: "Reception",
        email: "",
        doctorName: "",
        doctorColor: "#0E7490",
      });
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })
        ?.response?.data?.message;
      setFormError(msg ?? "حدث خطأ");
    } finally {
      setSaving(false);
    }
  };

  const handleToggleStatus = async (id: string) => {
    await api.put(`/api/users/${id}/status`, {}).catch(() => {});
    load();
  };

  if (loading) {
    return (
      <div className="animate-pulse space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-12 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{users.length} مستخدم</p>
        <button
          onClick={() => setShowForm(!showForm)}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          {showForm ? <X className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
          {showForm ? "إلغاء" : "مستخدم جديد"}
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={handleCreate}
          className="bg-gray-50 rounded-xl border border-gray-200 p-4 space-y-3"
        >
          <p className="text-sm font-semibold text-gray-800">
            إضافة مستخدم جديد
          </p>
          {formError && (
            <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">
              {formError}
            </p>
          )}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اسم المستخدم <span className="text-red-500">*</span>
              </label>
              <input
                value={form.username}
                onChange={(e) =>
                  setForm({ ...form, username: e.target.value })
                }
                className={inputCls}
                placeholder="username"
                dir="ltr"
                autoComplete="off"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                كلمة المرور <span className="text-red-500">*</span>
              </label>
              <input
                value={form.password}
                onChange={(e) =>
                  setForm({ ...form, password: e.target.value })
                }
                type="password"
                className={inputCls}
                autoComplete="new-password"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الدور
              </label>
              <select
                value={form.role}
                onChange={(e) =>
                  setForm({ ...form, role: e.target.value })
                }
                className={inputCls}
              >
                {ALL_ROLES.map((r) => (
                  <option key={r} value={r}>
                    {ROLE_LABELS[r] ?? r}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                البريد الإلكتروني
              </label>
              <input
                value={form.email}
                onChange={(e) =>
                  setForm({ ...form, email: e.target.value })
                }
                type="email"
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اسم الطبيب (اختياري)
              </label>
              <input
                value={form.doctorName}
                onChange={(e) =>
                  setForm({ ...form, doctorName: e.target.value })
                }
                className={inputCls}
                placeholder="د. محمد أحمد"
              />
            </div>
            {form.doctorName && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  لون الطبيب
                </label>
                <input
                  value={form.doctorColor}
                  onChange={(e) =>
                    setForm({ ...form, doctorColor: e.target.value })
                  }
                  type="color"
                  className="h-9 w-full rounded-lg border border-gray-300 cursor-pointer"
                />
              </div>
            )}
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {saving ? "جارٍ الحفظ..." : "إضافة المستخدم"}
            </button>
          </div>
        </form>
      )}

      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {[
                "اسم المستخدم",
                "الاسم الكامل",
                "الدور",
                "آخر دخول",
                "الحالة",
                "",
              ].map((h) => (
                <th
                  key={h}
                  className="text-start px-4 py-3 text-xs font-semibold text-gray-500"
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map((u) => (
              <tr key={u.id} className="hover:bg-gray-50 transition">
                <td className="px-4 py-3 font-mono font-medium text-gray-900">
                  {u.username}
                </td>
                <td className="px-4 py-3 text-gray-700">
                  {u.doctorName ?? "—"}
                </td>
                <td className="px-4 py-3">
                  <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">
                    {ROLE_LABELS[u.role] ?? u.role}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-500 text-xs">
                  {u.lastLoginAt
                    ? new Date(u.lastLoginAt).toLocaleDateString("ar-YE")
                    : "—"}
                </td>
                <td className="px-4 py-3">
                  <span
                    className={cn(
                      "text-xs px-2 py-0.5 rounded-full font-medium",
                      u.isActive
                        ? "bg-green-50 text-green-700"
                        : "bg-gray-100 text-gray-500"
                    )}
                  >
                    {u.isActive ? "نشط" : "معطّل"}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <button
                    onClick={() => handleToggleStatus(u.id)}
                    title={u.isActive ? "تعطيل المستخدم" : "تفعيل المستخدم"}
                    className="text-gray-400 hover:text-gray-700 transition"
                  >
                    {u.isActive ? (
                      <UserX className="w-4 h-4" />
                    ) : (
                      <UserCheck className="w-4 h-4 text-green-600" />
                    )}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Branches Tab ─────────────────────────────────────────────────────────────
function BranchesTab() {
  const { data: branches, isLoading } = useBranches();
  const createMutation = useCreateBranch();
  const updateMutation = useUpdateBranch();
  const deleteMutation = useDeleteBranch();

  const [showForm, setShowForm] = useState(false);
  const [editingBranch, setEditingBranch] = useState<BranchDto | null>(null);
  const [form, setForm] = useState<CreateBranchRequest>({
    name: "",
    address: "",
    phone: "",
    isMain: false,
  });
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  const resetForm = useCallback(() => {
    setForm({ name: "", address: "", phone: "", isMain: false });
    setShowForm(false);
    setEditingBranch(null);
  }, []);

  const handleEdit = useCallback((branch: BranchDto) => {
    setEditingBranch(branch);
    setForm({
      name: branch.name,
      address: branch.address ?? "",
      phone: branch.phone ?? "",
      isMain: branch.isMain,
    });
    setShowForm(true);
  }, []);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (!form.name.trim()) {
        toast.error("اسم الفرع مطلوب");
        return;
      }
      try {
        if (editingBranch) {
          await updateMutation.mutateAsync({ id: editingBranch.id, ...form });
          toast.success("تم تحديث الفرع بنجاح");
        } else {
          await createMutation.mutateAsync(form);
          toast.success("تم إضافة الفرع بنجاح");
        }
        resetForm();
      } catch {
        toast.error("حدث خطأ أثناء الحفظ");
      }
    },
    [form, editingBranch, createMutation, updateMutation, resetForm]
  );

  const handleDelete = useCallback(
    async (id: string) => {
      try {
        await deleteMutation.mutateAsync(id);
        setConfirmDeleteId(null);
        toast.success("تم حذف الفرع");
      } catch {
        toast.error("حدث خطأ أثناء الحذف");
      }
    },
    [deleteMutation]
  );

  const toggleMain = useCallback(
    async (branch: BranchDto) => {
      try {
        await updateMutation.mutateAsync({
          id: branch.id,
          isMain: !branch.isMain,
        });
        toast.success(branch.isMain ? "تم إلغاء تعيين الفرع الرئيسي" : "تم تعيين الفرع كرئيسي");
      } catch {
        toast.error("حدث خطأ");
      }
    },
    [updateMutation]
  );

  if (isLoading) {
    return (
      <div className="animate-pulse space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-12 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">
          {(branches ?? []).length} فرع
        </p>
        <button
          onClick={() => {
            resetForm();
            setShowForm(true);
          }}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          فرع جديد
        </button>
      </div>

      {showForm && (
        <form
          onSubmit={handleSubmit}
          className="bg-gray-50 rounded-xl border border-gray-200 p-4 space-y-3"
        >
          <p className="text-sm font-semibold text-gray-800">
            {editingBranch ? "تعديل الفرع" : "إضافة فرع جديد"}
          </p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                اسم الفرع <span className="text-red-500">*</span>
              </label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className={inputCls}
                placeholder="الفرع الرئيسي"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                العنوان
              </label>
              <input
                value={form.address ?? ""}
                onChange={(e) =>
                  setForm({ ...form, address: e.target.value })
                }
                className={inputCls}
                placeholder="تعز، شارع الزبيري"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الهاتف
              </label>
              <input
                value={form.phone ?? ""}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
                className={inputCls}
                placeholder="04-XXXXXXX"
                dir="ltr"
              />
            </div>
            <div className="flex items-center gap-2 pt-5">
              <input
                type="checkbox"
                id="isMain"
                checked={form.isMain ?? false}
                onChange={(e) =>
                  setForm({ ...form, isMain: e.target.checked })
                }
                className="w-4 h-4 text-clinic-teal rounded border-gray-300 focus:ring-clinic-teal"
              />
              <label htmlFor="isMain" className="text-sm text-gray-700">
                فرع رئيسي
              </label>
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={resetForm}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={createMutation.isPending || updateMutation.isPending}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {createMutation.isPending || updateMutation.isPending
                ? "جارٍ الحفظ..."
                : editingBranch
                ? "تحديث الفرع"
                : "إضافة الفرع"}
            </button>
          </div>
        </form>
      )}

      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {[
                "الاسم",
                "العنوان",
                "الهاتف",
                "رئيسي",
                "عدد المستخدمين",
                "",
              ].map((h) => (
                <th
                  key={h}
                  className="text-start px-4 py-3 text-xs font-semibold text-gray-500"
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {(branches ?? []).map((branch) => (
              <tr key={branch.id} className="hover:bg-gray-50 transition">
                <td className="px-4 py-3 font-medium text-gray-900">
                  {branch.name}
                </td>
                <td className="px-4 py-3 text-gray-600">
                  {branch.address ?? "—"}
                </td>
                <td className="px-4 py-3 text-gray-600" dir="ltr">
                  {branch.phone ?? "—"}
                </td>
                <td className="px-4 py-3">
                  <button
                    onClick={() => toggleMain(branch)}
                    className={cn(
                      "text-xs px-2.5 py-0.5 rounded-full font-medium border transition",
                      branch.isMain
                        ? "bg-green-50 text-green-700 border-green-200"
                        : "bg-gray-50 text-gray-400 border-gray-200 hover:bg-gray-100"
                    )}
                  >
                    {branch.isMain ? "رئيسي ✓" : "فرعي"}
                  </button>
                </td>
                <td className="px-4 py-3 text-gray-500">
                  {branch.userCount ?? 0}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleEdit(branch)}
                      className="p-1.5 text-gray-400 hover:text-clinic-teal rounded-lg hover:bg-teal-50 transition"
                      title="تعديل"
                    >
                      <Edit2 className="w-3.5 h-3.5" />
                    </button>
                    {confirmDeleteId === branch.id ? (
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleDelete(branch.id)}
                          className="text-xs px-2 py-1 rounded bg-red-50 text-red-600 hover:bg-red-100 transition"
                        >
                          تأكيد
                        </button>
                        <button
                          onClick={() => setConfirmDeleteId(null)}
                          className="text-xs px-2 py-1 rounded bg-gray-50 text-gray-500 hover:bg-gray-100 transition"
                        >
                          إلغاء
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setConfirmDeleteId(branch.id)}
                        className="p-1.5 text-gray-400 hover:text-red-600 rounded-lg hover:bg-red-50 transition"
                        title="حذف"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {(branches ?? []).length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-gray-400 text-sm">
                  لا توجد فروع مسجلة
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Roles Tab (Dynamic Permissions) ──────────────────────────────────────────
function RolesTab() {
  const { data: permissions, isLoading } = useRolePermissions();
  const bulkUpdateMutation = useBulkUpdateRolePermissions();
  const createPermissionMutation = useCreateRolePermission();
  const deletePermissionMutation = useDeleteRolePermission();

  const [editedPermissions, setEditedPermissions] = useState<
    Record<string, RolePermissionDto>
  >({});
  const [showAddRow, setShowAddRow] = useState(false);
  const [newPerm, setNewPerm] = useState<CreateRolePermissionRequest>({
    role: "Reception",
    resource: "patients",
    canView: true,
    canCreate: false,
    canEdit: false,
    canDelete: false,
    canExport: false,
    canApprove: false,
  });
  const [hasChanges, setHasChanges] = useState(false);

  // Sync edited permissions when data loads
  useEffect(() => {
    if (permissions) {
      const map: Record<string, RolePermissionDto> = {};
      permissions.forEach((p) => {
        map[p.id] = { ...p };
      });
      setEditedPermissions(map);
      setHasChanges(false);
    }
  }, [permissions]);

  const togglePermission = useCallback(
    (id: string, field: keyof RolePermissionDto) => {
      setEditedPermissions((prev) => {
        const perm = prev[id];
        if (!perm) return prev;
        const updated = { ...perm, [field]: !perm[field] };
        return { ...prev, [id]: updated };
      });
      setHasChanges(true);
    },
    []
  );

  const handleBulkSave = useCallback(async () => {
    if (!permissions) return;
    const changedItems: BulkUpdateRolePermissionItem[] = [];
    permissions.forEach((original) => {
      const edited = editedPermissions[original.id];
      if (!edited) return;
      if (
        edited.canView !== original.canView ||
        edited.canCreate !== original.canCreate ||
        edited.canEdit !== original.canEdit ||
        edited.canDelete !== original.canDelete ||
        edited.canExport !== original.canExport ||
        edited.canApprove !== original.canApprove
      ) {
        changedItems.push({
          id: edited.id,
          canView: edited.canView,
          canCreate: edited.canCreate,
          canEdit: edited.canEdit,
          canDelete: edited.canDelete,
          canExport: edited.canExport,
          canApprove: edited.canApprove,
        });
      }
    });
    if (changedItems.length === 0) {
      toast.info("لا توجد تغييرات للحفظ");
      return;
    }
    try {
      await bulkUpdateMutation.mutateAsync(changedItems);
      toast.success(`تم تحديث ${changedItems.length} صلاحية`);
      setHasChanges(false);
    } catch {
      toast.error("حدث خطأ أثناء الحفظ");
    }
  }, [permissions, editedPermissions, bulkUpdateMutation]);

  const handleAddPermission = useCallback(async () => {
    try {
      await createPermissionMutation.mutateAsync(newPerm);
      toast.success("تم إضافة الصلاحية");
      setShowAddRow(false);
      setNewPerm({
        role: "Reception",
        resource: "patients",
        canView: true,
        canCreate: false,
        canEdit: false,
        canDelete: false,
        canExport: false,
        canApprove: false,
      });
    } catch {
      toast.error("حدث خطأ أثناء الإضافة");
    }
  }, [newPerm, createPermissionMutation]);

  const handleDeletePermission = useCallback(
    async (id: string) => {
      try {
        await deletePermissionMutation.mutateAsync(id);
        toast.success("تم حذف الصلاحية");
      } catch {
        toast.error("حدث خطأ أثناء الحذف");
      }
    },
    [deletePermissionMutation]
  );

  const PERMISSION_COLS = [
    { key: "canView" as const, label: "عرض" },
    { key: "canCreate" as const, label: "إنشاء" },
    { key: "canEdit" as const, label: "تعديل" },
    { key: "canDelete" as const, label: "حذف" },
    { key: "canExport" as const, label: "تصدير" },
    { key: "canApprove" as const, label: "اعتماد" },
  ];

  if (isLoading) {
    return (
      <div className="animate-pulse space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-10 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">
          {(permissions ?? []).length} صلاحية
        </p>
        <div className="flex items-center gap-2">
          {hasChanges && (
            <button
              onClick={handleBulkSave}
              disabled={bulkUpdateMutation.isPending}
              className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {bulkUpdateMutation.isPending
                ? "جارٍ الحفظ..."
                : "حفظ التغييرات"}
            </button>
          )}
          <button
            onClick={() => setShowAddRow(!showAddRow)}
            className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-teal-50 transition"
          >
            {showAddRow ? <X className="w-4 h-4" /> : <Plus className="w-4 h-4" />}
            {showAddRow ? "إلغاء" : "صلاحية جديدة"}
          </button>
        </div>
      </div>

      {showAddRow && (
        <div className="bg-gray-50 rounded-xl border border-gray-200 p-4 space-y-3">
          <p className="text-sm font-semibold text-gray-800">
            إضافة صلاحية جديدة
          </p>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الدور
              </label>
              <select
                value={newPerm.role}
                onChange={(e) =>
                  setNewPerm({ ...newPerm, role: e.target.value })
                }
                className={inputCls}
              >
                {ALL_ROLES.map((r) => (
                  <option key={r} value={r}>
                    {ROLE_LABELS[r] ?? r}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                المورد
              </label>
              <input
                value={newPerm.resource}
                onChange={(e) =>
                  setNewPerm({ ...newPerm, resource: e.target.value })
                }
                className={inputCls}
                placeholder="patients"
                dir="ltr"
              />
            </div>
            {PERMISSION_COLS.map(({ key, label }) => (
              <div key={key} className="flex items-center gap-2 pt-5">
                <input
                  type="checkbox"
                  checked={newPerm[key] ?? false}
                  onChange={(e) =>
                    setNewPerm({ ...newPerm, [key]: e.target.checked })
                  }
                  className="w-4 h-4 text-clinic-teal rounded border-gray-300 focus:ring-clinic-teal"
                />
                <span className="text-xs text-gray-600">{label}</span>
              </div>
            ))}
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <button
              onClick={handleAddPermission}
              disabled={createPermissionMutation.isPending}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Plus className="w-4 h-4" />
              {createPermissionMutation.isPending
                ? "جارٍ الإضافة..."
                : "إضافة"}
            </button>
          </div>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="w-full text-xs">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <th className="text-start px-4 py-3 font-semibold text-gray-700 min-w-[120px]">
                الدور
              </th>
              <th className="text-start px-4 py-3 font-semibold text-gray-700 min-w-[120px]">
                المورد
              </th>
              {PERMISSION_COLS.map(({ key, label }) => (
                <th
                  key={key}
                  className="px-3 py-3 font-semibold text-gray-600 text-center whitespace-nowrap"
                >
                  {label}
                </th>
              ))}
              <th className="px-3 py-3 font-semibold text-gray-600 text-center w-10">
                حذف
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {(permissions ?? []).map((perm) => {
              const edited = editedPermissions[perm.id] ?? perm;
              return (
                <tr key={perm.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-2.5 text-gray-700 font-medium">
                    <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">
                      {ROLE_LABELS[perm.role] ?? perm.role}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-gray-700">
                    {beautifyResource(perm.resource)}
                  </td>
                  {PERMISSION_COLS.map(({ key }) => (
                    <td key={key} className="px-3 py-2.5 text-center">
                      <input
                        type="checkbox"
                        checked={edited[key] ?? false}
                        onChange={() => togglePermission(perm.id, key)}
                        className="w-4 h-4 text-clinic-teal rounded border-gray-300 focus:ring-clinic-teal cursor-pointer"
                      />
                    </td>
                  ))}
                  <td className="px-3 py-2.5 text-center">
                    <button
                      onClick={() => handleDeletePermission(perm.id)}
                      className="p-1 text-gray-300 hover:text-red-500 transition"
                      title="حذف الصلاحية"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </td>
                </tr>
              );
            })}
            {(permissions ?? []).length === 0 && (
              <tr>
                <td colSpan={PERMISSION_COLS.length + 3} className="px-4 py-8 text-center text-gray-400 text-sm">
                  لا توجد صلاحيات مسجلة
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Audit Tab (Embedded) ─────────────────────────────────────────────────────
function AuditTab() {
  const [page, setPage] = useState(1);
  const [action, setAction] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [search, setSearch] = useState("");
  const [appliedFilters, setAppliedFilters] = useState<{
    page: number;
    pageSize: number;
    action?: string;
    from?: string;
    to?: string;
    search?: string;
  }>({ page: 1, pageSize: 10 });

  const { data, isLoading } = useAuditLogs(appliedFilters);
  const exportMutation = useExportAuditLogs();

  const applyFilters = useCallback(() => {
    setAppliedFilters({
      page,
      pageSize: 10,
      action: action || undefined,
      from: from || undefined,
      to: to || undefined,
      search: search || undefined,
    });
  }, [page, action, from, to, search]);

  const resetFilters = useCallback(() => {
    setPage(1);
    setAction("");
    setFrom("");
    setTo("");
    setSearch("");
    setAppliedFilters({ page: 1, pageSize: 10 });
  }, []);

  const handlePageChange = useCallback((newPage: number) => {
    setPage(newPage);
    setAppliedFilters((prev) => ({ ...prev, page: newPage }));
  }, []);

  const handleExport = useCallback(async () => {
    try {
      const result = await exportMutation.mutateAsync({
        action: appliedFilters.action,
        from: appliedFilters.from,
        to: appliedFilters.to,
        search: appliedFilters.search,
      });
      const blob = new Blob([result as unknown as BlobPart], {
        type: "text/csv;charset=utf-8;",
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
      toast.success("تم تصدير السجل");
    } catch {
      toast.error("حدث خطأ أثناء التصدير");
    }
  }, [exportMutation, appliedFilters]);

  const logs = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="bg-gray-50 rounded-lg border border-gray-200 p-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              الإجراء
            </label>
            <select
              value={action}
              onChange={(e) => setAction(e.target.value)}
              className={inputCls}
            >
              {ACTION_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              من تاريخ
            </label>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className={inputCls}
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              إلى تاريخ
            </label>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className={inputCls}
              dir="ltr"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              بحث
            </label>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className={inputCls}
              placeholder="بحث..."
            />
          </div>
        </div>
        <div className="flex items-center gap-2 mt-2">
          <button
            onClick={applyFilters}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Search className="w-3.5 h-3.5" />
            تطبيق
          </button>
          <button
            onClick={resetFilters}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white transition"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            إعادة تعيين
          </button>
          <button
            onClick={handleExport}
            disabled={exportMutation.isPending}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white transition mr-auto"
          >
            <Download className="w-3.5 h-3.5" />
            تصدير
          </button>
        </div>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-10 bg-gray-100 rounded-lg" />
          ))}
        </div>
      ) : logs.length === 0 ? (
        <div className="text-center py-10 text-gray-400 text-sm">
          لا توجد سجلات
        </div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {[
                    "التاريخ",
                    "المستخدم",
                    "الإجراء",
                    "المورد",
                    "عنوان IP",
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
                {logs.map((log) => (
                  <tr key={log.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                      {formatAuditDate(log.timestamp)}
                    </td>
                    <td className="px-4 py-2.5 font-medium text-gray-900 text-xs">
                      {log.userName ?? log.userId}
                    </td>
                    <td className="px-4 py-2.5">
                      <span
                        className={cn(
                          "inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border",
                          ACTION_COLORS[log.action] ??
                            "bg-gray-100 text-gray-600 border-gray-200"
                        )}
                      >
                        {ACTION_LABELS[log.action] ?? log.action}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-gray-700">
                      {beautifyResource(log.resource)}
                    </td>
                    <td
                      className="px-4 py-2.5 font-mono text-xs text-gray-400"
                      dir="ltr"
                    >
                      {log.ipAddress ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between px-2">
              <p className="text-xs text-gray-400">
                صفحة {page} من {totalPages}
              </p>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => handlePageChange(page - 1)}
                  disabled={page <= 1}
                  className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed transition"
                >
                  <ChevronRight className="w-3 h-3" />
                  السابقة
                </button>
                <button
                  onClick={() => handlePageChange(page + 1)}
                  disabled={page >= totalPages}
                  className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed transition"
                >
                  التالية
                  <ChevronLeft className="w-3 h-3" />
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ─── Change Password Tab ──────────────────────────────────────────────────────
function ChangePasswordTab() {
  const [form, setForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const changePasswordMutation = useChangePassword();

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError("");
      setSuccess(false);

      if (form.newPassword.length < 8) {
        setError("كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل");
        return;
      }
      if (form.newPassword === form.currentPassword) {
        setError("كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية");
        return;
      }
      if (form.newPassword !== form.confirmPassword) {
        setError("تأكيد كلمة المرور غير متطابق");
        return;
      }

      try {
        await changePasswordMutation.mutateAsync({
          currentPassword: form.currentPassword,
          newPassword: form.newPassword,
        });
        setSuccess(true);
        setForm({
          currentPassword: "",
          newPassword: "",
          confirmPassword: "",
        });
        setTimeout(() => setSuccess(false), 5000);
      } catch (err: unknown) {
        const msg = (err as { response?: { data?: { message?: string } } })
          ?.response?.data?.message;
        setError(msg ?? "حدث خطأ أثناء تغيير كلمة المرور");
      }
    },
    [form, changePasswordMutation]
  );

  return (
    <div className="max-w-md space-y-4">
      {success && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm">
          <CheckCircle className="w-5 h-5" />
          <span>تم تغيير كلمة المرور بنجاح</span>
        </div>
      )}

      {error && (
        <div className="px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-red-600 text-sm">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            كلمة المرور الحالية
          </label>
          <div className="relative">
            <input
              value={form.currentPassword}
              onChange={(e) =>
                setForm({ ...form, currentPassword: e.target.value })
              }
              type={showCurrent ? "text" : "password"}
              className={cn(inputCls, "pl-10")}
              placeholder="أدخل كلمة المرور الحالية"
              required
            />
            <button
              type="button"
              onClick={() => setShowCurrent(!showCurrent)}
              className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition"
            >
              {showCurrent ? (
                <EyeOff className="w-4 h-4" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
            </button>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            كلمة المرور الجديدة
          </label>
          <div className="relative">
            <input
              value={form.newPassword}
              onChange={(e) =>
                setForm({ ...form, newPassword: e.target.value })
              }
              type={showNew ? "text" : "password"}
              className={cn(inputCls, "pl-10")}
              placeholder="8 أحرف على الأقل"
              required
            />
            <button
              type="button"
              onClick={() => setShowNew(!showNew)}
              className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition"
            >
              {showNew ? (
                <EyeOff className="w-4 h-4" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
            </button>
          </div>
          {form.newPassword.length > 0 && form.newPassword.length < 8 && (
            <p className="text-xs text-red-500 mt-1">
              يجب أن تكون 8 أحرف على الأقل
            </p>
          )}
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            تأكيد كلمة المرور الجديدة
          </label>
          <div className="relative">
            <input
              value={form.confirmPassword}
              onChange={(e) =>
                setForm({ ...form, confirmPassword: e.target.value })
              }
              type={showConfirm ? "text" : "password"}
              className={cn(inputCls, "pl-10")}
              placeholder="أعد إدخال كلمة المرور الجديدة"
              required
            />
            <button
              type="button"
              onClick={() => setShowConfirm(!showConfirm)}
              className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition"
            >
              {showConfirm ? (
                <EyeOff className="w-4 h-4" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
            </button>
          </div>
          {form.confirmPassword.length > 0 &&
            form.newPassword !== form.confirmPassword && (
              <p className="text-xs text-red-500 mt-1">
                كلمات المرور غير متطابقة
              </p>
            )}
        </div>

        <button
          type="submit"
          disabled={changePasswordMutation.isPending}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Lock className="w-4 h-4" />
          {changePasswordMutation.isPending
            ? "جارٍ التغيير..."
            : "تغيير كلمة المرور"}
        </button>
      </form>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────
export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("clinic");

  return (
    <div className="space-y-5 max-w-5xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">الإعدادات</h1>
        <p className="text-sm text-gray-500 mt-0.5">
          إدارة إعدادات المركز والمستخدمين
        </p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "clinic" && <ClinicTab />}
          {activeTab === "users" && <UsersTab />}
          {activeTab === "roles" && <RolesTab />}
          {activeTab === "branches" && <BranchesTab />}
          {activeTab === "audit" && <AuditTab />}
          {activeTab === "password" && <ChangePasswordTab />}
        </div>
      </div>
    </div>
  );
}
