"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Settings, Users, Shield, Save, Plus, X, UserCheck, UserX,
  FileSearch, Globe, Stethoscope, DoorOpen, Search, Filter,
  Trash2, RotateCcw, KeyRound, Copy, AlertTriangle,
  UserCog, Loader2, ShieldAlert, CheckCircle2, XCircle,
  Mail, MailWarning, Clock, Send,
} from "lucide-react";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useAuthStore } from "@/stores/authStore";
import api from "@/lib/api";
import {
  useUsers,
  usePatientPortalAccounts,
  usePasswordResetRequests,
  usePermissions,
  useCreateUser,
  useEditUser,
  useToggleUserStatus,
  useDeleteUser,
  useRestoreUser,
  useResetUserPassword,
  useApproveResetRequest,
  useRejectResetRequest,
  useUpdateRolePermissions,
  useEmailStats,
  useEmailHistory,
  type UserDetailDto,
  type PermissionDto,
  type CreateUserRequest,
  type EditUserRequest,
} from "@/hooks/useUsers";
import type { ImpersonateResponse } from "@/types/auth";

type Tab = "clinic" | "users" | "roles" | "email";

const TABS: { key: Tab; label: string; icon: typeof Settings }[] = [
  { key: "clinic", label: "بيانات المركز", icon: Settings },
  { key: "users",  label: "المستخدمون",   icon: Users },
  { key: "roles",  label: "الأدوار",      icon: Shield },
  { key: "email",  label: "البريد",        icon: Mail },
];

