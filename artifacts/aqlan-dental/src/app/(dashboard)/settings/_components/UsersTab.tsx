// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.
//
// This file keeps UsersTab + CreateUserDialog + EditUserDialog together
// because the dialogs are tightly coupled to UsersTab (state, mutations,
// and form data flow through it).

import { useState, useCallback } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Plus, X, Search, Filter, UserCheck, UserX, RotateCcw, KeyRound, Copy,
  AlertTriangle, UserCog, Loader2, ShieldAlert, CheckCircle2, XCircle,
  Trash2, Save,
} from "lucide-react";
import Link from "@/lib/nextLinkCompat";
import { cn } from "@/lib/utils";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useAuthStore } from "@/stores/authStore";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import {
  useUsers,
  usePatientPortalAccounts,
  usePasswordResetRequests,
  useCreateUser,
  useEditUser,
  useToggleUserStatus,
  useDeleteUser,
  useRestoreUser,
  useResetUserPassword,
  useApproveResetRequest,
  useRejectResetRequest,
  type UserDetailDto,
  type CreateUserRequest,
  type EditUserRequest,
} from "@/hooks/useUsers";
import type { ImpersonateResponse } from "@/types/auth";
import { ALL_ROLES, ROLE_LABELS, inputCls } from "./_shared";

