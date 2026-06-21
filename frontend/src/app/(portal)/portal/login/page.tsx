"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, User, Phone, KeyRound, ArrowRight } from "lucide-react";
import Image from "next/image";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import { cn } from "@/lib/utils";

export default function PortalLoginPage() {
  const router = useRouter();
  const { setAuth } = usePatientAuthStore();
  const [step, setStep] = useState<"login" | "forgot" | "reset">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [phoneNumber, setPhoneNumber] = useState("");
  const [code, setCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username || !password) {
      setError("أدخل اسم المستخدم وكلمة المرور");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/login", { username, password });
      setAuth(data.profile, data.accessToken, data.mustChangePassword);
      router.push("/portal");
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { message?: string } } };
      if (!axiosErr.response) {
        setError("تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت وحاول مجدداً.");
      } else {
        setError(axiosErr.response.data?.message || "اسم المستخدم أو كلمة المرور غير صحيحة");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleForgotPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!phoneNumber || phoneNumber.length < 9) {
      setError("أدخل رقم هاتف صحيح");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/forgot-password", { phoneNumber });
      setSuccess(data.message || "تم إرسال رمز التحقق عبر واتساب");
      setStep("reset");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "حدث خطأ أثناء إرسال الرمز");
    } finally {
      setLoading(false);
    }
  };

  const [resent, setResent] = useState(false);

  const handleResendCode = async () => {
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/forgot-password", { phoneNumber });
      setSuccess(data.message || "تم إعادة إرسال رمز التحقق");
      setResent(true);
      setTimeout(() => setResent(false), 30000); // Prevent spam for 30 seconds
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "حدث خطأ أثناء إعادة إرسال الرمز");
    } finally {
      setLoading(false);
    }
  };

  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code || code.length < 6) {
      setError("أدخل رمز التحقق المكون من 6 أرقام");
      return;
    }
    // SEC-11: client-side mirror of PasswordPolicy (8+ chars, digit, upper, lower).
    // The backend is the source of truth — this is UX only.
    if (!newPassword || newPassword.length < 8) {
      setError("كلمة المرور يجب أن تكون 8 أحرف على الأقل");
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
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/reset-password", {
        phoneNumber,
        code,
        newPassword,
      });
      setAuth(data.profile, data.accessToken, data.mustChangePassword);
      router.push("/portal");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "رمز التحقق غير صحيح أو منتهي الصلاحية");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-clinic-blue-50 to-white flex flex-col" style={{ direction: "rtl" }}>
      {/* Header */}
      <div className="clinic-gradient px-6 pt-12 pb-16 text-center text-white">
        <div className="w-16 h-16 bg-white/15 rounded-2xl flex items-center justify-center mx-auto mb-4 overflow-hidden backdrop-blur-sm border border-white/10">
          <Image src="/logo.png" alt="Aqlan Dental Pro" width={52} height={52} className="w-13 h-13" />
        </div>
        <h1 className="text-2xl font-extrabold">بوابة المريض</h1>
        <p className="mt-1 text-white/80 text-sm">مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
      </div>

      {/* Form Card */}
      <div className="flex-1 -mt-8 px-4">
        <div className="bg-white rounded-2xl shadow-lg p-6 max-w-md mx-auto">
          {step === "login" ? (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-clinic-blue-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <User className="w-6 h-6 text-clinic-blue" />
                </div>
                <h2 className="text-lg font-bold text-[#0d2137]">تسجيل الدخول</h2>
                <p className="text-sm text-[#64748b] mt-1">أدخل اسم المستخدم وكلمة المرور</p>
              </div>

              <form onSubmit={handleLogin} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                    اسم المستخدم
                  </label>
                  <input
                    type="text"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    placeholder="GM0001"
                    dir="ltr"
                    className={cn(
                      "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                      "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
                  <p className="mt-1 text-xs text-[#94a3b8]">رقم الملف (مثل GM0001)</p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                    كلمة المرور
                  </label>
                  <div className="relative">
                    <input
                      type={showPassword ? "text" : "password"}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder="••••••"
                      dir="ltr"
                      className={cn(
                        "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                        "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                        "border-gray-300"
                      )}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute inset-y-0 left-3 flex items-center text-[#94a3b8]"
                    >
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                <div className="text-left">
                  <button
                    type="button"
                    onClick={() => { setStep("forgot"); setError(""); setSuccess(""); }}
                    className="text-sm text-[#f5922e] hover:text-[#e07d1e] transition font-medium bg-transparent border-none cursor-pointer p-0"
                  >
                    نسيت كلمة المرور؟
                  </button>
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className={cn(
                    "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                    "bg-[#f5922e] hover:bg-[#e07d1e] active:scale-[0.98]",
                    "disabled:opacity-60 disabled:cursor-not-allowed"
                  )}
                >
                  {loading ? "جارٍ الدخول..." : "تسجيل الدخول"}
                </button>
              </form>
            </>
          ) : step === "forgot" ? (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-clinic-blue-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Phone className="w-6 h-6 text-clinic-blue" />
                </div>
                <h2 className="text-lg font-bold text-[#0d2137]">نسيت كلمة المرور</h2>
                <p className="text-sm text-[#64748b] mt-1">أدخل رقم هاتفك المسجل لإرسال رمز التحقق عبر واتساب</p>
              </div>

              <form onSubmit={handleForgotPassword} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                    رقم الهاتف
                  </label>
                  <input
                    type="tel"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="770123456"
                    dir="ltr"
                    className={cn(
                      "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                      "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
                  <p className="mt-1 text-xs text-[#94a3b8]">أدخل الرقم بدون رمز الدولة (967+)</p>
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className={cn(
                    "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                    "bg-[#f5922e] hover:bg-[#e07d1e] active:scale-[0.98]",
                    "disabled:opacity-60 disabled:cursor-not-allowed"
                  )}
                >
                  {loading ? "جارٍ الإرسال..." : "إرسال رمز التحقق"}
                </button>

                <button
                  type="button"
                  onClick={() => { setStep("login"); setError(""); setSuccess(""); }}
                  className="w-full py-2 text-sm text-[#64748b] hover:text-[#0d2137] transition flex items-center justify-center gap-1"
                >
                  <ArrowRight className="w-3 h-3" />
                  العودة إلى تسجيل الدخول
                </button>
              </form>
            </>
          ) : (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-clinic-blue-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <KeyRound className="w-6 h-6 text-clinic-blue" />
                </div>
                <h2 className="text-lg font-bold text-[#0d2137]">إعادة تعيين كلمة المرور</h2>
                <p className="text-sm text-[#64748b] mt-1">أدخل الرمز المرسل وكلمة المرور الجديدة</p>
              </div>

              {success && (
                <div className="bg-green-50 border border-green-200 text-green-700 rounded-lg p-3 text-sm mb-4">
                  {success}
                </div>
              )}

              <form onSubmit={handleResetPassword} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                    رمز التحقق
                  </label>
                  <input
                    type="text"
                    value={code}
                    onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                    placeholder="000000"
                    dir="ltr"
                    maxLength={6}
                    className={cn(
                      "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-center text-2xl tracking-[0.5em] font-mono",
                      "placeholder:text-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                    كلمة المرور الجديدة
                  </label>
                  <input
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="8 أحرف على الأقل"
                    dir="ltr"
                    className={cn(
                      "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                      "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
                  {/* SEC-11: password complexity hint (UX only — backend enforces) */}
                  <p className="mt-1 text-xs text-[#94a3b8]">
                    8+ أحرف، يحتوي على رقم وحرف كبير
                  </p>
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className={cn(
                    "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                    "bg-[#f5922e] hover:bg-[#e07d1e] active:scale-[0.98]",
                    "disabled:opacity-60 disabled:cursor-not-allowed"
                  )}
                >
                  {loading ? "جارٍ التحقق..." : "إعادة تعيين كلمة المرور"}
                </button>

                <button
                  type="button"
                  onClick={handleResendCode}
                  disabled={loading || resent}
                  className="w-full py-2 text-sm text-[#64748b] hover:text-[#0d2137] transition flex items-center justify-center gap-1 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {resent ? "تم إعادة الإرسال ✓" : "إعادة إرسال الرمز"}
                </button>
              </form>
            </>
          )}
        </div>
      </div>

      {/* Back to staff login + Footer */}
      <div className="p-4 text-center space-y-2">
        <a
          href="/login"
          className="inline-block text-sm text-clinic-blue hover:text-clinic-blue/80 transition font-medium no-underline"
        >
          ← العودة إلى دخول الطاقم
        </a>
        <div className="text-xs text-[#94a3b8]">
          © {new Date().getFullYear()} مركز د. عقلان الكامل · جميع الحقوق محفوظة
        </div>
      </div>
    </div>
  );
}
