"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Eye, EyeOff } from "lucide-react";
import Image from "next/image";
import { useAuthStore } from "@/stores/authStore";

const schema = z.object({
  username: z.string().min(1, "اسم المستخدم مطلوب"),
  password: z.string().min(1, "كلمة المرور مطلوبة"),
});
type FormData = z.infer<typeof schema>;

const DOCTORS = [
  { name: "د. عقلان الكامل", specialty: "أخصائي تقويم الأسنان", color: "#3d7ab5", initials: "عك" },
  { name: "د. عائشة غازي", specialty: "طب أسنان عام", color: "#f5922e", initials: "عغ" },
  { name: "د. إيمان الكامل", specialty: "طب أسنان عام", color: "#22c55e", initials: "إك" },
  { name: "د. هشام القدسي", specialty: "طب أسنان عام", color: "#a855f7", initials: "هق" },
  { name: "د. خلدون البريهي", specialty: "أخصائي جراحة وجه وفكين", color: "#ef4444", initials: "خب" },
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
    <div
      className="min-h-screen flex items-center justify-center relative overflow-hidden"
      style={{
        background: "linear-gradient(145deg, #0a1c30 0%, #0d2137 55%, #1a3a5c 100%)",
        direction: "rtl",
        fontFamily: "Tajawal, sans-serif",
      }}
    >
      {/* Decorative circles — matches ZIP */}
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          className="absolute rounded-full pointer-events-none"
          style={{
            border: `1px solid rgba(61,122,181,${0.08 + i * 0.04})`,
            width: 300 + i * 200,
            height: 300 + i * 200,
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
          }}
        />
      ))}

      <div className="flex flex-col items-center w-full max-w-[420px] px-6 py-8 z-10">
        {/* Logo & Header — matches ZIP */}
        <div className="text-center mb-8">
          <div
            className="w-[90px] h-[90px] rounded-3xl flex items-center justify-center mx-auto mb-4"
            style={{
              background: "rgba(255,255,255,0.07)",
              border: "1px solid rgba(255,255,255,0.12)",
            }}
          >
            <Image
              src="/logo.png"
              alt="Aqlan Dental Pro"
              width={70}
              height={70}
              className="w-[70px] h-[70px] object-contain"
            />
          </div>
          <div className="text-white text-[22px] font-extrabold mb-1">
            Aqlan Dental Pro
          </div>
          <div className="text-[13px]" style={{ color: "rgba(255,255,255,0.5)" }}>
            مركز د. عقلان الكامل لطب وتقويم الأسنان
          </div>
          <div className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.35)" }}>
            تعز — شارع التحرير الأعلى
          </div>
        </div>

        {/* Login Card — glass morphism, matches ZIP */}
        <div
          className="w-full rounded-[20px] p-8"
          style={{
            background: "rgba(255,255,255,0.05)",
            backdropFilter: "blur(12px)",
            border: "1px solid rgba(255,255,255,0.1)",
          }}
        >
          <div className="text-base font-bold text-white mb-6">
            تسجيل الدخول
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            {/* Error message */}
            {error && (
              <div className="text-[13px] mb-3" style={{ color: "#fca5a5" }}>
                {error}
              </div>
            )}

            {/* Username */}
            <div>
              <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.7)" }}>
                اسم المستخدم
              </label>
              <input
                {...register("username")}
                type="text"
                autoComplete="username"
                placeholder="admin"
                className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none"
                style={{
                  border: "1.5px solid rgba(255,255,255,0.15)",
                  background: "rgba(255,255,255,0.08)",
                  direction: "ltr",
                  textAlign: "right",
                }}
                onFocus={(e) => (e.currentTarget.style.borderColor = "rgba(61,122,181,0.7)")}
                onBlur={(e) => (e.currentTarget.style.borderColor = "rgba(255,255,255,0.15)")}
              />
              {errors.username && (
                <p className="mt-1 text-xs" style={{ color: "#fca5a5" }}>
                  {errors.username.message}
                </p>
              )}
            </div>

            {/* Password */}
            <div>
              <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.7)" }}>
                كلمة المرور
              </label>
              <div className="relative">
                <input
                  {...register("password")}
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="••••••••"
                  className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none"
                  style={{
                    border: "1.5px solid rgba(255,255,255,0.15)",
                    background: "rgba(255,255,255,0.08)",
                  }}
                  onFocus={(e) => (e.currentTarget.style.borderColor = "rgba(61,122,181,0.7)")}
                  onBlur={(e) => (e.currentTarget.style.borderColor = "rgba(255,255,255,0.15)")}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 start-3 flex items-center"
                  style={{ color: "rgba(255,255,255,0.4)" }}
                >
                  {showPassword ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </button>
              </div>
              {errors.password && (
                <p className="mt-1 text-xs" style={{ color: "#fca5a5" }}>
                  {errors.password.message}
                </p>
              )}
            </div>

            {/* Forgot password — matches ZIP orange */}
            <div className="text-left mb-1">
              <a href="#" className="text-[12px] no-underline" style={{ color: "#f5922e" }}>
                نسيت كلمة المرور؟
              </a>
            </div>

            {/* Submit button — matches ZIP */}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
              style={{
                background: isLoading ? "#2d5e8e" : "#3d7ab5",
              }}
              onMouseEnter={(e) => !isLoading && (e.currentTarget.style.background = "#2d5e8e")}
              onMouseLeave={(e) => (e.currentTarget.style.background = isLoading ? "#2d5e8e" : "#3d7ab5")}
            >
              {isLoading ? (
                <>
                  <div
                    className="w-4 h-4 rounded-full"
                    style={{
                      border: "2px solid rgba(255,255,255,0.3)",
                      borderTopColor: "#fff",
                      animation: "spin 0.7s linear infinite",
                    }}
                  />
                  جارٍ الدخول...
                </>
              ) : (
                "دخول"
              )}
            </button>
          </form>
        </div>

        {/* Doctors — matches ZIP */}
        <div className="mt-7 w-full">
          <div className="text-center mb-3 text-[12px]" style={{ color: "rgba(255,255,255,0.35)" }}>
            الفريق الطبي
          </div>
          <div className="flex gap-2 justify-center flex-wrap">
            {DOCTORS.map((doc) => (
              <div
                key={doc.name}
                title={doc.name}
                className="w-9 h-9 rounded-full flex items-center justify-center text-[12px] font-bold text-white cursor-default"
                style={{
                  backgroundColor: doc.color,
                  border: "2px solid rgba(255,255,255,0.15)",
                }}
              >
                {doc.initials}
              </div>
            ))}
          </div>
        </div>

        {/* Footer — matches ZIP */}
        <div className="mt-8 text-[11px] text-center" style={{ color: "rgba(255,255,255,0.2)" }}>
          04-253028 · 770-245745 · 711-752823
        </div>
      </div>
    </div>
  );
}
