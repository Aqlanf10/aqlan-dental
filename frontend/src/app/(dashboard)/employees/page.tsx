"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import {
  UserCog,
  Plus,
  Search,
  Pencil,
  Trash2,
  Power,
  PowerOff,
  Loader2,
  AlertTriangle,
  Eye,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useBranches } from "@/hooks/useBranches";
import { toast } from "@/stores/toastStore";
import {
  type Employee,
  ROLE_LABELS,
} from "@/types/employee";
import { TableSkeleton } from "@/components/ui/skeleton";

// ─── Role badge colors ──────────────────────────────────────────────────────

const ROLE_COLORS: Record<string, string> = {
  Admin: "bg-purple-100 text-purple-700",
  Orthodontist: "bg-sky-100 text-sky-700",
  GeneralDentist: "bg-emerald-100 text-emerald-700",
  OralSurgeon: "bg-red-100 text-red-700",
  Reception: "bg-amber-100 text-amber-700",
  Accountant: "bg-indigo-100 text-indigo-700",
  Assistant: "bg-teal-100 text-teal-700",
  BranchManager: "bg-orange-100 text-orange-700",
};

function getRoleBadgeColor(role: string): string {
  return ROLE_COLORS[role] ?? "bg-gray-100 text-gray-700";
}

// ─── Page Component ─────────────────────────────────────────────────────────

