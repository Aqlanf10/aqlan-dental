"use client";
import { useState } from "react";
import Link from "next/link";
import { Mail, ArrowRight, Loader2 } from "lucide-react";
import api from "@/lib/api";

const BRAND_PRIMARY = "#1a3a5c";
const BRAND_BLUE = "#3d7ab5";
const BRAND_ORANGE = "#f5922e";
const BRAND_ORANGE_DARK = "#c47022";

export default function ForgotPasswordPage() {
  const [usernameOrEmail, setUsernameOrEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!usernameOrEmail.trim()) {
      setError("أدخل اسم المستخدم أو البريد الإلكتروني");
      return;
    }
    setLoading(true);
    setError("");
    try {
      await api.post("/api/auth/forgot-password", { usernameOrEmail: usernameOrEmail.trim() });
      setSubmitted(true);
    } catch {
      // Always show generic success to prevent user enumeration
      setSubmitted(true);
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
              style={{ background: "rgba(61,122,181,0.2)" }}
            >
              <Mail className="w-7 h-7" style={{ color: BRAND_BLUE }} />
            </div>
            <h2 className="text-xl font-bold text-white">استعادة كلمة المرور</h2>
            <p className="text-sm mt-1" style={{ color: "rgba(255,255,255,0.55)" }}>
              أدخل اسم المستخدم أو البريد الإلكتروني لإرسال تعليمات الاستعادة
            </p>
          </div>

          {submitted ? (
            <div className="space-y-4">
              <div
                className="text-[13px] px-3 py-2.5 rounded-lg flex items-start gap-2"
                style={{
                  color: "#86efac",
                  background: "rgba(134,239,172,0.1)",
                  border: "1px solid rgba(134,239,172,0.2)",
                }}
              >
                <Mail className="w-4 h-4 mt-0.5 flex-shrink-0" />
                <span>إذا كان الحساب موجوداً، سيتم إرسال تعليمات استعادة كلمة المرور إلى البريد الإلكتروني المسجل.</span>
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
                العودة لتسجيل الدخول
              </Link>
            </div>
          ) : (
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
                <label
                  className="block text-[13px] font-semibold mb-1.5"
                  style={{ color: "rgba(255,255,255,0.75)" }}
                >
                  اسم المستخدم أو البريد الإلكتروني
                </label>
                <input
                  type="text"
                  value={usernameOrEmail}
                  onChange={(e) => { setUsernameOrEmail(e.target.value); setError(""); }}
                  placeholder="admin أو admin@example.com"
                  dir="ltr"
                  className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left"
                  style={inputStyle(!!error)}
                  onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                  onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
                  autoFocus
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
                {loading ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    جارٍ الإرسال...
                  </>
                ) : (
                  "إرسال تعليمات الاستعادة"
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
