"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Stethoscope, User, Lock, Phone, MessageCircle, KeyRound, Eye, EyeOff } from "lucide-react";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import { cn } from "@/lib/utils";

export default function PortalLoginPage() {
  const router = useRouter();
  const { setAuth } = usePatientAuthStore();
  const [mode, setMode] = useState<"login" | "request">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [showPassword, setShowPassword] = useState(false);
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
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "اسم المستخدم أو كلمة المرور غير صحيحة");
    } finally {
      setLoading(false);
    }
  };

  const handleRequestCredentials = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!phoneNumber || phoneNumber.length < 9) {
      setError("أدخل رقم هاتف صحيح");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/request-credentials", { phoneNumber });
      setSuccess(data.message || "تم إرسال بيانات الدخول عبر الواتساب");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "حدث خطأ أثناء إرسال البيانات");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-teal-50 to-white flex flex-col" style={{ direction: "rtl" }}>
      {/* Header */}
      <div className="clinic-gradient px-6 pt-12 pb-16 text-center text-white">
        <div className="w-16 h-16 bg-white/20 rounded-2xl flex items-center justify-center mx-auto mb-4">
          <Stethoscope className="w-9 h-9 text-white" />
        </div>
        <h1 className="text-2xl font-extrabold">بوابة المريض</h1>
        <p className="mt-1 text-white/80 text-sm">مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
      </div>

      {/* Form Card */}
      <div className="flex-1 -mt-8 px-4">
        <div className="bg-white rounded-2xl shadow-lg p-6 max-w-md mx-auto">
          {/* Tab Switcher */}
          <div className="flex bg-gray-100 rounded-lg p-1 mb-6">
            <button
              onClick={() => { setMode("login"); setError(""); setSuccess(""); }}
              className={cn(
                "flex-1 py-2 text-sm font-semibold rounded-md transition flex items-center justify-center gap-2",
                mode === "login" ? "bg-white text-teal-700 shadow-sm" : "text-gray-500"
              )}
            >
              <KeyRound className="w-4 h-4" />
              تسجيل الدخول
            </button>
            <button
              onClick={() => { setMode("request"); setError(""); setSuccess(""); }}
              className={cn(
                "flex-1 py-2 text-sm font-semibold rounded-md transition flex items-center justify-center gap-2",
                mode === "request" ? "bg-white text-teal-700 shadow-sm" : "text-gray-500"
              )}
            >
              <MessageCircle className="w-4 h-4" />
              طلب بيانات الدخول
            </button>
          </div>

          {mode === "login" ? (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-teal-100 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <KeyRound className="w-6 h-6 text-teal-700" />
                </div>
                <h2 className="text-lg font-bold text-gray-900">تسجيل الدخول</h2>
                <p className="text-sm text-gray-500 mt-1">أدخل اسم المستخدم وكلمة المرور</p>
              </div>

              <form onSubmit={handleLogin} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    اسم المستخدم
                  </label>
                  <div className="relative">
                    <User className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="text"
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      placeholder="رقم المريض"
                      dir="ltr"
                      className={cn(
                        "w-full pl-4 pr-10 py-3 rounded-lg border bg-white text-gray-900 text-left",
                        "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-teal-500",
                        "border-gray-300"
                      )}
                    />
                  </div>
                  <p className="mt-1 text-xs text-gray-400">اسم المستخدم هو رقم المريض الخاص بك</p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    كلمة المرور
                  </label>
                  <div className="relative">
                    <Lock className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type={showPassword ? "text" : "password"}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder="كلمة المرور"
                      dir="ltr"
                      className={cn(
                        "w-full pl-10 pr-10 py-3 rounded-lg border bg-white text-gray-900 text-left",
                        "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-teal-500",
                        "border-gray-300"
                      )}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                    >
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className={cn(
                    "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                    "clinic-gradient hover:opacity-90 active:scale-[0.98]",
                    "disabled:opacity-60 disabled:cursor-not-allowed"
                  )}
                >
                  {loading ? "جارٍ تسجيل الدخول..." : "تسجيل الدخول"}
                </button>

                <button
                  type="button"
                  onClick={() => { setMode("request"); setError(""); setSuccess(""); }}
                  className="w-full py-2 text-sm text-teal-600 hover:text-teal-700 transition flex items-center justify-center gap-1"
                >
                  <MessageCircle className="w-3.5 h-3.5" />
                  نسيت بيانات الدخول؟ اطلبها عبر الواتساب
                </button>
              </form>
            </>
          ) : (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-teal-100 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Phone className="w-6 h-6 text-teal-700" />
                </div>
                <h2 className="text-lg font-bold text-gray-900">طلب بيانات الدخول</h2>
                <p className="text-sm text-gray-500 mt-1">أدخل رقم هاتفك المسجل لدينا</p>
              </div>

              {success && (
                <div className="bg-green-50 border border-green-200 text-green-700 rounded-lg p-3 text-sm mb-4">
                  {success}
                </div>
              )}

              <form onSubmit={handleRequestCredentials} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    رقم الهاتف
                  </label>
                  <div className="relative">
                    <Phone className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                      type="tel"
                      value={phoneNumber}
                      onChange={(e) => setPhoneNumber(e.target.value)}
                      placeholder="770123456"
                      dir="ltr"
                      className={cn(
                        "w-full pl-4 pr-10 py-3 rounded-lg border bg-white text-gray-900 text-left",
                        "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-teal-500",
                        "border-gray-300"
                      )}
                    />
                  </div>
                  <p className="mt-1 text-xs text-gray-400">سيتم إرسال بيانات الدخول عبر الواتساب</p>
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className={cn(
                    "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                    "clinic-gradient hover:opacity-90 active:scale-[0.98]",
                    "disabled:opacity-60 disabled:cursor-not-allowed"
                  )}
                >
                  {loading ? "جارٍ الإرسال..." : "إرسال بيانات الدخول عبر الواتساب"}
                </button>

                <button
                  type="button"
                  onClick={() => { setMode("login"); setError(""); setSuccess(""); }}
                  className="w-full py-2 text-sm text-gray-500 hover:text-gray-700 transition flex items-center justify-center gap-1"
                >
                  <KeyRound className="w-3.5 h-3.5" />
                  العودة لتسجيل الدخول
                </button>
              </form>
            </>
          )}
        </div>
      </div>

      {/* Footer */}
      <div className="p-4 text-center text-xs text-gray-400">
        © {new Date().getFullYear()} مركز د. عقلان الكامل · جميع الحقوق محفوظة
      </div>
    </div>
  );
}
