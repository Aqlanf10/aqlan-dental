
import { useState } from "react";
import { useRouter } from "@/lib/nextNavCompat";
import Link from "@/lib/nextLinkCompat";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
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

// ─── Validation (FE-30: zod schema mirrors the prior ad-hoc `validate()` ──────)
// SEC-11: client-side mirror of the centralized PasswordPolicy (UX only —
// backend enforces). Arabic messages preserved verbatim from the prior
// validator.
const employeeSchema = z.object({
  fullName: z.string().trim().min(1, { message: "الاسم الكامل مطلوب" }),
  username: z
    .string()
    .trim()
    .min(3, { message: "اسم المستخدم يجب أن يكون 3 أحرف على الأقل" })
    .regex(/^[a-zA-Z0-9_]+$/, {
      message: "اسم المستخدم يجب أن يحتوي على أحرف لاتينية وأرقام وشرطة سفلية فقط",
    }),
  password: z
    .string()
    .min(8, { message: "كلمة المرور يجب أن تكون 8 أحرف على الأقل" })
    .refine((v) => /[A-Z]/.test(v), { message: "كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل" })
    .refine((v) => /[a-z]/.test(v), { message: "كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل" })
    .refine((v) => /[0-9]/.test(v), { message: "كلمة المرور يجب أن تحتوي على رقم واحد على الأقل" }),
  role: z.string().min(1, { message: "الدور مطلوب" }),
  phone: z.string().optional(),
  nationalId: z.string().optional(),
  position: z.string().optional(),
  branchId: z.string().optional(),
  hireDate: z.string().optional(),
  baseSalary: z.string().optional(),
  emergencyContact: z.string().optional(),
  emergencyPhone: z.string().optional(),
  notes: z.string().optional(),
});
type EmployeeFormData = z.infer<typeof employeeSchema>;

// ─── Page Component ─────────────────────────────────────────────────────────

export default function NewEmployeePage() {
  const router = useRouter();
  const { data: branches } = useBranches();

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EmployeeFormData>({
    resolver: zodResolver(employeeSchema),
    defaultValues: {
      fullName: "",
      phone: "",
      nationalId: "",
      position: "",
      branchId: "",
      hireDate: "",
      baseSalary: "",
      emergencyContact: "",
      emergencyPhone: "",
      notes: "",
      username: "",
      password: "",
      role: "",
    },
  });

  const onSubmit = handleSubmit(async (formData) => {
    setApiError(null);
    setIsSubmitting(true);
    try {
      const payload: CreateEmployeeRequest = {
        fullName: formData.fullName.trim(),
        phone: formData.phone?.trim() || undefined,
        nationalId: formData.nationalId?.trim() || undefined,
        position: formData.position?.trim() || undefined,
        branchId: formData.branchId?.trim() || undefined,
        hireDate: formData.hireDate || undefined,
        baseSalary: formData.baseSalary ? Number(formData.baseSalary) : undefined,
        emergencyContact: formData.emergencyContact?.trim() || undefined,
        emergencyPhone: formData.emergencyPhone?.trim() || undefined,
        notes: formData.notes?.trim() || undefined,
        username: formData.username.trim(),
        password: formData.password || undefined,
        role: formData.role,
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
  });

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/employees" className="hover:text-gray-700 transition">
          الموظفين
        </Link>
        <ChevronLeft className="w-4 h-4" />
        <span className="text-[#0d2137] font-medium">إضافة موظف جديد</span>
      </nav>

      {/* Form Card */}
      <form onSubmit={onSubmit}>
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
                      {...register("username")}
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
                    <p className="text-red-500 text-xs mt-1">{errors.username.message}</p>
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
                      {...register("password")}
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
                    <p className="text-red-500 text-xs mt-1">{errors.password.message}</p>
                  )}
                  {/* SEC-11: password complexity hint (UX only — backend enforces) */}
                  <p className="text-gray-400 text-xs mt-1">
                    8+ أحرف، يحتوي على رقم وحرف كبير
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
                      {...register("role")}
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
                    <p className="text-red-500 text-xs mt-1">{errors.role.message}</p>
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
                      {...register("fullName")}
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
                    <p className="text-red-500 text-xs mt-1">{errors.fullName.message}</p>
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
                      {...register("phone")}
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
                      {...register("nationalId")}
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
                    {...register("emergencyContact")}
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
                    {...register("emergencyPhone")}
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
                    {...register("position")}
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
                      {...register("branchId")}
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
                      {...register("hireDate")}
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
                    {...register("baseSalary")}
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
                {...register("notes")}
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
