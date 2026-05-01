"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Eye, EyeOff } from "lucide-react";
import Image from "next/image";
import { useAuthStore } from "@/stores/authStore";
import { cn } from "@/lib/utils";

const schema = z.object({
  username: z.string().min(1, "اسم المستخدم مطلوب"),
  password: z.string().min(1, "كلمة المرور مطلوبة"),
});
type FormData = z.infer<typeof schema>;

const DOCTORS = [
  { name: "د. عقلان الكامل", specialty: "أخصائي تقويم الأسنان", color: "#2563EB" },
  { name: "د. عائشة غازي", specialty: "طب أسنان عام", color: "#7C3AED" },
  { name: "د. إيمان الكامل", specialty: "طب أسنان عام", color: "#059669" },
  { name: "د. هشام القدسي", specialty: "طب أسنان عام", color: "#F97316" },
  { name: "د. خلدون البريهي", specialty: "أخصائي جراحة وجه وفكين", color: "#DC2626" },
];

export default function LoginPage() {
  const router = useRouter();
  const { login, isLoading } = useAuthStore();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: FormData) => {
    setError("");
    try {
      await login(data);
      router.push("/");
    } catch {
      setError("اسم المستخدم أو كلمة المرور غير صحيحة");
    }
  };

  return (
    <div className="min-h-screen flex" style={{ direction: "rtl" }}>
      {/* Right Panel — Clinic Identity (Navy Gradient) */}
      <div className="hidden lg:flex lg:w-1/2 clinic-gradient flex-col justify-between p-10 text-white relative overflow-hidden">
        {/* Subtle decorative elements */}
        <div className="absolute top-0 left-0 w-96 h-96 bg-white/5 rounded-full -translate-x-1/2 -translate-y-1/2" />
        <div className="absolute bottom-0 right-0 w-64 h-64 bg-blue-500/10 rounded-full translate-x-1/3 translate-y-1/3" />

        <div className="relative flex items-center gap-3">
          <div className="w-12 h-12 bg-white/15 rounded-xl flex items-center justify-center overflow-hidden backdrop-blur-sm border border-white/10">
            <Image
              src="/logo.svg"
              alt="Aqlan Dental Pro"
              width={40}
              height={40}
              className="w-10 h-10"
            />
          </div>
          <div>
            <p className="text-white/60 text-sm">نظام إدارة</p>
            <p className="font-bold text-lg leading-tight">Aqlan Dental Pro</p>
          </div>
        </div>

        <div className="relative space-y-8">
          <div>
            <h1 className="text-3xl font-extrabold leading-snug">
              مركز د. عقلان الكامل
              <br />
              لطب وتقويم الأسنان
            </h1>
            <div className="mt-4 space-y-2 text-white/70 text-sm">
              <p className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-clinic-orange flex-shrink-0" />
                تعز، اليمن — شارع التحرير الأعلى
              </p>
              <p className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-clinic-blue flex-shrink-0" />
                04-253028 · 770-245745 · 711-752823
              </p>
            </div>
          </div>

          <div className="space-y-3">
            <p className="text-white/50 text-xs font-semibold tracking-widest uppercase">
              الفريق الطبي
            </p>
            {DOCTORS.map((doc) => (
              <div key={doc.name} className="flex items-center gap-3 bg-white/5 rounded-lg px-3 py-2 backdrop-blur-sm">
                <div
                  className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0"
                  style={{ backgroundColor: doc.color + "40", border: `1px solid ${doc.color}60` }}
                >
                  {doc.name.charAt(3)}
                </div>
                <div>
                  <p className="font-semibold text-sm">{doc.name}</p>
                  <p className="text-white/50 text-xs">{doc.specialty}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <p className="relative text-white/30 text-xs">
          © {new Date().getFullYear()} Aqlan Dental Pro · جميع الحقوق محفوظة
        </p>
      </div>

      {/* Left Panel — Login Form */}
      <div className="flex-1 flex items-center justify-center bg-gray-50 p-6">
        <div className="w-full max-w-md space-y-8">
          {/* Mobile: clinic name with logo */}
          <div className="lg:hidden text-center">
            <div className="w-16 h-16 rounded-2xl flex items-center justify-center mx-auto mb-3 overflow-hidden shadow-lg">
              <Image
                src="/logo.svg"
                alt="Aqlan Dental Pro"
                width={64}
                height={64}
                className="w-16 h-16"
              />
            </div>
            <h2 className="text-xl font-extrabold text-gray-900">
              مركز د. عقلان الكامل
            </h2>
            <p className="text-sm text-gray-500">لطب وتقويم الأسنان</p>
          </div>

          <div>
            <h2 className="text-2xl font-extrabold text-gray-900">
              تسجيل الدخول
            </h2>
            <p className="mt-1 text-sm text-gray-500">
              أدخل بيانات حسابك للوصول إلى النظام
            </p>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Error message */}
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                {error}
              </div>
            )}

            {/* Username */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                اسم المستخدم
              </label>
              <input
                {...register("username")}
                type="text"
                autoComplete="username"
                placeholder="أدخل اسم المستخدم"
                className={cn(
                  "w-full px-4 py-2.5 rounded-lg border bg-white text-gray-900",
                  "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                  errors.username ? "border-red-400" : "border-gray-300"
                )}
              />
              {errors.username && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.username.message}
                </p>
              )}
            </div>

            {/* Password */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                كلمة المرور
              </label>
              <div className="relative">
                <input
                  {...register("password")}
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="أدخل كلمة المرور"
                  className={cn(
                    "w-full px-4 py-2.5 rounded-lg border bg-white text-gray-900 pe-10",
                    "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                    errors.password ? "border-red-400" : "border-gray-300"
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 end-0 flex items-center pe-3 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
              {errors.password && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.password.message}
                </p>
              )}
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={isLoading}
              className={cn(
                "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                "blue-gradient hover:opacity-90 active:scale-[0.98] shadow-md shadow-blue-500/20",
                "disabled:opacity-60 disabled:cursor-not-allowed"
              )}
            >
              {isLoading ? (
                <span className="flex items-center justify-center gap-2">
                  <svg
                    className="animate-spin h-4 w-4"
                    viewBox="0 0 24 24"
                    fill="none"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8v8H4z"
                    />
                  </svg>
                  جارٍ تسجيل الدخول...
                </span>
              ) : (
                "تسجيل الدخول"
              )}
            </button>
          </form>

          <p className="text-center text-xs text-gray-400">
            Aqlan Dental Pro v1.0 · نظام إدارة العيادة
          </p>
        </div>
      </div>
    </div>
  );
}
