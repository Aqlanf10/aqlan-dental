"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, KeyRound, ShieldAlert, ArrowRight } from "lucide-react";
import api from "@/lib/api";
import { useAuthStore } from "@/stores/authStore";

const BRAND_PRIMARY = "#1a3a5c";
const BRAND_BLUE = "#3d7ab5";
const BRAND_ORANGE = "#f5922e";
const BRAND_ORANGE_DARK = "#c47022";

export default function StaffChangePasswordPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const isForced = user?.mustChangePassword;

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");

    if (!currentPassword) {
      setError("أدخل كلمة المرور الحالية");
      return;
    }
    if (!newPassword || newPassword.length < 4) {
      setError("كلمة المرور الجديدة يجب أن تكون 4 أحرف على الأقل");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("كلمة المرور الجديدة غير متطابقة");
      return;
    }
    if (currentPassword === newPassword) {
      setError("كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية");
      return;
    }

    setLoading(true);
    try {
      const { data } = await api.post<{ message: string; accessToken?: string }>("/api/auth/change-password", {
        currentPassword,
        newPassword,
      });
      // LOGIN FIX: If the backend returns a new access token (with mustChangePassword=false
      // claim), use it immediately. Without this, the old JWT still has mustChangePassword=true
      // and MustChangePasswordMiddleware blocks ALL subsequent API calls.
      if (data.accessToken) {
        localStorage.setItem("access_token", data.accessToken);
      }
      // Update user state to clear mustChangePassword flag
      await useAuthStore.getState().fetchMe();
      router.push("/");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "فشل تغيير كلمة المرور");
    } finally {
      setLoading(false);
    }
  };

  const glassCardStyle: React.CSSProperties = {
    background: "rgba(255,255,255,0.06)",
    backdropFilter: "blur(12px)",
    border: "1px solid rgba(255,255,255,0.12)",
  };

  const inputStyle = (hasError: boolean): React.CSSProperties => ({
    border: hasError ? "1.5px solid rgba(252,165,165,0.7)" : "1.5px solid rgba(255,255,255,0.18)",
    background: "rgba(255,255,255,0.08)",
  });

  const inputFocusStyle = (hasError: boolean) => ({
    borderColor: hasError ? "rgba(252,165,165,0.7)" : "rgba(245,146,46,0.7)",
  });

  return (
    <div
      className="min-h-screen flex items-center justify-center relative overflow-hidden"
      style={{
        background: `linear-gradient(135deg, ${BRAND_PRIMARY} 0%, #244b73 55%, #2e4a6f 100%)`,
        direction: "rtl",
        fontFamily: "Tajawal, sans-serif",
      }}
    >
      {/* Decorative circles */}
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          className="absolute rounded-full pointer-events-none"
          style={{
            border: `1px solid rgba(61,122,181,${0.1 + i * 0.05})`,
            width: 300 + i * 200,
            height: 300 + i * 200,
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
          }}
        />
      ))}

      <div className="w-full max-w-md px-4 py-8 z-10">
        <div className="rounded-[20px] p-7" style={glassCardStyle}>
          {/* Header */}
          <div className="text-center mb-6">
            <div
              className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
              style={{ background: "rgba(245,146,46,0.2)" }}
            >
              {isForced ? (
                <ShieldAlert className="w-7 h-7" style={{ color: BRAND_ORANGE }} />
              ) : (
                <KeyRound className="w-7 h-7" style={{ color: BRAND_BLUE }} />
              )}
            </div>
            <h2 className="text-xl font-bold text-white">
              {isForced ? "تغيير كلمة المرور مطلوب" : "تغيير كلمة المرور"}
            </h2>
            <p className="text-sm mt-1" style={{ color: "rgba(255,255,255,0.55)" }}>
              {isForced
                ? "يجب تغيير كلمة المرور المؤقتة قبل المتابعة"
                : "قم بتغيير كلمة المرور الخاصة بك"}
            </p>
          </div>

          {isForced && (
            <div
              className="text-[13px] px-3 py-2.5 rounded-lg mb-4 flex items-start gap-2"
              style={{
                color: "#fcd34d",
                background: "rgba(252,211,77,0.1)",
                border: "1px solid rgba(252,211,77,0.2)",
              }}
            >
              <ShieldAlert className="w-4 h-4 mt-0.5 flex-shrink-0" />
              <span>يتم استخدام كلمة مرور مؤقتة. يرجى تعيين كلمة مرور جديدة للمتابعة.</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div
                className="text-[13px] px-3 py-2 rounded-lg"
                style={{
                  color: "#fca5a5",
                  background: "rgba(252,165,165,0.1)",
                  border: "1px solid rgba(252,165,165,0.2)",
                }}
              >
                {error}
              </div>
            )}

            <div>
              <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
                كلمة المرور الحالية
              </label>
              <div className="relative">
                <input
                  type={showCurrentPassword ? "text" : "password"}
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  placeholder="••••••"
                  dir="ltr"
                  className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                  style={inputStyle(!!error)}
                  onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                  onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
                />
                <button
                  type="button"
                  onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                  className="absolute inset-y-0 start-3 flex items-center"
                  style={{ color: "rgba(255,255,255,0.5)" }}
                >
                  {showCurrentPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
                كلمة المرور الجديدة
              </label>
              <div className="relative">
                <input
                  type={showNewPassword ? "text" : "password"}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="4 أحرف على الأقل"
                  dir="ltr"
                  className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                  style={inputStyle(!!error)}
                  onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                  onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
                />
                <button
                  type="button"
                  onClick={() => setShowNewPassword(!showNewPassword)}
                  className="absolute inset-y-0 start-3 flex items-center"
                  style={{ color: "rgba(255,255,255,0.5)" }}
                >
                  {showNewPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
                تأكيد كلمة المرور الجديدة
              </label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="أعد كتابة كلمة المرور"
                dir="ltr"
                className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                style={inputStyle(!!error)}
                onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
              style={{
                background: loading ? BRAND_ORANGE_DARK : BRAND_ORANGE,
                boxShadow: "0 4px 12px rgba(245,146,46,0.3)",
              }}
              onMouseEnter={(e) => !loading && (e.currentTarget.style.background = BRAND_ORANGE_DARK)}
              onMouseLeave={(e) => (e.currentTarget.style.background = loading ? BRAND_ORANGE_DARK : BRAND_ORANGE)}
            >
              {loading ? "جارٍ التغيير..." : "تغيير كلمة المرور"}
            </button>

            {!isForced && (
              <button
                type="button"
                onClick={() => router.back()}
                className="w-full py-2 text-[12px] transition flex items-center justify-center gap-1 bg-transparent border-none cursor-pointer"
                style={{ color: "rgba(255,255,255,0.5)" }}
              >
                <ArrowRight className="w-3 h-3" />
                إلغاء
              </button>
            )}
          </form>
        </div>
      </div>
    </div>
  );
}
