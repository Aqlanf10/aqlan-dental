
import { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "@/lib/nextNavCompat";
import Link from "@/lib/nextLinkCompat";
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
  Clock,
  CalendarOff,
  FileSpreadsheet,
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
  return salary.toLocaleString("ar-SA") + " ر.ي";
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
    <div className="space-y-6">
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

      {/* ── HR Tabs ── */}
      <EmployeeHRTabs employeeId={employee.id} />

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

// ─── Employee HR Tabs ─────────────────────────────────────────────────────

const ATTENDANCE_STATUS: Record<string, { label: string; color: string }> = {
  Present: { label: "حاضر", color: "bg-emerald-100 text-emerald-700" },
  Absent: { label: "غائب", color: "bg-red-100 text-red-700" },
  Late: { label: "متأخر", color: "bg-amber-100 text-amber-700" },
  HalfDay: { label: "نصف يوم", color: "bg-sky-100 text-sky-700" },
  Leave: { label: "إجازة", color: "bg-purple-100 text-purple-700" },
  Holiday: { label: "عطلة", color: "bg-gray-100 text-gray-700" },
};

const REQUEST_STATUS: Record<string, { label: string; color: string }> = {
  Pending: { label: "قيد المراجعة", color: "bg-amber-100 text-amber-700" },
  Approved: { label: "مقبول", color: "bg-emerald-100 text-emerald-700" },
  Rejected: { label: "مرفوض", color: "bg-red-100 text-red-700" },
  Cancelled: { label: "ملغي", color: "bg-gray-200 text-gray-500" },
};

const LEAVE_TYPE_LABELS: Record<string, string> = {
  Annual: "سنوية", Sick: "مرضية", Emergency: "طارئة", Unpaid: "بدون راتب", Maternity: "أمومة", Hajj: "حج",
};

function EmployeeHRTabs({ employeeId }: { employeeId: string }) {
  const [activeTab, setActiveTab] = useState<"attendance" | "salaries" | "advances" | "leaves" | "documents">("attendance");
  const [data, setData] = useState<unknown[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      const endpoints: Record<string, string> = {
        attendance: `/api/attendance?employeeId=${employeeId}&pageSize=20`,
        salaries: `/api/salaries?employeeId=${employeeId}&pageSize=20`,
        advances: `/api/advances?employeeId=${employeeId}&pageSize=20`,
        leaves: `/api/leaves?employeeId=${employeeId}&pageSize=20`,
        documents: `/api/employee-documents?employeeId=${employeeId}&pageSize=20`,
      };
      const { data: res } = await api.get(endpoints[activeTab]);
      setData(res?.data || Array.isArray(res) ? (res?.data || res) : []);
    } catch {
      setData([]);
    } finally {
      setIsLoading(false);
    }
  }, [employeeId, activeTab]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const tabs = [
    { key: "attendance" as const, label: "الحضور", icon: <Clock className="w-4 h-4" /> },
    { key: "salaries" as const, label: "الرواتب", icon: <Banknote className="w-4 h-4" /> },
    { key: "advances" as const, label: "السلف", icon: <CreditCard className="w-4 h-4" /> },
    { key: "leaves" as const, label: "الإجازات", icon: <CalendarOff className="w-4 h-4" /> },
    { key: "documents" as const, label: "المستندات", icon: <FileText className="w-4 h-4" /> },
  ];

  const records = data as Record<string, unknown>[];

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
      {/* Tab Headers */}
      <div className="flex items-center border-b border-gray-100 overflow-x-auto">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={cn(
              "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
              activeTab === tab.key
                ? "text-[#f5922e] border-[#f5922e] bg-[#f5922e]/5"
                : "text-gray-500 border-transparent hover:text-gray-700 hover:bg-gray-50"
            )}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="p-4">
        {isLoading ? (
          <div className="flex items-center justify-center py-8 gap-2 text-gray-400">
            <Loader2 className="w-5 h-5 animate-spin" />
            <span className="text-sm">جارٍ التحميل...</span>
          </div>
        ) : records.length === 0 ? (
          <div className="text-center py-8 text-gray-400">
            <FileSpreadsheet className="w-8 h-8 mx-auto mb-2 opacity-40" />
            <p className="text-sm">لا يوجد بيانات</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            {activeTab === "attendance" && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">التاريخ</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الحضور</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الانصراف</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الحالة</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec, i) => {
                    const cfg = ATTENDANCE_STATUS[String(rec.status)] ?? { label: String(rec.status), color: "bg-gray-100 text-gray-700" };
                    return (
                      <tr key={i} className="hover:bg-gray-50/50">
                        <td className="px-3 py-2 text-gray-600 text-xs">{String(rec.date ?? "—")}</td>
                        <td className="px-3 py-2 font-mono text-xs" dir="ltr">{String(rec.checkIn ?? "—")}</td>
                        <td className="px-3 py-2 font-mono text-xs" dir="ltr">{String(rec.checkOut ?? "—")}</td>
                        <td className="px-3 py-2">
                          <span className={cn("inline-flex text-[10px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>{cfg.label}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}

            {activeTab === "salaries" && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الشهر</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الأساسي</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الخصومات</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">السلف</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">البدلات</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الصافي</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الحالة</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec, i) => (
                    <tr key={i} className="hover:bg-gray-50/50">
                      <td className="px-3 py-2 text-gray-600 text-xs">{String(rec.year ?? "")}/{String(rec.month ?? "")}</td>
                      <td className="px-3 py-2 text-xs" dir="ltr">{Number(rec.baseSalary).toLocaleString("ar-SA")}</td>
                      <td className="px-3 py-2 text-xs text-red-600" dir="ltr">{Number(rec.deductions).toLocaleString("ar-SA")}</td>
                      <td className="px-3 py-2 text-xs text-amber-600" dir="ltr">{Number(rec.advances).toLocaleString("ar-SA")}</td>
                      <td className="px-3 py-2 text-xs text-emerald-600" dir="ltr">{Number(rec.bonuses).toLocaleString("ar-SA")}</td>
                      <td className="px-3 py-2 text-xs font-bold" dir="ltr">{Number(rec.netSalary).toLocaleString("ar-SA")} ر.ي</td>
                      <td className="px-3 py-2">
                        <span className={cn("inline-flex text-[10px] font-semibold px-2 py-0.5 rounded-full",
                          rec.paidAt ? "bg-emerald-100 text-emerald-700" : "bg-amber-100 text-amber-700")}>
                          {rec.paidAt ? "مدفوع" : "غير مدفوع"}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {activeTab === "advances" && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">المبلغ</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">السبب</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">تاريخ الطلب</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الخص من</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الحالة</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec, i) => {
                    const cfg = REQUEST_STATUS[String(rec.status)] ?? { label: String(rec.status), color: "bg-gray-100 text-gray-700" };
                    return (
                      <tr key={i} className="hover:bg-gray-50/50">
                        <td className="px-3 py-2 text-xs font-bold" dir="ltr">{Number(rec.amount).toLocaleString("ar-SA")} ر.ي</td>
                        <td className="px-3 py-2 text-xs text-gray-500 max-w-[150px] truncate">{String(rec.reason ?? "—")}</td>
                        <td className="px-3 py-2 text-xs text-gray-500">{String(rec.requestDate ?? "—").split("T")[0]}</td>
                        <td className="px-3 py-2 text-xs text-gray-500">{rec.deductFromMonth ? `${rec.deductFromYear}/${rec.deductFromMonth}` : "—"}</td>
                        <td className="px-3 py-2">
                          <span className={cn("inline-flex text-[10px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>{cfg.label}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}

            {activeTab === "leaves" && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">النوع</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">من</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">إلى</th>
                    <th className="text-center px-3 py-2.5 font-semibold text-gray-600 text-xs">الأيام</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الحالة</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec, i) => {
                    const cfg = REQUEST_STATUS[String(rec.status)] ?? { label: String(rec.status), color: "bg-gray-100 text-gray-700" };
                    return (
                      <tr key={i} className="hover:bg-gray-50/50">
                        <td className="px-3 py-2 text-xs font-semibold text-[#1a3a5c]">{LEAVE_TYPE_LABELS[String(rec.leaveType)] ?? String(rec.leaveType)}</td>
                        <td className="px-3 py-2 text-xs text-gray-500">{String(rec.startDate ?? "—")}</td>
                        <td className="px-3 py-2 text-xs text-gray-500">{String(rec.endDate ?? "—")}</td>
                        <td className="px-3 py-2 text-xs text-center font-bold">{String(rec.totalDays ?? "—")}</td>
                        <td className="px-3 py-2">
                          <span className={cn("inline-flex text-[10px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>{cfg.label}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}

            {activeTab === "documents" && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">العنوان</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">النوع</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">الملف</th>
                    <th className="text-right px-3 py-2.5 font-semibold text-gray-600 text-xs">التاريخ</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec, i) => (
                    <tr key={i} className="hover:bg-gray-50/50">
                      <td className="px-3 py-2 text-xs font-medium text-[#0d2137]">{String(rec.title ?? "—")}</td>
                      <td className="px-3 py-2 text-xs text-gray-500">{String(rec.documentType ?? "—")}</td>
                      <td className="px-3 py-2 text-xs text-gray-400">{String(rec.fileName ?? "—")}</td>
                      <td className="px-3 py-2 text-xs text-gray-400">{String(rec.createdAt ?? "—").split("T")[0]}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