// ─── Users Tab (Complete Rewrite) ────────────────────────────────────────────
export function UsersTab() {
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
              onError: (err) => showToast(extractErrorMessage(err, "حدث خطأ أثناء إنشاء المستخدم"), "error"),
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
              onError: (err) => showToast(extractErrorMessage(err, "حدث خطأ أثناء التحديث"), "error"),
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

// ─── Create User Dialog (FE-30: react-hook-form + zod) ───────────────────────
// SEC-11: client-side mirror of the centralized PasswordPolicy (UX only —
// backend enforces). Arabic error messages preserved from the prior
// ad-hoc validator.
const createUserSchema = z.object({
  username: z.string().min(1, { message: "اسم المستخدم مطلوب" }),
  password: z
    .string()
    .min(8, { message: "كلمة المرور يجب أن تكون 8 أحرف على الأقل" })
    .refine((v) => /[A-Z]/.test(v), { message: "كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل" })
    .refine((v) => /[a-z]/.test(v), { message: "كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل" })
    .refine((v) => /[0-9]/.test(v), { message: "كلمة المرور يجب أن تحتوي على رقم واحد على الأقل" }),
  role: z.string().min(1, { message: "الدور مطلوب" }),
  email: z.string().optional(),
  doctorName: z.string().optional(),
  doctorSpecialty: z.string().optional(),
  doctorColor: z.string().optional(),
});
type CreateUserFormData = z.infer<typeof createUserSchema>;

export function CreateUserDialog({
  onClose,
  onSubmit,
  saving,
}: {
  onClose: () => void;
  onSubmit: (data: CreateUserRequest) => void;
  saving: boolean;
}) {
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<CreateUserFormData>({
    resolver: zodResolver(createUserSchema),
    defaultValues: {
      username: "",
      password: "",
      role: "Reception",
      email: "",
      doctorName: "",
      doctorSpecialty: "",
      doctorColor: "#3d7ab5",
    },
  });

  const doctorName = watch("doctorName");

  const onSubmitHandler = handleSubmit((formData) => {
    onSubmit({
      username: formData.username,
      password: formData.password,
      role: formData.role,
      email: formData.email || undefined,
      doctorName: formData.doctorName || undefined,
      doctorSpecialty: formData.doctorSpecialty || undefined,
      doctorColor: formData.doctorName ? formData.doctorColor : undefined,
    });
  });

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

        <form onSubmit={onSubmitHandler} className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم المستخدم <span className="text-red-500">*</span></label>
              <input
                {...register("username")}
                className={inputCls}
                placeholder="username"
                dir="ltr"
                autoComplete="off"
              />
              {errors.username && (
                <p className="text-xs text-red-600 mt-1">{errors.username.message}</p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">كلمة المرور <span className="text-red-500">*</span></label>
              <input
                {...register("password")}
                type="password"
                className={inputCls}
                autoComplete="new-password"
              />
              {errors.password && (
                <p className="text-xs text-red-600 mt-1">{errors.password.message}</p>
              )}
              {/* SEC-11: password complexity hint (UX only — backend enforces) */}
              <p className="text-gray-400 text-[11px] mt-1">8+ أحرف، يحتوي على رقم وحرف كبير</p>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
              <select {...register("role")} className={inputCls}>
                {ALL_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
              </select>
              {errors.role && (
                <p className="text-xs text-red-600 mt-1">{errors.role.message}</p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">البريد الإلكتروني</label>
              <input
                {...register("email")}
                type="email"
                className={inputCls}
                dir="ltr"
              />
              {errors.email && (
                <p className="text-xs text-red-600 mt-1">{errors.email.message}</p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم الطبيب (اختياري)</label>
              <input
                {...register("doctorName")}
                className={inputCls}
                placeholder="د. محمد أحمد"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">تخصص الطبيب</label>
              <input
                {...register("doctorSpecialty")}
                className={inputCls}
                placeholder="أخصائي تقويم"
              />
            </div>
            {doctorName && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">لون الطبيب</label>
                <input
                  {...register("doctorColor")}
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

// ─── Edit User Dialog (FE-30: react-hook-form + zod) ────────────────────────
// The prior ad-hoc dialog had no inline validation (just sent whatever was in
// the form). The zod schema here is therefore permissive — only username
// remains required to match the prior behaviour (it was always pre-filled
// from user.username, so the constraint never fires in practice).
const editUserSchema = z.object({
  username: z.string().min(1, { message: "اسم المستخدم مطلوب" }),
  email: z.string().optional(),
  role: z.string().optional(),
  doctorName: z.string().optional(),
  doctorSpecialty: z.string().optional(),
  doctorColor: z.string().optional(),
});
type EditUserFormData = z.infer<typeof editUserSchema>;

export function EditUserDialog({
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
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<EditUserFormData>({
    resolver: zodResolver(editUserSchema),
    defaultValues: {
      username: user.username,
      email: user.email ?? "",
      role: user.role,
      doctorName: user.doctorName ?? "",
      doctorSpecialty: user.doctorSpecialty ?? "",
      doctorColor: user.doctorColor ?? "#3d7ab5",
    },
  });

  const doctorName = watch("doctorName");

  const onSubmitHandler = handleSubmit((formData) => {
    onSubmit({
      username: formData.username,
      email: formData.email || undefined,
      role: formData.role,
      doctorName: formData.doctorName || undefined,
      doctorSpecialty: formData.doctorSpecialty || undefined,
      doctorColor: formData.doctorName ? formData.doctorColor : undefined,
    });
  });

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

        <form onSubmit={onSubmitHandler} className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم المستخدم</label>
              <input
                {...register("username")}
                className={inputCls}
                dir="ltr"
              />
              {errors.username && (
                <p className="text-xs text-red-600 mt-1">{errors.username.message}</p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">البريد الإلكتروني</label>
              <input
                {...register("email")}
                type="email"
                className={inputCls}
                dir="ltr"
              />
              {errors.email && (
                <p className="text-xs text-red-600 mt-1">{errors.email.message}</p>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">الدور</label>
              <select {...register("role")} className={inputCls}>
                {ALL_ROLES.map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم الطبيب (اختياري)</label>
              <input
                {...register("doctorName")}
                className={inputCls}
                placeholder="د. محمد أحمد"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">تخصص الطبيب</label>
              <input
                {...register("doctorSpecialty")}
                className={inputCls}
                placeholder="أخصائي تقويم"
              />
            </div>
            {doctorName && (
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">لون الطبيب</label>
                <input
                  {...register("doctorColor")}
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
