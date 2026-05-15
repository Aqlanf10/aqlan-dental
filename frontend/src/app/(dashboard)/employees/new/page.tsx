"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  ChevronLeft,
  Loader2,
  User,
  Lock,
  Shield,
  Phone,
  CreditCard,
  Briefcase,
  MapPin,
  CalendarDays,
  Banknote,
  UserPlus,
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
  type CreateEmployeeRequest,
} from "@/types/employee";

// ─── Validation ─────────────────────────────────────────────────────────────

interface FormErrors {
  fullName?: string;
  username?: string;
  password?: string;
  role?: string;
}

function validate(data: CreateEmployeeRequest): FormErrors {
  const errors: FormErrors = {};

  if (!data.fullName.trim()) {
    errors.fullName = "الاسم الكامل مطلوب";
  }

  if (!data.username.trim()) {
    errors.username = "اسم المستخدم مطلوب";
  } else if (data.username.length < 3) {
    errors.username = "اسم المستخدم يجب أن يكون 3 أحرف على الأقل";
  } else if (!/^[a-zA-Z0-9_]+$/.test(data.username)) {
    errors.username = "اسم المستخدم يجب أن يحتوي على أحرف لاتينية وأرقام وشرطة سفلية فقط";
  }

  if (!data.password || data.password.length < 8) {
    errors.password = "كلمة المرور يجب أن تكون 8 أحرف على الأقل";
  }

  if (!data.role) {
    errors.role = "الدور مطلوب";
  }

  return errors;
}

// ─── Page Component ─────────────────────────────────────────────────────────

export default function NewEmployeePage() {
  const router = useRouter();
  const { data: branches } = useBranches();

  const [form, setForm] = useState<CreateEmployeeRequest>({
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
    username: "",
    password: "",
    role: "",
  });

  const [errors, setErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const handleChange = (
    field: keyof CreateEmployeeRequest,
    value: string | number | undefined
  ) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    // Clear error on change
    setErrors((prev) => ({ ...prev, [field]: undefined }));
    setApiError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setApiError(null);

    const validationErrors = validate(form);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: CreateEmployeeRequest = {
        fullName: form.fullName.trim(),
        phone: form.phone?.trim() || undefined,
        nationalId: form.nationalId?.trim() || undefined,
        position: form.position?.trim() || undefined,
        branchId: form.branchId?.trim() || undefined,
        hireDate: form.hireDate || undefined,
        baseSalary: form.baseSalary ? Number(form.baseSalary) : undefined,
        emergencyContact: form.emergencyContact?.trim() || undefined,
        emergencyPhone: form.emergencyPhone?.trim() || undefined,
        notes: form.notes?.trim() || undefined,
        username: form.username.trim(),
        password: form.password || undefined,
        role: form.role,
      };

      await api.post("/api/employees", payload);
      toast.success("تم إضافة الموظف بنجاح");
      router.push("/employees");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? ((err as { response?: { data?: { message?: string } } }).response?.data
              ?.message ?? "حدث خطأ أثناء إضافة الموظف")
          : "حدث خطأ أثناء إضافة الموظف";
      setApiError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6" dir="rtl">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/employees" className="hover:text-gray-700 transition">
          الموظفين
        </Link>
        <ChevronLeft className="w-4 h-4" />
        <span className="text-[#0d2137] font-medium">إضافة موظف جديد</span>
      </nav>

      {/* Form Card */}
      <form onSubmit={handleSubmit}>
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          {/* Header */}
          <div className="bg-[#0d2137] px-6 py-4">
            <h2 className="text-white font-bold text-lg">إضافة موظف جديد</h2>
            <p className="text-white/60 text-sm mt-0.5">
              أدخل بيانات الموظف الجديد لإنشاء حسابه وإضافته للنظام
            </p>
          </div>

          {/* Body */}
          <div className="p-6 space-y-6">
            {/* API Error */}
            {apiError && (
              <div className="flex items-center gap-2 text-red-600 text-xs bg-red-50 rounded-lg px-3 py-2">
                <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                {apiError}
              </div>
            )}

            {/* ── Section: معلومات الحساب ── */}
            <div>
              <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
                <User className="w-4 h-4 text-[#1a3a5c]" />
                معلومات الحساب
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {/* Username */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    اسم المستخدم <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <User className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      value={form.username}
                      onChange={(e) => handleChange("username", e.target.value)}
                      placeholder="مثال: ahmed ali"
                      className={cn(
                        "w-full border rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 transition",
                        errors.username
                          ? "border-red-300 focus:ring-red-200 focus:border-red-400"
                          : "border-gray-200 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c]"
                      )}
                      dir="ltr"
                      autoFocus
                    />
                  </div>
                  {errors.username && (
                    <p className="text-red-500 text-xs mt-1">{errors.username}</p>
                  )}
                </div>

                {/* Password */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    كلمة المرور <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <Lock className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="password"
                      value={form.password}
                      onChange={(e) => handleChange("password", e.target.value)}
                      placeholder="8 أحرف على الأقل"
                      className={cn(
                        "w-full border rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 transition",
                        errors.password
                          ? "border-red-300 focus:ring-red-200 focus:border-red-400"
                          : "border-gray-200 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c]"
                      )}
                      dir="ltr"
                    />
                  </div>
                  {errors.password && (
                    <p className="text-red-500 text-xs mt-1">{errors.password}</p>
                  )}
                  <p className="text-gray-400 text-xs mt-1">
                    كلمة المرور الافتراضية: Aqlan@2024
                  </p>
                </div>

                {/* Role */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    الدور <span className="text-red-500">*</span>
                  </label>
                  <div className="relative">
                    <Shield className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <select
                      value={form.role}
                      onChange={(e) => handleChange("role", e.target.value)}
                      className={cn(
                        "w-full border rounded-xl pr-9 pl-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 transition appearance-none",
                        errors.role
                          ? "border-red-300 focus:ring-red-200 focus:border-red-400"
                          : "border-gray-200 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c]"
                      )}
                    >
                      <option value="">اختر الدور</option>
                      {Object.entries(ROLE_LABELS).map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </div>
                  {errors.role && (
                    <p className="text-red-500 text-xs mt-1">{errors.role}</p>
                  )}
                </div>
              </div>
            </div>

            {/* ── Section: المعلومات الشخصية ── */}
            <div>
              <h3 className="text-base font-bold text-[#0d2137] mb-4 flex items-center gap-2">
                <UserPlus className="w-4 h-4 text-[#1a3a5c]" />
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
                      placeholder="الاسم الكامل بالعربية"
                      className={cn(
                        "w-full border rounded-xl pr-9 pl-3 py-2.5 text-sm focus:outline-none focus:ring-2 transition",
                        errors.fullName
                          ? "border-red-300 focus:ring-red-200 focus:border-red-400"
                          : "border-gray-200 focus:ring-[#1a3a5c]/40 focus:border-[#1a3a5c]"
                      )}
                    />
                  </div>
                  {errors.fullName && (
                    <p className="text-red-500 text-xs mt-1">{errors.fullName}</p>
                  )}
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
                      placeholder="05XXXXXXXX"
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
                      placeholder="رقم الهوية الوطنية"
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
                    placeholder="اسم جهة الاتصال"
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
                    placeholder="05XXXXXXXX"
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
                    placeholder="الراتب الشهري"
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
              {isSubmitting ? "جارٍ الحفظ..." : "إضافة الموظف"}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}