export default function EmployeesPage() {
  // Filters
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("");
  const [branchFilter, setBranchFilter] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);

  // Data
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [totalPages, setTotalPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Actions
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [confirmDialog, setConfirmDialog] = useState<{
    open: boolean;
    title: string;
    message: string;
    confirmLabel: string;
    variant: "danger" | "warning";
    onConfirm: () => void;
  } | null>(null);

  const { data: branches } = useBranches();

  // ── Fetch employees ──

  const fetchEmployees = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (search.trim()) params.set("search", search.trim());
      if (roleFilter) params.set("role", roleFilter);
      if (branchFilter) params.set("branchId", branchFilter);
      params.set("page", String(page));
      params.set("pageSize", String(pageSize));

      const { data } = await api.get<{
        data: Employee[];
        total: number;
        page: number;
        totalPages: number;
      }>(`/api/employees?${params.toString()}`);

      setEmployees(data.data);
      setTotal(data.total);
      setTotalPages(data.totalPages);
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? ((err as { response?: { data?: { message?: string } } }).response?.data
              ?.message ?? "حدث خطأ أثناء تحميل البيانات")
          : "حدث خطأ أثناء تحميل البيانات";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, [search, roleFilter, branchFilter, page, pageSize]);

  useEffect(() => {
    fetchEmployees();
  }, [fetchEmployees]);

  // Reset page when filters change
  useEffect(() => {
    setPage(1);
  }, [search, roleFilter, branchFilter]);

  // ── Toggle status ──

  const handleToggleStatus = (employee: Employee) => {
    setConfirmDialog({
      open: true,
      title: employee.isActive ? "تعطيل الموظف" : "تفعيل الموظف",
      message: employee.isActive
        ? `هل أنت متأكد من تعطيل الموظف "${employee.fullName}"؟`
        : `هل أنت متأكد من تفعيل الموظف "${employee.fullName}"؟`,
      confirmLabel: employee.isActive ? "تعطيل" : "تفعيل",
      variant: "warning",
      onConfirm: async () => {
        setConfirmDialog(null);
        setTogglingId(employee.id);
        try {
          await api.put(`/api/employees/${employee.id}/status`);
          toast.success(
            employee.isActive
              ? "تم تعطيل الموظف بنجاح"
              : "تم تفعيل الموظف بنجاح"
          );
          fetchEmployees();
        } catch (err: unknown) {
          const msg =
            err && typeof err === "object" && "response" in err
              ? ((err as { response?: { data?: { message?: string } } }).response
                  ?.data?.message ?? "حدث خطأ")
              : "حدث خطأ";
          toast.error(msg);
        } finally {
          setTogglingId(null);
        }
      },
    });
  };

  // ── Delete ──

  const handleDelete = (employee: Employee) => {
    setConfirmDialog({
      open: true,
      title: "حذف الموظف",
      message: `هل أنت متأكد من حذف الموظف "${employee.fullName}"؟ سيتم تعطيل حساب الموظف مع بقاء حساب المستخدم نشطاً.`,
      confirmLabel: "حذف",
      variant: "danger",
      onConfirm: async () => {
        setConfirmDialog(null);
        setDeletingId(employee.id);
        try {
          await api.delete(`/api/employees/${employee.id}`);
          toast.success("تم حذف الموظف بنجاح");
          fetchEmployees();
        } catch (err: unknown) {
          const msg =
            err && typeof err === "object" && "response" in err
              ? ((err as { response?: { data?: { message?: string } } }).response
                  ?.data?.message ?? "حدث خطأ")
              : "حدث خطأ";
          toast.error(msg);
        } finally {
          setDeletingId(null);
        }
      },
    });
  };

  // ── Format helpers ──

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleDateString("ar-SA");
  };

  const formatSalary = (salary?: number) => {
    if (salary == null) return "—";
    return salary.toLocaleString("ar-SA");
  };

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6" dir="rtl">
      {/* ── Header ── */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">إدارة الموظفين</h1>
          <p className="text-sm text-gray-500 mt-1">
            إدارة موظفين العيادة وبياناتهم الوظيفية
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#1a3a5c]/10 flex items-center justify-center">
            <UserCog className="w-5 h-5 text-[#1a3a5c]" />
          </div>
          <Link
            href="/employees/new"
            className="flex items-center gap-2 bg-[#f5922e] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm"
          >
            <Plus className="w-4 h-4" />
            إضافة موظف
          </Link>
        </div>
      </div>

      {/* ── Filters Bar ── */}
      <div className="flex flex-wrap gap-3 items-center">
        {/* Search input */}
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو اسم المستخدم..."
            className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition bg-white"
          />
        </div>

        {/* Role filter */}
        <select
          value={roleFilter}
          onChange={(e) => setRoleFilter(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition min-w-[160px]"
        >
          <option value="">جميع الأدوار</option>
          {Object.entries(ROLE_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>

        {/* Branch filter */}
        <select
          value={branchFilter}
          onChange={(e) => setBranchFilter(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition min-w-[160px]"
        >
          <option value="">جميع الفروع</option>
          {branches?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </div>

      {/* ── Content ── */}
      {isLoading ? (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <TableSkeleton rows={5} cols={7} />
        </div>
      ) : error ? (
        <div className="flex items-center justify-center py-24">
          <div className="text-center">
            <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
            <p className="text-gray-600 font-medium mb-2">{error}</p>
            <button
              onClick={fetchEmployees}
              className="text-sm text-[#1a3a5c] hover:underline font-medium"
            >
              إعادة المحاولة
            </button>
          </div>
        </div>
      ) : employees.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <UserCog className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">لا يوجد موظفين</p>
          <p className="text-sm mt-1 text-gray-300">
            قم بإضافة موظف جديد للبدء
          </p>
        </div>
      ) : (
        <>
          {/* Table */}
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الاسم
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      اسم المستخدم
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الدور
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الفرع
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الهاتف
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      تاريخ التعيين
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الراتب
                    </th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">
                      الحالة
                    </th>
                    <th className="text-center px-4 py-3 font-semibold text-gray-600">
                      الإجراءات
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {employees.map((emp) => (
                    <tr
                      key={emp.id}
                      className={cn(
                        "hover:bg-gray-50/50 transition",
                        !emp.isActive && "opacity-60"
                      )}
                    >
                      {/* Name */}
                      <td className="px-4 py-3">
                        <Link
                          href={`/employees/${emp.id}`}
                          className="font-medium text-[#0d2137] hover:text-[#1a3a5c] transition"
                        >
                          {emp.fullName}
                        </Link>
                      </td>

                      {/* Username */}
                      <td className="px-4 py-3 text-gray-600">
                        {emp.username}
                      </td>

                      {/* Role */}
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full",
                            getRoleBadgeColor(emp.role)
                          )}
                        >
                          {ROLE_LABELS[emp.role] ?? emp.role}
                        </span>
                      </td>

                      {/* Branch */}
                      <td className="px-4 py-3 text-gray-500">
                        {emp.branchName ?? "—"}
                      </td>

                      {/* Phone */}
                      <td className="px-4 py-3 text-gray-500 font-mono text-xs" dir="ltr">
                        {emp.phone ?? "—"}
                      </td>

                      {/* Hire Date */}
                      <td className="px-4 py-3 text-gray-500">
                        {formatDate(emp.hireDate)}
                      </td>

                      {/* Salary */}
                      <td className="px-4 py-3 text-gray-600 font-medium" dir="ltr">
                        {formatSalary(emp.baseSalary)}
                      </td>

                      {/* Status */}
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full",
                            emp.isActive
                              ? "bg-emerald-100 text-emerald-700"
                              : "bg-gray-200 text-gray-500"
                          )}
                        >
                          <span
                            className={cn(
                              "w-1.5 h-1.5 rounded-full",
                              emp.isActive ? "bg-emerald-500" : "bg-gray-400"
                            )}
                          />
                          {emp.isActive ? "نشط" : "غير نشط"}
                        </span>
                      </td>

                      {/* Actions */}
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-center gap-1">
                          <Link
                            href={`/employees/${emp.id}`}
                            className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-[#1a3a5c] hover:bg-[#1a3a5c]/10 transition"
                            title="عرض"
                          >
                            <Eye className="w-4 h-4" />
                          </Link>
                          <Link
                            href={`/employees/${emp.id}/edit`}
                            className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-[#1a3a5c] hover:bg-[#1a3a5c]/10 transition"
                            title="تعديل"
                          >
                            <Pencil className="w-4 h-4" />
                          </Link>
                          <button
                            onClick={() => handleToggleStatus(emp)}
                            disabled={togglingId === emp.id}
                            className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-amber-600 hover:bg-amber-50 transition disabled:opacity-50"
                            title={emp.isActive ? "تعطيل" : "تفعيل"}
                          >
                            {togglingId === emp.id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : emp.isActive ? (
                              <PowerOff className="w-4 h-4" />
                            ) : (
                              <Power className="w-4 h-4" />
                            )}
                          </button>
                          <button
                            onClick={() => handleDelete(emp)}
                            disabled={deletingId === emp.id}
                            className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-red-600 hover:bg-red-50 transition disabled:opacity-50"
                            title="حذف"
                          >
                            {deletingId === emp.id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <Trash2 className="w-4 h-4" />
                            )}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50/40">
                <p className="text-xs text-gray-500">
                  عرض {employees.length} من {total}
                </p>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-gray-600 hover:bg-gray-200 transition disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                  {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
                    let pageNum: number;
                    if (totalPages <= 5) {
                      pageNum = i + 1;
                    } else if (page <= 3) {
                      pageNum = i + 1;
                    } else if (page >= totalPages - 2) {
                      pageNum = totalPages - 4 + i;
                    } else {
                      pageNum = page - 2 + i;
                    }
                    return (
                      <button
                        key={pageNum}
                        onClick={() => setPage(pageNum)}
                        className={cn(
                          "w-8 h-8 rounded-lg flex items-center justify-center text-sm font-medium transition",
                          page === pageNum
                            ? "bg-[#1a3a5c] text-white"
                            : "text-gray-500 hover:text-gray-700 hover:bg-gray-200"
                        )}
                      >
                        {pageNum}
                      </button>
                    );
                  })}
                  <button
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-gray-600 hover:bg-gray-200 transition disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {/* ── Confirm Dialog ── */}
      {confirmDialog?.open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={() => setConfirmDialog(null)}
          />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden animate-in fade-in zoom-in-95">
            <div className="px-6 py-6 text-center">
              <div
                className={cn(
                  "w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4",
                  confirmDialog.variant === "danger"
                    ? "bg-red-100"
                    : "bg-amber-100"
                )}
              >
                <AlertTriangle
                  className={cn(
                    "w-7 h-7",
                    confirmDialog.variant === "danger"
                      ? "text-red-500"
                      : "text-amber-500"
                  )}
                />
              </div>
              <h3 className="text-lg font-bold text-gray-900 mb-2">
                {confirmDialog.title}
              </h3>
              <p className="text-sm text-gray-500 leading-relaxed">
                {confirmDialog.message}
              </p>
            </div>
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-center gap-3">
              <button
                onClick={() => setConfirmDialog(null)}
                className="px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition"
              >
                إلغاء
              </button>
              <button
                onClick={confirmDialog.onConfirm}
                className={cn(
                  "flex items-center gap-2 px-6 py-2.5 rounded-xl text-sm font-semibold text-white transition shadow-sm",
                  confirmDialog.variant === "danger"
                    ? "bg-red-600 hover:bg-red-700"
                    : "bg-amber-500 hover:bg-amber-600"
                )}
              >
                {confirmDialog.confirmLabel}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
