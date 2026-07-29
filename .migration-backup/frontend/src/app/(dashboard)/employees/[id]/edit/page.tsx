"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import {
  ChevronLeft,
  Loader2,
  User,
  Phone,
  CreditCard,
  Briefcase,
  MapPin,
  CalendarDays,
  Banknote,
  FileText,
  AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useBranches } from "@/hooks/useBranches";
import { toast } from "@/stores/toastStore";
import {
  ROLE_LABELS,
  POSITION_LABELS,
  type Employee,
  type UpdateEmployeeRequest,
} from "@/types/employee";

// ─── Page Component ─────────────────────────────────────────────────────────

export default function EditEmployeePage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const { data: branches } = useBranches();

  const [employee, setEmployee] = useState<Employee | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [form, setForm] = useState<UpdateEmployeeRequest>({
    fullName: "",
    phone: "",
    nationalId: "",
    position: "",
    branchId: "",
    hireDate: "",
    baseSalary: undefined,
    emergencyContact: "",
    emergencyPhone: "",
    notes: "",
  });

  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // ── Fetch employee ──

  const fetchEmployee = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const { data } = await api.get<Employee>(`/api/employees/${params.id}`);
      setEmployee(data);
      setForm({
        fullName: data.fullName,
        phone: data.phone ?? "",
        nationalId: data.nationalId ?? "",
        position: data.position ?? "",
        branchId: data.branchId ?? "",
        hireDate: data.hireDate ? data.hireDate.split("T")[0] : "",
        baseSalary: data.baseSalary,
        emergencyContact: data.emergencyContact ?? "",
        emergencyPhone: data.emergencyPhone ?? "",
        notes: data.notes ?? "",
      });
    } catch {
      setLoadError("حدث خطأ أثناء تحميل بيانات الموظف");
    } finally {
      setIsLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    fetchEmployee();
  }, [fetchEmployee]);

  const handleChange = (
    field: keyof UpdateEmployeeRequest,
    value: string | number | undefined
  ) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setFormError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.fullName?.trim()) {
      setFormError("الاسم الكامل مطلوب");
      return;
    }

    setIsSubmitting(true);
    setFormError(null);
    try {
      const payload: UpdateEmployeeRequest = {
        fullName: form.fullName.trim(),
        phone: form.phone?.trim() || undefined,
        nationalId: form.nationalId?.trim() || undefined,
        position: form.position?.trim() || undefined,
        branchId: form.branchId?.trim() || undefined,
        hireDate: form.hireDate || undefined,
        baseSalary: form.baseSalary,
        emergencyContact: form.emergencyContact?.trim() || undefined,
        emergencyPhone: form.emergencyPhone?.trim() || undefined,
        notes: form.notes?.trim() || undefined,
      };

      await api.put(`/api/employees/${params.id}`, payload);
      toast.success("تم تحديث بيانات الموظف بنجاح");
      router.push("/employees");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? ((err as { response?: { data?: { message?: string } } }).response?.data
              ?.message ?? "حدث خطأ أثناء تحديث البيانات")
          : "حدث خطأ أثناء تحديث البيانات";
      setFormError(msg);
    } finally {
      setIsSubmitting(false);
    }
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
        <span className="text-[#0d2137] font-medium">تعديل الموظف</span>
      </nav>

      {/* Form Card */}
      <form onSubmit={handleSubmit}>
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          {/* Header */}
          <div className="bg-[#0d2137] px-6 py-4">
            <h2 className="text-white font-bold text-lg">تعديل بيانات الموظف</h2>
            <div className="flex items-center gap-3 mt-1">
              <span className="text-white/60 text-sm">
                {employee.username}
              </span>
              <span
                className={cn(
                  "inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full",
                  "bg-white/20 text-white/90"
                )}
              >
                {ROLE_LABELS[employee.role] ?? employee.role}
              </span>
            </div>
          </div>

          {/* Body */}
          <div className="p-6 space-y-6">
            {/* Form Error */}
            {formError && (
              <div className="flex items-center gap-2 text-red-600 text-xs bg-red-50 rounded-lg px-3 py-2">
                <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                {formError}
              </div>
            )}

            {/* ── Section: المعلومات الشخصية ── */}
            <div>
              <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
                <User className="w-4 h-4 text-[#1a3a5c]" />
                المعلومات الشخصية
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {/* Full Name */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    الاسم الكامل <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <User className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      value={form.fullName}
                      onChange={(e) => handleChange("fullName", e.target.value)}
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                    />
                  </div>
                </div>

                {/* Phone */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    رقم الهاتف
                  </label>
                  <div className="relative">
                    <Phone className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      value={form.phone}
                      onChange={(e) => handleChange("phone", e.target.value)}
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                      dir="ltr"
                    />
                  </div>
                </div>

                {/* National ID */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    رقم الهوية
                  </label>
                  <div className="relative">
                    <CreditCard className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      value={form.nationalId}
                      onChange={(e) => handleChange("nationalId", e.target.value)}
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                      dir="ltr"
                    />
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                {/* Emergency Contact */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    جهة اتصال الطوارئ
                  </label>
                  <input
                    value={form.emergencyContact}
                    onChange={(e) => handleChange("emergencyContact", e.target.value)}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                  />
                </div>

                {/* Emergency Phone */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    هاتف الطوارئ
                  </label>
                  <input
                    value={form.emergencyPhone}
                    onChange={(e) => handleChange("emergencyPhone", e.target.value)}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                    dir="ltr"
                  />
                </div>
              </div>
            </div>

            {/* ── Section: المعلومات الوظيفية ── */}
            <div>
              <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
                <Briefcase className="w-4 h-4 text-[#1a3a5c]" />
                المعلومات الوظيفية
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {/* Position */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    المسمى الوظيفي
                  </label>
                  <select
                    value={form.position}
                    onChange={(e) => handleChange("position", e.target.value)}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                  >
                    <option value="">اختر المسمى</option>
                    {Object.entries(POSITION_LABELS).map(([value, label]) => (
                      <option key={value} value={value}>
                        {label}
                      </option>
                    ))}
                  </select>
                </div>

                {/* Branch */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    الفرع
                  </label>
                  <div className="relative">
                    <MapPin className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <select
                      value={form.branchId}
                      onChange={(e) => handleChange("branchId", e.target.value)}
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition appearance-none"
                    >
                      <option value="">اختر الفرع</option>
                      {branches?.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Hire Date */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    تاريخ التعيين
                  </label>
                  <div className="relative">
                    <CalendarDays className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="date"
                      value={form.hireDate}
                      onChange={(e) => handleChange("hireDate", e.target.value)}
                      className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                    />
                  </div>
                </div>
              </div>

              <div className="mt-4">
                <label className="block text-sm font-medium text-gray-700 mb-1.5">
                  الراتب الأساسي
                </label>
                <div className="relative max-w-xs">
                  <Banknote className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.baseSalary ?? ""}
                    onChange={(e) =>
                      handleChange("baseSalary", e.target.value ? Number(e.target.value) : undefined)
                    }
                    className="w-full border border-gray-200 rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition"
                    dir="ltr"
                  />
                </div>
              </div>
            </div>

            {/* ── Section: ملاحظات ── */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5 flex items-center gap-1.5">
                <FileText className="w-3.5 h-3.5" />
                ملاحظات
              </label>
              <textarea
                value={form.notes}
                onChange={(e) => handleChange("notes", e.target.value)}
                placeholder="ملاحظات إضافية..."
                rows={3}
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c] transition resize-none"
              />
            </div>
          </div>

          {/* Footer */}
          <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-between flex-wrap gap-3">
            <Link
              href="/employees"
              className="px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition"
            >
              إلغاء والعودة
            </Link>
            <button
              type="submit"
              disabled={isSubmitting}
              className={cn(
                "flex items-center gap-2 px-6 py-2.5 rounded-xl text-sm font-semibold transition",
                isSubmitting
                  ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                  : "bg-[#f5922e] text-white hover:opacity-90 shadow-sm"
              )}
            >
              {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSubmitting ? "جارٍ الحفظ..." : "حفظ التعديلات"}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}
