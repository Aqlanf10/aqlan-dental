"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Phone, Shield, ArrowRight } from "lucide-react";
import Image from "next/image";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import { cn } from "@/lib/utils";

export default function PortalLoginPage() {
  const router = useRouter();
  const { setAuth, setPhoneNumber: savePhoneToStore, phoneNumber: storedPhone } = usePatientAuthStore();
  const [step, setStep] = useState<"phone" | "verify">("phone");
  const [phoneNumber, setPhoneNumber] = useState(storedPhone || "");
  const [code, setCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const handleSendCode = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!phoneNumber || phoneNumber.length < 9) {
      setError("أدخل رقم هاتف صحيح");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/send-code", { phoneNumber });
      setSuccess(data.message || "تم إرسال رمز التحقق");
      savePhoneToStore(phoneNumber);
      setStep("verify");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "حدث خطأ أثناء إرسال الرمز");
    } finally {
      setLoading(false);
    }
  };

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code || code.length < 6) {
      setError("أدخل رمز التحقق المكون من 6 أرقام");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const { data } = await portalApi.post("/api/portal/auth/verify", {
        phoneNumber,
        code,
      });
      setAuth(data.profile, data.accessToken);
      router.push("/portal");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "رمز التحقق غير صحيح");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-clinic-blue-50 to-white flex flex-col" style={{ direction: "rtl" }}>
      {/* Header */}
      <div className="clinic-gradient px-6 pt-12 pb-16 text-center text-white">
        <div className="w-16 h-16 bg-white/15 rounded-2xl flex items-center justify-center mx-auto mb-4 overflow-hidden backdrop-blur-sm border border-white/10">
          <Image src="/logo.svg" alt="Aqlan Dental Pro" width={52} height={52} className="w-13 h-13" />
        </div>
        <h1 className="text-2xl font-extrabold">بوابة المريض</h1>
        <p className="mt-1 text-white/80 text-sm">مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
      </div>

      {/* Form Card */}
      <div className="flex-1 -mt-8 px-4">
        <div className="bg-white rounded-2xl shadow-lg p-6 max-w-md mx-auto">
          {step === "phone" ? (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-clinic-blue-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Phone className="w-6 h-6 text-clinic-blue" />
                </div>
                <h2 className="text-lg font-bold text-gray-900">تسجيل الدخول</h2>
                <p className="text-sm text-gray-500 mt-1">أدخل رقم هاتفك المسجل عندنا</p>
              </div>

              <form onSubmit={handleSendCode} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
                    رقم الهاتف
                  </label>
                  <input
                    type="tel"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="770123456"
                    dir="ltr"
                    className={cn(
                      "w-full px-4 py-3 rounded-lg border bg-white text-gray-900 text-left",
                      "placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
                  <p className="mt-1 text-xs text-gray-400">أدخل الرقم بدون رمز الدولة (967+)</p>
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
                  {loading ? "جارٍ الإرسال..." : "إرسال رمز التحقق"}
                </button>
              </form>
            </>
          ) : (
            <>
              <div className="text-center mb-6">
                <div className="w-12 h-12 bg-clinic-blue-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Shield className="w-6 h-6 text-clinic-blue" />
                </div>
                <h2 className="text-lg font-bold text-gray-900">رمز التحقق</h2>
                <p className="text-sm text-gray-500 mt-1">
                  أدخل الرمز المرسل إلى <span className="font-mono text-gray-700" dir="ltr">{phoneNumber}</span>
                </p>
              </div>

              {success && (
                <div className="bg-green-50 border border-green-200 text-green-700 rounded-lg p-3 text-sm mb-4">
                  {success}
                </div>
              )}

              <form onSubmit={handleVerify} className="space-y-4">
                {error && (
                  <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                    {error}
                  </div>
                )}

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">
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
                      "w-full px-4 py-3 rounded-lg border bg-white text-gray-900 text-center text-2xl tracking-[0.5em] font-mono",
                      "placeholder:text-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                      "border-gray-300"
                    )}
                  />
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
                  {loading ? "جارٍ التحقق..." : "تسجيل الدخول"}
                </button>

                <button
                  type="button"
                  onClick={() => { setStep("phone"); setError(""); setSuccess(""); }}
                  className="w-full py-2 text-sm text-gray-500 hover:text-gray-700 transition flex items-center justify-center gap-1"
                >
                  <ArrowRight className="w-3 h-3" />
                  تغيير رقم الهاتف
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
