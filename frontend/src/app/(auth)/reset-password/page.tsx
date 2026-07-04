"use client";
import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { KeyRound, Eye, EyeOff, ArrowRight, Loader2, ShieldAlert } from "lucide-react";
import api from "@/lib/api";

const BRAND_PRIMARY = "#1a3a5c";
const BRAND_BLUE = "#3d7ab5";
const BRAND_ORANGE = "#f5922e";
const BRAND_ORANGE_DARK = "#c47022";

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <div
          className="min-h-screen flex items-center justify-center"
          style={{
            background: `linear-gradient(135deg, ${BRAND_PRIMARY} 0%, #244b73 55%, #2e4a6f 100%)`,
            direction: "rtl",
            fontFamily: "Tajawal, sans-serif",
          }}
        >
          <Loader2 className="w-8 h-8 animate-spin" style={{ color: "rgba(255,255,255,0.5)" }} />
        </div>
      }
    >
      <ResetPasswordContent />
    </Suspense>
  );
}

function ResetPasswordContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token");

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");

    // SEC-11: client-side mirror of the centralized PasswordPolicy.
    // The backend is the source of truth — this is UX only. Special
    // characters are NOT required (user-friendly for Arabic patients).
    if (!newPassword || newPassword.length < 8) {
      setError("كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل");
      return;
    }
    if (!/[A-Z]/.test(newPassword)) {
      setError("كلمة المرور يجب أن تحتوي على حرف كبير واحد على الأقل");
      return;
    }
    if (!/[a-z]/.test(newPassword)) {
      setError("كلمة المرور يجب أن تحتوي على حرف صغير واحد على الأقل");
      return;
    }
    if (!/[0-9]/.test(newPassword)) {
      setError("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("كلمة المرور غير متطابقة");
      return;
    }

    setLoading(true);
    try {
      await api.post("/api/auth/reset-password", {
        token,
        newPassword,
        confirmPassword,
      });
      setSuccess(true);
      // Redirect to login after 3 seconds
      setTimeout(() => {
        router.push("/login");
      }, 3000);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "فشل إعادة تعيين كلمة المرور. الرابط قد يكون منتهي الصلاحية.");
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

  // No token → invalid link
  if (!token) {
    return (
      <div
        className="min-h-screen flex items-center justify-center relative overflow-hidden"
        style={{
          background: `linear-gradient(135deg, ${BRAND_PRIMARY} 0%, #244b73 55%, #2e4a6f 100%)`,
          direction: "rtl",
          fontFamily: "Tajawal, sans-serif",
        }}
      >
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
            <div className="text-center mb-6">
              <div
                className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
                style={{ background: "rgba(252,165,165,0.15)" }}
              >
                <ShieldAlert className="w-7 h-7" style={{ color: "#fca5a5" }} />
              </div>
              <h2 className="text-xl font-bold text-white">رابط غير صالح</h2>
              <p className="text-sm mt-1" style={{ color: "rgba(255,255,255,0.55)" }}>
                رابط إعادة التعيين غير صالح أو منتهي الصلاحية
              </p>
            </div>
            <Link
              href="/forgot-password"
              className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
              style={{
                background: BRAND_ORANGE,
                boxShadow: "0 4px 12px rgba(245,146,46,0.3)",
              }}
            >
              طلب رابط جديد
            </Link>
          </div>
        </div>
      </div>
    );
  }

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
              style={{ background: "rgba(61,122,181,0.2)" }}
            >
              <KeyRound className="w-7 h-7" style={{ color: BRAND_BLUE }} />
            </div>
            <h2 className="text-xl font-bold text-white">إعادة تعيين كلمة المرور</h2>
            <p className="text-sm mt-1" style={{ color: "rgba(255,255,255,0.55)" }}>
              أدخل كلمة المرور الجديدة (8 أحرف على الأقل، حرف كبير + صغير + رقم)
            </p>
          </div>

          {success ? (
            <div className="space-y-4">
              <div
                className="text-[13px] px-3 py-2.5 rounded-lg"
                style={{
                  color: "#86efac",
                  background: "rgba(134,239,172,0.1)",
                  border: "1px solid rgba(134,239,172,0.2)",
                }}
              >
                تم إعادة تعيين كلمة المرور بنجاح. سيتم تحويلك إلى صفحة تسجيل الدخول...
              </div>
              <Link
                href="/login"
                className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
                style={{
                  background: BRAND_ORANGE,
                  boxShadow: "0 4px 12px rgba(245,146,46,0.3)",
                }}
              >
                <ArrowRight className="w-4 h-4" />
                الذهاب لتسجيل الدخول
              </Link>
            </div>
          ) : (
            <form method="post" onSubmit={handleSubmit} className="space-y-4">
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
                <label
                  className="block text-[13px] font-semibold mb-1.5"
                  style={{ color: "rgba(255,255,255,0.75)" }}
                >
                  كلمة المرور الجديدة
                </label>
                <div className="relative">
                  <input
                    type={showNewPassword ? "text" : "password"}
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="8 أحرف على الأقل - حرف كبير + صغير + رقم"
                    dir="ltr"
                    className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                    style={inputStyle(!!error)}
                    onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                    onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
                    autoFocus
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
                {/* Password strength indicator */}
                {newPassword.length > 0 && (
                  <div className="mt-2 space-y-1.5">
                    <div className="flex gap-1">
                      {[0, 1, 2, 3].map((i) => (
                        <div
                          key={i}
                          className="h-1 flex-1 rounded-full transition-colors"
                          style={{
                            background: i < (
                              newPassword.length >= 8 && /[A-Z]/.test(newPassword) && /[a-z]/.test(newPassword) && /[0-9]/.test(newPassword) && /[^A-Za-z0-9]/.test(newPassword) ? 4
                              : newPassword.length >= 8 && ((/[A-Z]/.test(newPassword) && /[a-z]/.test(newPassword)) || (/[0-9]/.test(newPassword) && /[^A-Za-z0-9]/.test(newPassword))) ? 3
                              : newPassword.length >= 6 ? 2
                              : 1
                            ) ? (
                              i < 1 ? "#ef4444"
                              : i < 2 ? "#f59e0b"
                              : i < 3 ? "#3b82f6"
                              : "#22c55e"
                            ) : "rgba(255,255,255,0.1)",
                          }}
                        />
                      ))}
                    </div>
                    <p className="text-[11px]" style={{ color: "rgba(255,255,255,0.45)" }}>
                      {newPassword.length >= 8 && /[A-Z]/.test(newPassword) && /[a-z]/.test(newPassword) && /[0-9]/.test(newPassword) && /[^A-Za-z0-9]/.test(newPassword)
                        ? "قوة كلمة المرور: ممتازة ✓"
                        : newPassword.length >= 8 && ((/[A-Z]/.test(newPassword) && /[a-z]/.test(newPassword)) || (/[0-9]/.test(newPassword) && /[^A-Za-z0-9]/.test(newPassword)))
                        ? "قوة كلمة المرور: جيدة"
                        : newPassword.length >= 6
                        ? "قوة كلمة المرور: متوسطة"
                        : "قوة كلمة المرور: ضعيفة"}
                    </p>
                  </div>
                )}
              </div>

              <div>
                <label
                  className="block text-[13px] font-semibold mb-1.5"
                  style={{ color: "rgba(255,255,255,0.75)" }}
                >
                  تأكيد كلمة المرور
                </label>
                <div className="relative">
                  <input
                    type={showConfirmPassword ? "text" : "password"}
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder="أعد كتابة كلمة المرور"
                    dir="ltr"
                    className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                    style={inputStyle(!!error)}
                    onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                    onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    className="absolute inset-y-0 start-3 flex items-center"
                    style={{ color: "rgba(255,255,255,0.5)" }}
                  >
                    {showConfirmPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
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
                {loading ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    جارٍ إعادة التعيين...
                  </>
                ) : (
                  "إعادة تعيين كلمة المرور"
                )}
              </button>

              <Link
                href="/login"
                className="w-full py-2 text-[12px] transition flex items-center justify-center gap-1 bg-transparent border-none cursor-pointer"
                style={{ color: "rgba(255,255,255,0.5)" }}
              >
                <ArrowRight className="w-3 h-3" />
                العودة لتسجيل الدخول
              </Link>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
