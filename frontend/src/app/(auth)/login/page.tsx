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
  { name: "د. عقلان", color: "#3d7ab5" },
  { name: "د. عائشة", color: "#7c3aed" },
  { name: "د. إيمان", color: "#22c55e" },
  { name: "د. هشام", color: "#f5922e" },
  { name: "د. خلدون", color: "#ef4444" },
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

  const inputStyle: React.CSSProperties = {
    border: "1.5px solid rgba(255,255,255,0.15)",
    background: "rgba(255,255,255,0.08)",
    color: "#fff",
    borderRadius: 10,
    padding: "12px 14px",
    fontSize: 14,
    width: "100%",
    outline: "none",
    fontFamily: "var(--font-tajawal), sans-serif",
  };

  return (
    <div
      dir="rtl"
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(145deg, #0a1c30 0%, #0d2137 55%, #1a3a5c 100%)",
        position: "relative",
        overflow: "hidden",
        padding: 24,
      }}
    >
      {/* Decorative concentric circles */}
      <div
        style={{
          position: "absolute",
          top: "50%",
          left: "50%",
          transform: "translate(-50%, -50%)",
          pointerEvents: "none",
        }}
      >
        <div
          style={{
            width: 600,
            height: 600,
            borderRadius: "50%",
            border: "1px solid rgba(61,122,181,0.08)",
            position: "absolute",
            top: -300,
            left: -300,
          }}
        />
        <div
          style={{
            width: 440,
            height: 440,
            borderRadius: "50%",
            border: "1px solid rgba(61,122,181,0.12)",
            position: "absolute",
            top: -220,
            left: -220,
          }}
        />
        <div
          style={{
            width: 280,
            height: 280,
            borderRadius: "50%",
            border: "1px solid rgba(61,122,181,0.16)",
            position: "absolute",
            top: -140,
            left: -140,
          }}
        />
      </div>

      {/* Content */}
      <div style={{ position: "relative", zIndex: 1, width: "100%", maxWidth: 420, textAlign: "center" }}>
        {/* Logo Container */}
        <div
          style={{
            width: 90,
            height: 90,
            borderRadius: 24,
            background: "rgba(255,255,255,0.07)",
            border: "1px solid rgba(255,255,255,0.12)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            margin: "0 auto 16px",
          }}
        >
          <Image src="/logo.png" alt="Aqlan Dental Pro" width={70} height={70} priority />
        </div>

        {/* Title */}
        <h1
          style={{
            fontSize: 22,
            fontWeight: 800,
            color: "#fff",
            marginBottom: 6,
          }}
        >
          Aqlan Dental Pro
        </h1>
        <p
          style={{
            fontSize: 13,
            color: "rgba(255,255,255,0.5)",
            marginBottom: 32,
            lineHeight: 1.6,
          }}
        >
          نظام إدارة مركز د. عقلان الكامل لطب وتقويم الأسنان
        </p>

        {/* Login Card */}
        <div
          style={{
            background: "rgba(255,255,255,0.05)",
            backdropFilter: "blur(12px)",
            WebkitBackdropFilter: "blur(12px)",
            border: "1px solid rgba(255,255,255,0.1)",
            borderRadius: 20,
            padding: 32,
          }}
        >
          {/* Card Title */}
          <h2
            style={{
              fontSize: 16,
              fontWeight: 700,
              color: "#fff",
              marginBottom: 24,
              textAlign: "right",
            }}
          >
            تسجيل الدخول
          </h2>

          <form onSubmit={handleSubmit(onSubmit)} style={{ display: "flex", flexDirection: "column", gap: 18 }}>
            {/* Error message */}
            {error && (
              <div
                style={{
                  background: "rgba(239,68,68,0.15)",
                  border: "1px solid rgba(239,68,68,0.3)",
                  borderRadius: 10,
                  padding: "10px 14px",
                  color: "#fca5a5",
                  fontSize: 13,
                  textAlign: "right",
                }}
              >
                {error}
              </div>
            )}

            {/* Username */}
            <div style={{ textAlign: "right" }}>
              <label
                style={{
                  display: "block",
                  fontSize: 12,
                  fontWeight: 600,
                  color: "rgba(255,255,255,0.6)",
                  marginBottom: 6,
                }}
              >
                اسم المستخدم
              </label>
              <input
                {...register("username")}
                type="text"
                autoComplete="username"
                placeholder="أدخل اسم المستخدم"
                style={{
                  ...inputStyle,
                  borderColor: errors.username ? "rgba(252,165,165,0.6)" : undefined,
                }}
                onFocus={(e) => {
                  if (!errors.username) e.target.style.borderColor = "rgba(61,122,181,0.7)";
                }}
                onBlur={(e) => {
                  if (!errors.username) e.target.style.borderColor = "rgba(255,255,255,0.15)";
                }}
              />
              {errors.username && (
                <p style={{ marginTop: 4, fontSize: 12, color: "#fca5a5" }}>
                  {errors.username.message}
                </p>
              )}
            </div>

            {/* Password */}
            <div style={{ textAlign: "right" }}>
              <label
                style={{
                  display: "block",
                  fontSize: 12,
                  fontWeight: 600,
                  color: "rgba(255,255,255,0.6)",
                  marginBottom: 6,
                }}
              >
                كلمة المرور
              </label>
              <div style={{ position: "relative" }}>
                <input
                  {...register("password")}
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="أدخل كلمة المرور"
                  style={{
                    ...inputStyle,
                    paddingLeft: 44,
                    borderColor: errors.password ? "rgba(252,165,165,0.6)" : undefined,
                  }}
                  onFocus={(e) => {
                    if (!errors.password) e.target.style.borderColor = "rgba(61,122,181,0.7)";
                  }}
                  onBlur={(e) => {
                    if (!errors.password) e.target.style.borderColor = "rgba(255,255,255,0.15)";
                  }}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{
                    position: "absolute",
                    left: 12,
                    top: "50%",
                    transform: "translateY(-50%)",
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    color: "rgba(255,255,255,0.4)",
                    padding: 0,
                    display: "flex",
                    alignItems: "center",
                  }}
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
              {errors.password && (
                <p style={{ marginTop: 4, fontSize: 12, color: "#fca5a5" }}>
                  {errors.password.message}
                </p>
              )}
            </div>

            {/* Forgot password */}
            <div style={{ textAlign: "left" }}>
              <button
                type="button"
                onClick={() => {
                  const username = document.querySelector<HTMLInputElement>('input[autocomplete="username"]')?.value;
                  if (username) {
                    alert(`سيتم إرسال رابط إعادة تعيين كلمة المرور للمسؤول.\nتواصل مع مدير النظام لإعادة تعيين كلمة المرور.`);
                  } else {
                    alert("أدخل اسم المستخدم أولاً، ثم تواصل مع مدير النظام لإعادة تعيين كلمة المرور.");
                  }
                }}
                style={{
                  background: "none",
                  border: "none",
                  color: "#f5922e",
                  fontSize: 12,
                  cursor: "pointer",
                  padding: 0,
                  fontFamily: "var(--font-tajawal), sans-serif",
                }}
              >
                نسيت كلمة المرور؟
              </button>
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={isLoading}
              style={{
                borderRadius: 10,
                padding: "12px 16px",
                background: isLoading ? "#2d5e8e" : "#3d7ab5",
                boxShadow: isLoading ? "none" : "0 4px 16px rgba(61,122,181,0.4)",
                color: "#fff",
                fontWeight: 700,
                fontSize: 15,
                border: "none",
                cursor: isLoading ? "not-allowed" : "pointer",
                fontFamily: "var(--font-tajawal), sans-serif",
                transition: "background 0.2s",
                width: "100%",
                opacity: isLoading ? 0.8 : 1,
              }}
            >
              {isLoading ? (
                <span style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8 }}>
                  <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  جارٍ تسجيل الدخول...
                </span>
              ) : (
                "تسجيل الدخول"
              )}
            </button>
          </form>
        </div>

        {/* Doctor avatars row */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8, marginTop: 24 }}>
          {DOCTORS.map((doc) => (
            <div
              key={doc.name}
              title={doc.name}
              style={{
                width: 36,
                height: 36,
                borderRadius: 99,
                border: "2px solid rgba(255,255,255,0.15)",
                background: doc.color,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "#fff",
                fontSize: 12,
                fontWeight: 700,
                flexShrink: 0,
              }}
            >
              {doc.name.charAt(3)}
            </div>
          ))}
        </div>

        {/* Footer */}
        <p style={{ marginTop: 20, color: "rgba(255,255,255,0.2)", fontSize: 11 }}>
          © {new Date().getFullYear()} Aqlan Dental Pro · جميع الحقوق محفوظة
        </p>
      </div>
    </div>
  );
}