interface ClinicSettings {
  "clinic.name"?: string;
  "clinic.location"?: string;
  "clinic.phones"?: string;
  "clinic.currency"?: string;
  [key: string]: string | undefined;
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

const ALL_ROLES = ["Admin","Orthodontist","GeneralDentist","OralSurgeon","Reception","Accountant","Assistant","BranchManager"] as const;

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

function getApiErrorMessage(error: unknown, fallback: string) {
  const response = error as { response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
  const message = response.response?.data?.message;
  if (message) return message;

  const errors = response.response?.data?.errors;
  if (errors) {
    const firstError = Object.values(errors).flat()[0];
    if (firstError) return firstError;
  }

  return fallback;
}

// ─── Clinic Info Tab ──────────────────────────────────────────────────────────
function ClinicTab() {
  const [settings, setSettings] = useState<ClinicSettings>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api.get<ClinicSettings>("/api/settings")
      .then((r) => setSettings(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      const clinicFields = ["clinic.name", "clinic.location", "clinic.phones"] as const;
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
    return <div className="animate-pulse space-y-3">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  return (
    <div className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">اسم المركز</label>
        <input
          value={settings["clinic.name"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.name": e.target.value })}
          className={inputCls}
          placeholder="مركز د. عقلان الكامل لطب وتقويم الأسنان"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">العنوان</label>
        <input
          value={settings["clinic.location"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.location": e.target.value })}
          className={inputCls}
          placeholder="تعز، اليمن"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">أرقام الهاتف</label>
        <input
          value={settings["clinic.phones"] ?? ""}
          onChange={(e) => setSettings({ ...settings, "clinic.phones": e.target.value })}
          className={inputCls}
          placeholder="04-253028، 770XXXXXX"
          dir="ltr"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">بادئة رقم المريض</label>
        <input
          value={settings["patient.number_prefix"] ?? "GM"}
          onChange={(e) => setSettings({ ...settings, "patient.number_prefix": e.target.value })}
          className={inputCls}
          placeholder="GM"
          dir="ltr"
        />
      </div>

      <div className="flex items-center gap-3 pt-2">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
        </button>
        {saved && <span className="text-sm text-green-600 font-medium">✓ تم الحفظ</span>}
      </div>
    </div>
  );
}

// ─── Users Tab (Complete Rewrite) ────────────────────────────────────────────
function UsersTab() {
  const { data: users, isLoading: usersLoading } = useUsers();
  const { data: patientPortalAccounts, isLoading: portalAccountsLoading } = usePatientPortalAccounts();
  const { data: resetRequests } = usePasswordResetRequests();
  const [accountView, setAccountView] = useState<"staff" | "patients">("staff");

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [roleFilter, setRoleFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive" | "deleted">("all");

  // Dialogs
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDetailDto | null>(null);
  const [showResetPasswordDialog, setShowResetPasswordDialog] = useState(false);
  const [resetPasswordUserId, setResetPasswordUserId] = useState<string>("");
  const [tempPassword, setTempPassword] = useState<string>("");
  const [showImpersonateDialog, setShowImpersonateDialog] = useState(false);
  const [impersonateUserId, setImpersonateUserId] = useState<string>("");
  const [impersonateReason, setImpersonateReason] = useState("");
  const [confirmDialog, setConfirmDialog] = useState<{
    open: boolean;
    title: string;
    message: string;
    variant?: "danger" | "warning";
    onConfirm: () => void;
  }>({ open: false, title: "", message: "", onConfirm: () => {} });

  // Toast state
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const showToast = useCallback((message: string, type: "success" | "error" = "success") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  }, []);

  // Mutations
  const createUser = useCreateUser();
  const editUser = useEditUser();
  const toggleStatus = useToggleUserStatus();
  const deleteUser = useDeleteUser();
  const restoreUser = useRestoreUser();
  const resetPassword = useResetUserPassword();
  const approveRequest = useApproveResetRequest();
  const rejectRequest = useRejectResetRequest();
  const authStore = useAuthStore();

  // Filtered users
  const filteredUsers = (users ?? []).filter((u) => {
    const matchesSearch = !searchQuery ||
      u.username.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (u.email?.toLowerCase() ?? "").includes(searchQuery.toLowerCase()) ||
      (u.doctorName?.includes(searchQuery) ?? false);

    const matchesRole = roleFilter === "all" || u.role === roleFilter;

    const isDeleted = !!u.deletedAt;
    let matchesStatus = true;
    if (statusFilter === "active") matchesStatus = u.isActive && !isDeleted;
    else if (statusFilter === "inactive") matchesStatus = !u.isActive && !isDeleted;
    else if (statusFilter === "deleted") matchesStatus = isDeleted;

    return matchesSearch && matchesRole && matchesStatus;
  });

  // Handlers
  const handleToggleStatus = (user: UserDetailDto) => {
    setConfirmDialog({
      open: true,
      title: user.isActive ? "تعطيل المستخدم" : "تفعيل المستخدم",
      message: user.isActive
        ? `هل أنت متأكد من تعطيل المستخدم "${user.username}"؟`
        : `هل أنت متأكد من تفعيل المستخدم "${user.username}"؟`,
      variant: "warning",
      onConfirm: () => {
        toggleStatus.mutate(user.id, {
          onSuccess: () => showToast(user.isActive ? "تم تعطيل المستخدم" : "تم تفعيل المستخدم"),
          onError: () => showToast("حدث خطأ أثناء تغيير الحالة", "error"),
        });
        setConfirmDialog((prev) => ({ ...prev, open: false }));
      },
    });
  };

  const handleDelete = (user: UserDetailDto) => {
    setConfirmDialog({
      open: true,
      title: "حذف المستخدم",
      message: "هل أنت متأكد من حذف هذا المستخدم؟",
      variant: "danger",
      onConfirm: () => {
        deleteUser.mutate(user.id, {
          onSuccess: () => showToast("تم حذف المستخدم"),
          onError: () => showToast("حدث خطأ أثناء الحذف", "error"),
        });
        setConfirmDialog((prev) => ({ ...prev, open: false }));
      },
    });
  };

  const handleRestore = (user: UserDetailDto) => {
    restoreUser.mutate(user.id, {
      onSuccess: () => showToast("تم استعادة المستخدم"),
      onError: () => showToast("حدث خطأ أثناء الاستعادة", "error"),
    });
  };

  const handleResetPassword = (userId: string) => {
    setResetPasswordUserId(userId);
    setTempPassword("");
    setShowResetPasswordDialog(true);
  };

  const confirmResetPassword = () => {
    resetPassword.mutate(resetPasswordUserId, {
      onSuccess: (data) => {
        setTempPassword(data.temporaryPassword);
      },
      onError: () => {
        showToast("حدث خطأ أثناء إعادة تعيين كلمة المرور", "error");
        setShowResetPasswordDialog(false);
      },
    });
  };

  const handleImpersonate = (userId: string) => {
    setImpersonateUserId(userId);
    setImpersonateReason("");
    setShowImpersonateDialog(true);
  };

  const confirmImpersonate = async () => {
    if (!impersonateReason.trim()) return;
    try {
      const { data } = await api.post<ImpersonateResponse>(`/api/auth/impersonate/${impersonateUserId}`, {
        reason: impersonateReason.trim(),
      });
      authStore.startImpersonation(data.accessToken, data.user);
      setShowImpersonateDialog(false);
      showToast("تم الدخول كحساب آخر بنجاح");
    } catch {
      showToast("حدث خطأ أثناء الدخول كحساب آخر", "error");
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      showToast("تم نسخ كلمة المرور");
    });
  };

  const handleApproveRequest = (requestId: string) => {
    approveRequest.mutate(requestId, {
      onSuccess: (data) => {
        showToast(`تم الموافقة — كلمة المرور المؤقتة: ${data.temporaryPassword}`);
      },
      onError: () => showToast("حدث خطأ", "error"),
    });
  };

  const handleRejectRequest = (requestId: string) => {
    setConfirmDialog({
      open: true,
      title: "رفض طلب إعادة التعيين",
      message: "هل أنت متأكد من رفض هذا الطلب؟",
      variant: "danger",
      onConfirm: () => {
        rejectRequest.mutate({ id: requestId }, {
          onSuccess: () => showToast("تم رفض الطلب"),
          onError: () => showToast("حدث خطأ", "error"),
        });
        setConfirmDialog((prev) => ({ ...prev, open: false }));
      },
    });
  };

  if (usersLoading && accountView === "staff") {
    return <div className="animate-pulse space-y-2">{Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}</div>;
  }

  return (
    <div className="space-y-5">
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

      {/* Account type tabs */}
      <div className="inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1">
        <button
          type="button"
          onClick={() => setAccountView("staff")}
          className={cn(
            "px-3 py-1.5 text-sm font-medium rounded-md transition",
            accountView === "staff" ? "bg-white text-clinic-blue shadow-sm" : "text-gray-600 hover:text-gray-900"
          )}
        >
          حسابات الطاقم
        </button>
        <button
          type="button"
          onClick={() => setAccountView("patients")}
          className={cn(
            "px-3 py-1.5 text-sm font-medium rounded-md transition",
            accountView === "patients" ? "bg-white text-clinic-blue shadow-sm" : "text-gray-600 hover:text-gray-900"
          )}
        >
          حسابات بوابة المرضى
        </button>
      </div>

      {accountView === "patients" ? (
        <div className="space-y-4">
          {portalAccountsLoading ? (
            <div className="animate-pulse space-y-2">
              {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between">
                <p className="text-sm text-gray-500">{(patientPortalAccounts ?? []).length} حساب مريض</p>
                <p className="text-xs text-gray-400">تدار كلمات المرور من ملف المريض فقط</p>
              </div>
              <div className="overflow-x-auto rounded-lg border border-gray-200">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-200">
                    <tr>
                      {["المريض", "رقم الملف", "اسم الدخول", "الهاتف", "الحالة", "آخر دخول", "ملف المريض"].map((h) => (
                        <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {(patientPortalAccounts ?? []).length === 0 ? (
                      <tr>
                        <td colSpan={7} className="px-4 py-8 text-center text-gray-400">
                          لا توجد حسابات بوابة مرضى
                        </td>
                      </tr>
                    ) : (
                      (patientPortalAccounts ?? []).map((account) => (
                        <tr key={account.patientId} className="hover:bg-gray-50 transition">
                          <td className="px-4 py-3 font-medium text-gray-900">{account.patientName || "—"}</td>
                          <td className="px-4 py-3 font-mono text-gray-700" dir="ltr">{account.patientNumber}</td>
                          <td className="px-4 py-3 font-mono text-gray-700" dir="ltr">{account.username}</td>
                          <td className="px-4 py-3 text-gray-500" dir="ltr">{account.phone || "—"}</td>
                          <td className="px-4 py-3">
                            <span className={cn(
                              "text-xs px-2 py-0.5 rounded-full font-medium",
                              account.accountActive ? "bg-green-50 text-green-700" : "bg-gray-100 text-gray-500"
                            )}>
                              {account.accountActive ? "نشط" : "معطل"}
                            </span>
                            {account.mustChangePassword && (
                              <span className="me-1 text-xs bg-amber-50 text-amber-700 px-2 py-0.5 rounded-full font-medium">
                                تغيير كلمة المرور مطلوب
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-gray-500 text-xs">
                            {account.lastLogin
                              ? new Date(account.lastLogin).toLocaleDateString("ar-YE", { year: "numeric", month: "short", day: "numeric" })
                              : "—"}
                          </td>
                          <td className="px-4 py-3">
                            <Link
                              href={`/patients/${account.patientId}`}
                              className="text-clinic-blue hover:underline text-xs font-medium"
                            >
                              فتح الملف
                            </Link>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      ) : (
      <>
      {/* Header */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{(users ?? []).length} مستخدم</p>
        <button
          onClick={() => setShowCreateDialog(true)}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          مستخدم جديد
        </button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="بحث باسم المستخدم أو البريد..."
            className="w-full pr-9 pl-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          />
        </div>
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-gray-400" />
          <select
            value={roleFilter}
            onChange={(e) => setRoleFilter(e.target.value)}
            className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          >
            <option value="all">جميع الأدوار</option>
            {ALL_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
          </select>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
            className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          >
            <option value="all">جميع الحالات</option>
            <option value="active">نشط</option>
            <option value="inactive">معطّل</option>
            <option value="deleted">محذوف</option>
          </select>
        </div>
      </div>

      {/* Users Table */}
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {["اسم المستخدم", "البريد", "الاسم الكامل", "الدور", "الحالة", "إجراءات"].map((h) => (
                <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {filteredUsers.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                  لا يوجد مستخدمون مطابقون للبحث
                </td>
              </tr>
            ) : (
              filteredUsers.map((u) => {
                const isDeleted = !!u.deletedAt;
                return (
                  <tr key={u.id} className={cn("hover:bg-gray-50 transition", isDeleted && "opacity-50")}>
                    <td className="px-4 py-3 font-mono font-medium text-gray-900">{u.username}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs" dir="ltr">{u.email || "—"}</td>
                    <td className="px-4 py-3 text-gray-700">{u.doctorName || "—"}</td>
                    <td className="px-4 py-3">
                      <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">
                        {ROLE_LABELS[u.role] ?? u.role}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      {isDeleted ? (
                        <span className="text-xs bg-red-50 text-red-600 px-2 py-0.5 rounded-full font-medium">محذوف</span>
                      ) : u.isActive ? (
                        <span className="text-xs bg-green-50 text-green-700 px-2 py-0.5 rounded-full font-medium">نشط</span>
                      ) : (
                        <span className="text-xs bg-gray-100 text-gray-500 px-2 py-0.5 rounded-full font-medium">معطّل</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        {isDeleted ? (
                          <button
                            onClick={() => handleRestore(u)}
                            title="استعادة المستخدم"
                            className="p-1.5 rounded-lg text-green-600 hover:bg-green-50 transition"
                          >
                            <RotateCcw className="w-4 h-4" />
                          </button>
                        ) : (
                          <>
                            <button
                              onClick={() => setEditingUser(u)}
                              title="تعديل المستخدم"
                              className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
                            >
                              <UserCog className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleToggleStatus(u)}
                              title={u.isActive ? "تعطيل" : "تفعيل"}
                              className="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition"
                            >
                              {u.isActive ? <UserX className="w-4 h-4" /> : <UserCheck className="w-4 h-4 text-green-600" />}
                            </button>
                            <button
                              onClick={() => handleResetPassword(u.id)}
                              title="إعادة تعيين كلمة المرور"
                              className="p-1.5 rounded-lg text-gray-400 hover:text-orange-600 hover:bg-orange-50 transition"
                            >
                              <KeyRound className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleImpersonate(u.id)}
                              title="الدخول كحساب هذا المستخدم"
                              className="p-1.5 rounded-lg text-gray-400 hover:text-purple-600 hover:bg-purple-50 transition"
                            >
                              <ShieldAlert className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleDelete(u)}
                              title="حذف المستخدم"
                              className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Password Reset Requests Section */}
      {resetRequests && resetRequests.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2">
            <KeyRound className="w-4 h-4 text-clinic-orange" />
            طلبات إعادة تعيين كلمة المرور
          </h3>
          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المستخدم", "السبب", "التاريخ", "إجراءات"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {resetRequests.map((req) => (
                  <tr key={req.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-mono font-medium text-gray-900">{req.username}</td>
                    <td className="px-4 py-3 text-gray-600">{req.reason}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {new Date(req.createdAt).toLocaleDateString("ar-YE", { year: "numeric", month: "short", day: "numeric" })}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleApproveRequest(req.id)}
                          className="p-1.5 rounded-lg text-green-600 hover:bg-green-50 transition"
                          title="موافقة"
                        >
                          <CheckCircle2 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleRejectRequest(req.id)}
                          className="p-1.5 rounded-lg text-red-500 hover:bg-red-50 transition"
                          title="رفض"
                        >
                          <XCircle className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Create User Dialog */}
      {showCreateDialog && (
        <CreateUserDialog
          onClose={() => setShowCreateDialog(false)}
          onSubmit={async (data) => {
            createUser.mutate(data, {
              onSuccess: () => {
                setShowCreateDialog(false);
                showToast("تم إنشاء المستخدم بنجاح");
              },
              onError: (err) => showToast(getApiErrorMessage(err, "حدث خطأ أثناء إنشاء المستخدم"), "error"),
            });
          }}
          saving={createUser.isPending}
        />
      )}

      {/* Edit User Dialog */}
      {editingUser && (
        <EditUserDialog
          user={editingUser}
          onClose={() => setEditingUser(null)}
          onSubmit={async (data) => {
            editUser.mutate({ id: editingUser.id, ...data }, {
              onSuccess: () => {
                setEditingUser(null);
                showToast("تم تحديث المستخدم بنجاح");
              },
              onError: (err) => showToast(getApiErrorMessage(err, "حدث خطأ أثناء التحديث"), "error"),
            });
          }}
          saving={editUser.isPending}
        />
      )}

      {/* Reset Password Dialog */}
      {showResetPasswordDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => !tempPassword && setShowResetPasswordDialog(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center bg-orange-100">
                <KeyRound className="w-5 h-5 text-orange-600" />
              </div>
              <div className="flex-1">
                <h3 className="text-base font-bold text-gray-900">إعادة تعيين كلمة المرور</h3>
                {tempPassword ? (
                  <div className="mt-3 space-y-3">
                    <div
                      className="text-xs px-3 py-2 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start gap-2"
                    >
                      <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                      <span>لن تظهر كلمة المرور مرة أخرى. تأكد من نسخها الآن.</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <div className="flex-1 px-3 py-2.5 bg-gray-100 rounded-lg font-mono text-sm text-gray-900 text-left" dir="ltr">
                        {tempPassword}
                      </div>
                      <button
                        onClick={() => copyToClipboard(tempPassword)}
                        className="p-2.5 rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
                        title="نسخ"
                      >
                        <Copy className="w-4 h-4" />
                      </button>
                    </div>
                    <button
                      onClick={() => setShowResetPasswordDialog(false)}
                      className="w-full py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
                    >
                      إغلاق
                    </button>
                  </div>
                ) : (
                  <>
                    <p className="text-sm text-gray-600 mt-1">سيتم إنشاء كلمة مرور مؤقتة لهذا المستخدم.</p>
                    <div className="flex items-center justify-end gap-3 pt-4">
                      <button
                        onClick={() => setShowResetPasswordDialog(false)}
                        className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
                      >
                        إلغاء
                      </button>
                      <button
                        onClick={confirmResetPassword}
                        disabled={resetPassword.isPending}
                        className="px-4 py-2 text-sm font-medium text-white bg-orange-600 rounded-lg hover:bg-orange-700 disabled:opacity-60 transition flex items-center gap-2"
                      >
                        {resetPassword.isPending && <Loader2 className="w-4 h-4 animate-spin" />}
                        إعادة تعيين
                      </button>
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Impersonate Dialog */}
      {showImpersonateDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setShowImpersonateDialog(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center bg-purple-100">
                <ShieldAlert className="w-5 h-5 text-purple-600" />
              </div>
              <div className="flex-1">
                <h3 className="text-base font-bold text-gray-900">الدخول كحساب آخر</h3>
                <p className="text-sm text-gray-600 mt-1">سيتم تسجيل خروجك المؤقت والدخول بحساب هذا المستخدم.</p>
                <div
                  className="text-xs px-3 py-2 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 mt-2 flex items-start gap-2"
                >
                  <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                  <span>هذا الإجراء حساس ويتم تسجيله في سجل التدقيق.</span>
                </div>
                <div className="mt-3">
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">سبب الدخول <span className="text-red-500">*</span></label>
                  <textarea
                    value={impersonateReason}
                    onChange={(e) => setImpersonateReason(e.target.value)}
                    placeholder="أدخل سبب الدخول كحساب هذا المستخدم..."
                    rows={3}
                    className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none"
                  />
                </div>
                <div className="flex items-center justify-end gap-3 pt-2">
                  <button
                    onClick={() => setShowImpersonateDialog(false)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
                  >
                    إلغاء
                  </button>
                  <button
                    onClick={confirmImpersonate}
                    disabled={!impersonateReason.trim()}
                    className="px-4 py-2 text-sm font-medium text-white bg-purple-600 rounded-lg hover:bg-purple-700 disabled:opacity-60 transition"
                  >
                    تأكيد الدخول
                  </button>
                </div>
              </div>
            </div>
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
      </>
      )}
    </div>
  );
}

// ─── Create User Dialog ──────────────────────────────────────────────────────
function CreateUserDialog({
  onClose,
  onSubmit,
  saving,
}: {
  onClose: () => void;
  onSubmit: (data: CreateUserRequest) => void;
  saving: boolean;
}) {
  const [form, setForm] = useState<CreateUserRequest>({
    username: "",
    password: "",
    role: "Reception",
    email: "",
    doctorName: "",
    doctorSpecialty: "",
    doctorColor: "#3d7ab5",
  });
  const [error, setError] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.username || !form.password) {
      setError("اسم المستخدم وكلمة المرور مطلوبان");
      return;
    }
    setError("");
    onSubmit({
      ...form,
      email: form.email || undefined,
      doctorName: form.doctorName || undefined,
      doctorSpecialty: form.doctorSpecialty || undefined,
      doctorColor: form.doctorName ? form.doctorColor : undefined,
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
            <Plus className="w-5 h-5 text-clinic-blue" />
            إضافة مستخدم جديد
          </h3>
          <button onClick={onClose} className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition">
            <X className="w-5 h-5" />
          </button>
        </div>

        {error && <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{error}</p>}

        <form onSubmit={handleSubmit} className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم المستخدم <span className="text-red-500">*</span></label>
              <input
                value={form.username}
                onChange={(e) => setForm({ ...form, username: e.target.value })}
                className={inputCls}
                placeholder="username"
                dir="ltr"
                autoComplete="off"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">كلمة المرور <span className="text-red-500">*</span></label>
              <input
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                type="password"
                className={inputCls}
                autoComplete="new-password"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className={inputCls}>
                {ALL_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">البريد الإلكتروني</label>
              <input
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                type="email"
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم الطبيب (اختياري)</label>
              <input
                value={form.doctorName}
                onChange={(e) => setForm({ ...form, doctorName: e.target.value })}
                className={inputCls}
                placeholder="د. محمد أحمد"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">تخصص الطبيب</label>
              <input
                value={form.doctorSpecialty ?? ""}
                onChange={(e) => setForm({ ...form, doctorSpecialty: e.target.value })}
                className={inputCls}
                placeholder="أخصائي تقويم"
              />
            </div>
            {form.doctorName && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">لون الطبيب</label>
                <input
                  value={form.doctorColor}
                  onChange={(e) => setForm({ ...form, doctorColor: e.target.value })}
                  type="color"
                  className="h-9 w-full rounded-lg border border-gray-300 cursor-pointer"
                />
              </div>
            )}
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              {saving ? "جارٍ الحفظ..." : "إضافة المستخدم"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Edit User Dialog ────────────────────────────────────────────────────────
function EditUserDialog({
  user,
  onClose,
  onSubmit,
  saving,
}: {
  user: UserDetailDto;
  onClose: () => void;
  onSubmit: (data: EditUserRequest) => void;
  saving: boolean;
}) {
  const [form, setForm] = useState<EditUserRequest>({
    username: user.username,
    email: user.email ?? "",
    role: user.role,
    doctorName: user.doctorName ?? "",
    doctorSpecialty: user.doctorSpecialty ?? "",
    doctorColor: user.doctorColor ?? "#3d7ab5",
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      ...form,
      email: form.email || undefined,
      doctorName: form.doctorName || undefined,
      doctorSpecialty: form.doctorSpecialty || undefined,
      doctorColor: form.doctorName ? form.doctorColor : undefined,
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
            <UserCog className="w-5 h-5 text-clinic-blue" />
            تعديل المستخدم: {user.username}
          </h3>
          <button onClick={onClose} className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم المستخدم</label>
              <input
                value={form.username}
                onChange={(e) => setForm({ ...form, username: e.target.value })}
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">البريد الإلكتروني</label>
              <input
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                type="email"
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
              <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className={inputCls}>
                {ALL_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم الطبيب (اختياري)</label>
              <input
                value={form.doctorName}
                onChange={(e) => setForm({ ...form, doctorName: e.target.value })}
                className={inputCls}
                placeholder="د. محمد أحمد"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">تخصص الطبيب</label>
              <input
                value={form.doctorSpecialty ?? ""}
                onChange={(e) => setForm({ ...form, doctorSpecialty: e.target.value })}
                className={inputCls}
                placeholder="أخصائي تقويم"
              />
            </div>
            {form.doctorName && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">لون الطبيب</label>
                <input
                  value={form.doctorColor}
                  onChange={(e) => setForm({ ...form, doctorColor: e.target.value })}
                  type="color"
                  className="h-9 w-full rounded-lg border border-gray-300 cursor-pointer"
                />
              </div>
            )}
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              {saving ? "جارٍ الحفظ..." : "حفظ التعديلات"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Roles Tab (Complete Rewrite) ────────────────────────────────────────────
function RolesTab() {
  const { data: permissions, isLoading: permissionsLoading } = usePermissions();
  const [selectedRole, setSelectedRole] = useState<string>("Admin");
  const [rolePermissionsMap, setRolePermissionsMap] = useState<Record<string, Set<string>>>({});
  const [loadingRoles, setLoadingRoles] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const [showAdminWarning, setShowAdminWarning] = useState(false);

  const updateRolePermissions = useUpdateRolePermissions();

  // Load permissions for all roles
  useEffect(() => {
    const loadRolePermissions = async () => {
      const roles = ALL_ROLES;
      for (const role of roles) {
        setLoadingRoles((prev) => new Set(prev).add(role));
        try {
          const { data } = await api.get<{ role: string; permissions: string[] }>(`/api/roles/${role}/permissions`);
          setRolePermissionsMap((prev) => ({ ...prev, [role]: new Set(data.permissions) }));
        } catch {
          setRolePermissionsMap((prev) => ({ ...prev, [role]: new Set() }));
        } finally {
          setLoadingRoles((prev) => {
            const next = new Set(prev);
            next.delete(role);
            return next;
          });
        }
      }
    };
    loadRolePermissions();
  }, []);

  const togglePermission = (role: string, permissionKey: string) => {
    setRolePermissionsMap((prev) => {
      const current = new Set(prev[role] ?? []);
      if (current.has(permissionKey)) {
        current.delete(permissionKey);
      } else {
        current.add(permissionKey);
      }
      return { ...prev, [role]: current };
    });
  };

  const handleSave = async () => {
    if (selectedRole === "Admin") {
      setShowAdminWarning(true);
      return;
    }
    await savePermissions(selectedRole);
  };

  const savePermissions = async (role: string) => {
    const permissions = Array.from(rolePermissionsMap[role] ?? []);
    setSaving(true);
    updateRolePermissions.mutate(
      { role, permissions },
      {
        onSuccess: () => {
          setToast({ message: "تم حفظ الصلاحيات بنجاح", type: "success" });
          setTimeout(() => setToast(null), 3000);
        },
        onError: (err) => {
          setToast({ message: getApiErrorMessage(err, "حدث خطأ أثناء الحفظ"), type: "error" });
          setTimeout(() => setToast(null), 3000);
        },
        onSettled: () => setSaving(false),
      }
    );
  };

  if (permissionsLoading || loadingRoles.size > 0) {
    return <div className="animate-pulse space-y-3">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  // Group permissions by resource
  const permList = permissions ?? [];
  const groupedPermissions = permList.reduce<Record<string, PermissionDto[]>>((acc, p) => {
    if (!acc[p.resource]) acc[p.resource] = [];
    acc[p.resource].push(p);
    return acc;
  }, {});

  const RESOURCE_LABELS: Record<string, string> = {
    patients: "المرضى",
    ortho: "التقويم",
    general_dentistry: "طب الأسنان العام",
    surgery: "الجراحة",
    appointments: "المواعيد",
    finance: "المدفوعات",
    reports: "التقارير",
    users: "المستخدمون",
    settings: "الإعدادات",
    ai: "الذكاء الاصطناعي",
    user_management: "إدارة المستخدمين",
    password_reset_requests: "طلبات إعادة تعيين كلمة المرور",
    impersonation: "الانتحال",
    daily_operations: "التشغيل اليومي",
    booking_requests: "طلبات الحجز",
    clinic_queue: "الطابور",
    clinic_display: "شاشة النداء",
    patient_journey: "رحلة المرضى",
    visits: "الزيارات",
    checkout: "جاهز للدفع",
    invoices: "الفواتير",
    rooms: "الغرف / الكراسي",
    // Legacy PascalCase keys (fallback)
    Patients: "المرضى",
    Appointments: "المواعيد",
    Ortho: "التقويم",
    Finance: "المالية",
    Reports: "التقارير",
    Settings: "الإعدادات",
    Users: "المستخدمون",
    Doctors: "الأطباء",
  };

  const ACTION_LABELS: Record<string, string> = {
    view: "عرض",
    create: "إنشاء",
    edit: "تعديل",
    delete: "حذف",
    export: "تصدير",
    approve: "اعتماد",
    // Legacy PascalCase keys (fallback)
    View: "عرض",
    Create: "إنشاء",
    Edit: "تعديل",
    Delete: "حذف",
    Export: "تصدير",
    Approve: "موافقة",
  };

  const PERMISSION_GROUPS = [
    {
      title: "التشغيل اليومي",
      resources: ["daily_operations", "booking_requests", "clinic_queue", "clinic_display", "patient_journey", "visits", "checkout", "invoices", "rooms"],
    },
    {
      title: "العيادة",
      resources: ["patients", "appointments", "finance", "reports"],
    },
    {
      title: "التخصصات",
      resources: ["ortho", "general_dentistry", "surgery"],
    },
    {
      title: "النظام",
      resources: ["users", "user_management", "settings", "ai", "password_reset_requests", "impersonation"],
    },
  ];

  return (
    <div className="space-y-4">
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

      {/* Role selector */}
      <div className="flex items-center gap-3 flex-wrap">
        {ALL_ROLES.map((role) => (
          <button
            key={role}
            onClick={() => setSelectedRole(role)}
            className={cn(
              "px-3 py-1.5 text-sm font-medium rounded-lg border transition",
              selectedRole === role
                ? "bg-clinic-blue text-white border-clinic-blue"
                : "bg-white text-gray-700 border-gray-300 hover:border-clinic-blue"
            )}
          >
            {ROLE_LABELS[role] ?? role}
          </button>
        ))}
      </div>

      {/* Admin warning */}
      {selectedRole === "Admin" && (
        <div className="text-xs px-3 py-2 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start gap-2">
          <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span>مدير النظام يمتلك جميع الصلاحيات ولا يمكن تعديلها.</span>
        </div>
      )}

      {/* Permission Matrix — grouped by permission groups */}
      <div className="space-y-4">
        {PERMISSION_GROUPS.map((group) => {
          // Collect permissions for this group's resources
          const groupResources = group.resources.filter((r) => groupedPermissions[r]);
          if (groupResources.length === 0) return null;

          return (
            <div key={group.title} className="overflow-x-auto rounded-lg border border-gray-200">
              <div className="bg-gray-50 px-4 py-2.5 border-b border-gray-200">
                <h3 className="text-sm font-bold text-gray-800">{group.title}</h3>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-gray-50/50 border-b border-gray-200">
                  <tr>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[120px]">المورد</th>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[80px]">الإجراء</th>
                    <th className="text-center px-4 py-2.5 font-semibold text-gray-600">الصلاحية</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {groupResources.map((resource) =>
                    (groupedPermissions[resource] ?? []).map((p, idx) => {
                      const isChecked = rolePermissionsMap[selectedRole]?.has(p.key) ?? false;
                      const isAdmin = selectedRole === "Admin";
                      return (
                        <tr key={p.key} className="hover:bg-gray-50 transition">
                          <td className="px-4 py-2.5 text-gray-700 font-medium">
                            {idx === 0 ? (RESOURCE_LABELS[resource] ?? resource) : ""}
                          </td>
                          <td className="px-4 py-2.5 text-gray-600">
                            {ACTION_LABELS[p.action] ?? p.action}
                          </td>
                          <td className="px-4 py-2.5 text-center">
                            <input
                              type="checkbox"
                              checked={isAdmin || isChecked}
                              disabled={isAdmin}
                              onChange={() => togglePermission(selectedRole, p.key)}
                              className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue cursor-pointer disabled:cursor-not-allowed"
                            />
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          );
        })}

        {/* Show any ungrouped resources */}
        {(() => {
          const groupedResourceKeys = new Set(PERMISSION_GROUPS.flatMap((g) => g.resources));
          const ungroupedResources = Object.keys(groupedPermissions).filter((r) => !groupedResourceKeys.has(r));
          if (ungroupedResources.length === 0) return null;
          return (
            <div className="overflow-x-auto rounded-lg border border-gray-200">
              <div className="bg-gray-50 px-4 py-2.5 border-b border-gray-200">
                <h3 className="text-sm font-bold text-gray-800">أخرى</h3>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-gray-50/50 border-b border-gray-200">
                  <tr>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[120px]">المورد</th>
                    <th className="text-start px-4 py-2.5 font-semibold text-gray-700 min-w-[80px]">الإجراء</th>
                    <th className="text-center px-4 py-2.5 font-semibold text-gray-600">الصلاحية</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {ungroupedResources.map((resource) =>
                    (groupedPermissions[resource] ?? []).map((p, idx) => {
                      const isChecked = rolePermissionsMap[selectedRole]?.has(p.key) ?? false;
                      const isAdmin = selectedRole === "Admin";
                      return (
                        <tr key={p.key} className="hover:bg-gray-50 transition">
                          <td className="px-4 py-2.5 text-gray-700 font-medium">
                            {idx === 0 ? (RESOURCE_LABELS[resource] ?? resource) : ""}
                          </td>
                          <td className="px-4 py-2.5 text-gray-600">
                            {ACTION_LABELS[p.action] ?? p.action}
                          </td>
                          <td className="px-4 py-2.5 text-center">
                            <input
                              type="checkbox"
                              checked={isAdmin || isChecked}
                              disabled={isAdmin}
                              onChange={() => togglePermission(selectedRole, p.key)}
                              className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue cursor-pointer disabled:cursor-not-allowed"
                            />
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          );
        })()}
      </div>

      {/* Save button */}
      {selectedRole !== "Admin" && (
        <div className="flex items-center gap-3 pt-2">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {saving ? "جارٍ الحفظ..." : "حفظ الصلاحيات"}
          </button>
        </div>
      )}

      {/* Admin warning dialog */}
      {showAdminWarning && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={() => setShowAdminWarning(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-4">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center bg-amber-100">
                <AlertTriangle className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <h3 className="text-base font-bold text-gray-900">تحذير: تعديل صلاحيات المدير</h3>
                <p className="text-sm text-gray-600 mt-1">
                  تعديل صلاحيات مدير النظام قد يؤثر على قدرة المدير على إدارة النظام. هل أنت متأكد؟
                </p>
                <div className="flex items-center justify-end gap-3 pt-4">
                  <button
                    onClick={() => setShowAdminWarning(false)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
                  >
                    إلغاء
                  </button>
                  <button
                    onClick={async () => {
                      setShowAdminWarning(false);
                      await savePermissions("Admin");
                    }}
                    className="px-4 py-2 text-sm font-medium text-white bg-amber-600 rounded-lg hover:bg-amber-700 transition"
                  >
                    متابعة بالحفظ
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Email Tab ────────────────────────────────────────────────────────────────
function EmailTab() {
  const { data: stats, isLoading } = useEmailStats();
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const { data: history } = useEmailHistory(
    undefined,
    undefined,
    categoryFilter === "all" ? undefined : categoryFilter,
  );

  const CATEGORY_LABELS: Record<string, string> = {
    password_reset: "استعادة كلمة المرور",
    appointment_reminder: "تذكير موعد",
    general: "عام",
  };

  if (isLoading) {
    return (
      <div className="animate-pulse space-y-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  const limit = stats?.limit;
  const today = stats?.today;
  const week = stats?.week;

  return (
    <div className="space-y-6">
      {/* Daily Limit Alert */}
      {limit?.isAtLimit && (
        <div className="flex items-center gap-3 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-red-800">
          <MailWarning className="w-5 h-5 flex-shrink-0" />
          <div>
            <p className="font-medium">تم بلوغ الحد اليومي لإرسال البريد!</p>
            <p className="text-sm text-red-700">
              تم إرسال {limit.used} من {limit.dailyLimit} رسالة اليوم. لن يتم إرسال المزيد حتى الغد.
              يرجى ترقية خطة Resend أو التحقق من النطاق.
            </p>
          </div>
        </div>
      )}
      {limit?.isNearLimit && !limit.isAtLimit && (
        <div className="flex items-center gap-3 px-4 py-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-800">
          <MailWarning className="w-5 h-5 flex-shrink-0" />
          <div>
            <p className="font-medium">قرب الحد اليومي للبريد</p>
            <p className="text-sm text-amber-700">
              متبقي {limit.remaining} رسالة فقط من أصل {limit.dailyLimit} اليوم. فكر في ترقية خطة Resend.
            </p>
          </div>
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-green-50 flex items-center justify-center">
              <Send className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">مرسلة اليوم</p>
              <p className="text-xl font-bold text-gray-900">{today?.sent ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-red-50 flex items-center justify-center">
              <XCircle className="w-5 h-5 text-red-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">فاشلة اليوم</p>
              <p className="text-xl font-bold text-gray-900">{today?.failed ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center">
              <Clock className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">هذا الأسبوع</p>
              <p className="text-xl font-bold text-gray-900">{week?.sent ?? 0}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-purple-50 flex items-center justify-center">
              <Mail className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">الحد اليومي</p>
              <p className="text-xl font-bold text-gray-900">
                {limit?.used ?? 0}<span className="text-sm text-gray-400 font-normal">/{limit?.dailyLimit ?? 100}</span>
              </p>
            </div>
          </div>
          {/* Progress bar */}
          <div className="mt-2 w-full bg-gray-100 rounded-full h-1.5">
            <div
              className={cn(
                "h-1.5 rounded-full transition-all",
                (limit?.percentage ?? 0) >= 90 ? "bg-red-500" : (limit?.percentage ?? 0) >= 70 ? "bg-amber-500" : "bg-green-500"
              )}
              style={{ width: `${Math.min(limit?.percentage ?? 0, 100)}%` }}
            />
          </div>
        </div>
      </div>

      {/* Resend Domain Setup Guide */}
      <div className="bg-blue-50 border border-blue-200 rounded-xl p-5">
        <h3 className="font-bold text-blue-900 flex items-center gap-2 mb-3">
          <Globe className="w-5 h-5" />
          إعداد نطاق Resend (لإرسال بريد للمرضى)
        </h3>
        <p className="text-sm text-blue-800 mb-3">
          حالياً Resend يرسل رسائل فقط لحسابك المسجل (aqlanf10@gmail.com) لأن النطاق غير مفعل.
          لإرسال بريد للمرضى، تحتاج إضافة نطاقك الخاص في Resend:
        </p>
        <ol className="text-sm text-blue-800 space-y-2 list-decimal list-inside">
          <li>ادخل على <a href="https://resend.com/domains" target="_blank" rel="noopener noreferrer" className="underline font-medium">resend.com/domains</a></li>
          <li>أضف نطاقك (مثلاً aqlandental.com)</li>
          <li>أضف سجلات DNS المطلوبة (SPF, DKIM, DMARC) في لوحة تحكم النطاق</li>
          <li>بعد التحقق، غيّر <code className="bg-blue-100 px-1 rounded">SMTP_FROM_EMAIL</code> إلى <code className="bg-blue-100 px-1 rounded">noreply@aqlandental.com</code></li>
          <li>بعد التحقق، يمكنك إرسال بريد لأي عنوان</li>
        </ol>
        <p className="text-xs text-blue-600 mt-3">
          بدون هذه الخطوة، الإرسال مقتصر على بريدك فقط (حساب Resend المجاني).
        </p>
      </div>

      {/* Recent Emails */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2">
            <Mail className="w-4 h-4 text-clinic-blue" />
            آخر الرسائل
          </h3>
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            className="px-3 py-1.5 text-xs rounded-lg border border-gray-300 bg-white"
          >
            <option value="all">جميع الأنواع</option>
            <option value="password_reset">استعادة كلمة المرور</option>
            <option value="appointment_reminder">تذكير موعد</option>
            <option value="general">عام</option>
          </select>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {["الموضوع", "البريد", "النوع", "المزود", "الحالة", "التاريخ"].map((h) => (
                  <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {(history?.emails ?? stats?.recentEmails ?? []).length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                    لا توجد رسائل مسجلة بعد
                  </td>
                </tr>
              ) : (
                (history?.emails ?? stats?.recentEmails ?? []).map((email) => (
                  <tr key={email.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900 max-w-[200px] truncate">{email.subject}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs font-mono" dir="ltr">{email.toEmail}</td>
                    <td className="px-4 py-3">
                      <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">
                        {CATEGORY_LABELS[email.category] ?? email.category}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">{email.provider ?? "—"}</td>
                    <td className="px-4 py-3">
                      {email.isSent ? (
                        <span className="text-xs bg-green-50 text-green-700 px-2 py-0.5 rounded-full font-medium">تم الإرسال</span>
                      ) : (
                        <span className="text-xs bg-red-50 text-red-600 px-2 py-0.5 rounded-full font-medium" title={email.errorMessage ?? ""}>
                          فشل
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {new Date(email.createdAt).toLocaleDateString("ar-YE", {
                        year: "numeric", month: "short", day: "numeric",
                        hour: "2-digit", minute: "2-digit",
                      })}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
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
        <p className="text-sm text-gray-500 mt-0.5">إدارة إعدادات المركز والمستخدمين</p>
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
                  ? "border-clinic-blue text-clinic-blue"
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
          {activeTab === "users"  && <UsersTab />}
          {activeTab === "roles"  && <RolesTab />}
          {activeTab === "email"  && <EmailTab />}
        </div>
      </div>

      {/* Quick links */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Link
          href="/settings/website"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center flex-shrink-0 group-hover:bg-blue-100 transition">
            <Globe className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">إعدادات الموقع</p>
            <p className="text-sm text-gray-500">تحكم بمحتوى الصفحة الرئيسية والعنوان والتواصل</p>
          </div>
        </Link>
        <Link
          href="/settings/audit"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-purple-50 flex items-center justify-center flex-shrink-0 group-hover:bg-purple-100 transition">
            <FileSearch className="w-5 h-5 text-purple-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">سجل التدقيق</p>
            <p className="text-sm text-gray-500">عرض كل العمليات المنفذة في النظام</p>
          </div>
        </Link>
        <Link
          href="/settings/services"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-emerald-50 flex items-center justify-center flex-shrink-0 group-hover:bg-emerald-100 transition">
            <Stethoscope className="w-5 h-5 text-emerald-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">خدمات العيادة</p>
            <p className="text-sm text-gray-500">إدارة كتالوج الخدمات والأسعار</p>
          </div>
        </Link>
        <Link
          href="/settings/rooms"
          className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
        >
          <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center flex-shrink-0 group-hover:bg-amber-100 transition">
            <DoorOpen className="w-5 h-5 text-amber-600" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">غرف العيادة</p>
            <p className="text-sm text-gray-500">إدارة الغرف وتوزيعها</p>
          </div>
        </Link>
      </div>
    </div>
  );
}
