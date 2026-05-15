"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import {
  ChevronLeft,
  Loader2,
  AlertTriangle,
  Pencil,
  Trash2,
  Power,
  PowerOff,
  User,
  Phone,
  CreditCard,
  Briefcase,
  MapPin,
  CalendarDays,
  Banknote,
  FileText,
  Shield,
  UserCog,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import {
  type Employee,
  ROLE_LABELS,
  POSITION_LABELS,
} from "@/types/employee";

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

function formatDate(dateStr?: string): string {
  if (!dateStr) return "—";
  return new Date(dateStr).toLocaleDateString("ar-SA", {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function formatSalary(salary?: number): string {
  if (salary == null) return "—";
  return salary.toLocaleString("ar-SA") + " ر.س";
}

// ─── Page Component ─────────────────────────────────────────────────────────

export default function EmployeeDetailsPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();

  const [employee, setEmployee] = useState<Employee | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Actions
  const [toggling, setToggling] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [confirmDialog, setConfirmDialog] = useState<{
    open: boolean;
    title: string;
    message: string;
    confirmLabel: string;
    variant: "danger" | "warning";
    onConfirm: () => void;
  } | null>(null);

  const fetchEmployee = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const { data } = await api.get<Employee>(`/api/employees/${params.id}`);
      setEmployee(data);
    } catch {
      setLoadError("حدث خطأ أثناء تحميل بيانات الموظف");
    } finally {
      setIsLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    fetchEmployee();
  }, [fetchEmployee]);

  // ── Toggle status ──

  const handleToggleStatus = () => {
    if (!employee) return;
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
        setToggling(true);
        try {
          await api.put(`/api/employees/${employee.id}/status`);
          toast.success(
            employee.isActive
              ? "تم تعطيل الموظف بنجاح"
              : "تم تفعيل الموظف بنجاح"
          );
          fetchEmployee();
        } catch (err: unknown) {
          const msg =
            err && typeof err === "object" && "response" in err
              ? ((err as { response?: { data?: { message?: string } } }).response
                  ?.data?.message ?? "حدث خطأ")
              : "حدث خطأ";
          toast.error(msg);
        } finally {
          setToggling(false);
        }
      },
    });
  };

  // ── Delete ──

  const handleDelete = () => {
    if (!employee) return;
    setConfirmDialog({
      open: true,
      title: "حذف الموظف",
      message: `هل أنت متأكد من حذف الموظف "${employee.fullName}"؟ سيتم تعطيل حساب الموظف مع بقاء حساب المستخدم نشطاً.`,
      confirmLabel: "حذف",
      variant: "danger",
      onConfirm: async () => {
        setConfirmDialog(null);
        setDeleting(true);
        try {
          await api.delete(`/api/employees/${employee.id}`);
          toast.success("تم حذف الموظف بنجاح");
          router.push("/employees");
        } catch (err: unknown) {
          const msg =
            err && typeof err === "object" && "response" in err
              ? ((err as { response?: { data?: { message?: string } } }).response
                  ?.data?.message ?? "حدث خطأ")
              : "حدث خطأ";
          toast.error(msg);
        } finally {
          setDeleting(false);
        }
      },
    });
  };

  // ─── Render ─────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 gap-3 text-gray-400">
        <Loader2 className="w-6 h-6 animate-spin" />
        <span className="text-sm">جارٍ تحميل بيانات الموظف...</span>
      </div>
    );
  }

  if (loadError || !employee) {
    return (
      <div className="flex items-center justify-center py-24">
        <div className="text-center">
          <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
          <p className="text-gray-600 font-medium mb-2">
            {loadError ?? "الموظف غير موجود"}
          </p>
          <Link
            href="/employees"
            className="text-sm text-[#1a3a5c] hover:underline font-medium"
          >
            العودة للقائمة
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6" dir="rtl">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/employees" className="hover:text-gray-700 transition">
          الموظفين
        </Link>
        <ChevronLeft className="w-4 h-4" />
        <span className="text-[#0d2137] font-medium">بيانات الموظف</span>
      </nav>

      {/* Header Card */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="bg-[#0d2137] px-6 py-5">
          <div className="flex items-start justify-between flex-wrap gap-4">
            <div className="flex items-center gap-4">
              {/* Avatar */}
              <div
                className="w-14 h-14 rounded-full flex items-center justify-center text-white font-bold text-xl shadow-sm flex-shrink-0"
                style={{ backgroundColor: "#f5922e" }}
              >
                {employee.fullName.charAt(0)}
              </div>
              <div>
                <h1 className="text-white font-bold text-xl">
                  {employee.fullName}
                </h1>
                <div className="flex items-center gap-3 mt-1.5 flex-wrap">
                  <span className="text-white/60 text-sm">
                    @{employee.username}
                  </span>
                  <span
                    className={cn(
                      "inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full",
                      getRoleBadgeColor(employee.role)
                    )}
                  >
                    <Shield className="w-3 h-3 ml-1" />
                    {ROLE_LABELS[employee.role] ?? employee.role}
                  </span>
                  <span
                    className={cn(
                      "inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full",
                      employee.isActive
                        ? "bg-emerald-500/20 text-emerald-300"
                        : "bg-gray-500/20 text-gray-400"
                    )}
                  >
                    <span
                      className={cn(
                        "w-1.5 h-1.5 rounded-full",
                        employee.isActive ? "bg-emerald-400" : "bg-gray-500"
                      )}
                    />
                    {employee.isActive ? "نشط" : "غير نشط"}
                  </span>
                </div>
              </div>
            </div>

            {/* Action buttons */}
            <div className="flex items-center gap-2">
              <Link
                href={`/employees/${employee.id}/edit`}
                className="flex items-center gap-1.5 bg-white/10 hover:bg-white/20 text-white px-3 py-2 rounded-xl text-sm font-medium transition"
              >
                <Pencil className="w-3.5 h-3.5" />
                تعديل
              </Link>
              <button
                onClick={handleToggleStatus}
                disabled={toggling}
                className={cn(
                  "flex items-center gap-1.5 px-3 py-2 rounded-xl text-sm font-medium transition disabled:opacity-50",
                  employee.isActive
                    ? "bg-amber-500/20 hover:bg-amber-500/30 text-amber-300"
                    : "bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-300"
                )}
              >
                {toggling ? (
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                ) : employee.isActive ? (
                  <PowerOff className="w-3.5 h-3.5" />
                ) : (
                  <Power className="w-3.5 h-3.5" />
                )}
                {employee.isActive ? "تعطيل" : "تفعيل"}
              </button>
              <button
                onClick={handleDelete}
                disabled={deleting}
                className="flex items-center gap-1.5 bg-red-500/20 hover:bg-red-500/30 text-red-300 px-3 py-2 rounded-xl text-sm font-medium transition disabled:opacity-50"
              >
                {deleting ? (
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Trash2 className="w-3.5 h-3.5" />
                )}
                حذف
              </button>
            </div>
          </div>
        </div>

        {/* Quick info bar */}
        <div className="flex items-center gap-6 px-6 py-3 bg-gray-50/60 border-b border-gray-100 text-xs text-gray-500 flex-wrap">
          {employee.branchName && (
            <span className="flex items-center gap-1.5">
              <MapPin className="w-3.5 h-3.5 text-gray-400" />
              {employee.branchName}
            </span>
          )}
          {employee.hireDate && (
            <span className="flex items-center gap-1.5">
              <CalendarDays className="w-3.5 h-3.5 text-gray-400" />
              تاريخ التعيين: {formatDate(employee.hireDate)}
            </span>
          )}
          <span className="flex items-center gap-1.5">
            <UserCog className="w-3.5 h-3.5 text-gray-400" />
            {POSITION_LABELS[employee.position ?? ""] ?? employee.position ?? "—"}
          </span>
        </div>
      </div>

      {/* Details Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* ── المعلومات الشخصية ── */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
            <User className="w-4 h-4 text-[#1a3a5c]" />
            المعلومات الشخصية
          </h3>
          <div className="space-y-4">
            <InfoRow
              icon={<Phone className="w-4 h-4" />}
              label="رقم الهاتف"
              value={employee.phone}
              dir="ltr"
            />
            <InfoRow
              icon={<CreditCard className="w-4 h-4" />}
              label="رقم الهوية"
              value={employee.nationalId}
              dir="ltr"
            />
            <InfoRow
              icon={<User className="w-4 h-4" />}
              label="جهة اتصال الطوارئ"
              value={employee.emergencyContact}
            />
            <InfoRow
              icon={<Phone className="w-4 h-4" />}
              label="هاتف الطوارئ"
              value={employee.emergencyPhone}
              dir="ltr"
            />
          </div>
        </div>

        {/* ── المعلومات الوظيفية ── */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
            <Briefcase className="w-4 h-4 text-[#1a3a5c]" />
            المعلومات الوظيفية
          </h3>
          <div className="space-y-4">
            <InfoRow
              icon={<Briefcase className="w-4 h-4" />}
              label="المسمى الوظيفي"
              value={
                POSITION_LABELS[employee.position ?? ""] ?? employee.position
              }
            />
            <InfoRow
              icon={<MapPin className="w-4 h-4" />}
              label="الفرع"
              value={employee.branchName}
            />
            <InfoRow
              icon={<CalendarDays className="w-4 h-4" />}
              label="تاريخ التعيين"
              value={formatDate(employee.hireDate)}
            />
            <InfoRow
              icon={<Banknote className="w-4 h-4" />}
              label="الراتب الأساسي"
              value={formatSalary(employee.baseSalary)}
            />
          </div>
        </div>
      </div>

      {/* ── ملاحظات ── */}
      {employee.notes && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
          <h3 className="text-base font-bold text-[#0d2137] mb-3 flex items-center gap-2">
            <FileText className="w-4 h-4 text-[#1a3a5c]" />
            ملاحظات
          </h3>
          <p className="text-sm text-gray-600 leading-relaxed whitespace-pre-wrap">
            {employee.notes}
          </p>
        </div>
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

// ─── Info Row Component ─────────────────────────────────────────────────────

function InfoRow({
  icon,
  label,
  value,
  dir,
}: {
  icon: React.ReactNode;
  label: string;
  value?: string | null;
  dir?: string;
}) {
  return (
    <div className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
      <span className="flex items-center gap-2 text-sm text-gray-500">
        <span className="text-gray-400">{icon}</span>
        {label}
      </span>
      <span
        className={cn(
          "text-sm font-medium text-gray-800",
          !value && "text-gray-400"
        )}
        dir={dir}
      >
        {value ?? "—"}
      </span>
    </div>
  );
}
