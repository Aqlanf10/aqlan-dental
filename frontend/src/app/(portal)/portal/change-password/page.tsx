"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, KeyRound, ArrowRight, ShieldAlert } from "lucide-react";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import { cn } from "@/lib/utils";

export default function ChangePasswordPage() {
  const router = useRouter();
  const { mustChangePassword, setAuth, setMustChangePassword } = usePatientAuthStore();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const isForced = mustChangePassword;

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
      const { data } = await portalApi.post("/api/portal/auth/change-password", {
        currentPassword,
        newPassword,
      });
      setAuth(data.profile, data.accessToken, false);
      setMustChangePassword(false);
      router.push("/portal");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "فشل تغيير كلمة المرور");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50 to-white flex flex-col" style={{ direction: "rtl" }}>
      {/* Header */}
      <div className="clinic-gradient px-6 pt-12 pb-16 text-center text-white">
        <div className="w-16 h-16 bg-white/15 rounded-2xl flex items-center justify-center mx-auto mb-4 backdrop-blur-sm border border-white/10">
          {isForced ? (
            <ShieldAlert className="w-8 h-8" />
          ) : (
            <KeyRound className="w-8 h-8" />
          )}
        </div>
        <h1 className="text-2xl font-extrabold">
          {isForced ? "تغيير كلمة المرور مطلوب" : "تغيير كلمة المرور"}
        </h1>
        <p className="mt-1 text-white/80 text-sm">
          {isForced
            ? "يجب تغيير كلمة المرور المؤقتة قبل المتابعة"
            : "قم بتغيير كلمة المرور الخاصة بك"}
        </p>
      </div>

      {/* Form */}
      <div className="flex-1 -mt-8 px-4">
        <div className="bg-white rounded-2xl shadow-lg p-6 max-w-md mx-auto">
          {isForced && (
            <div className="bg-amber-50 border border-amber-200 text-amber-700 rounded-lg p-3 text-sm mb-4 flex items-start gap-2">
              <ShieldAlert className="w-4 h-4 mt-0.5 flex-shrink-0" />
              <span>يتم استخدام كلمة مرور مؤقتة. يرجى تعيين كلمة مرور جديدة للمتابعة.</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
                {error}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                كلمة المرور الحالية
              </label>
              <div className="relative">
                <input
                  type={showCurrentPassword ? "text" : "password"}
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
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
                  onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                  className="absolute inset-y-0 left-3 flex items-center text-[#94a3b8]"
                >
                  {showCurrentPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                كلمة المرور الجديدة
              </label>
              <div className="relative">
                <input
                  type={showNewPassword ? "text" : "password"}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="4 أحرف على الأقل"
                  dir="ltr"
                  className={cn(
                    "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                    "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                    "border-gray-300"
                  )}
                />
                <button
                  type="button"
                  onClick={() => setShowNewPassword(!showNewPassword)}
                  className="absolute inset-y-0 left-3 flex items-center text-[#94a3b8]"
                >
                  {showNewPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-[#0d2137] mb-1.5">
                تأكيد كلمة المرور الجديدة
              </label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="أعد كتابة كلمة المرور"
                dir="ltr"
                className={cn(
                  "w-full px-4 py-3 rounded-lg border bg-white text-[#0d2137] text-left",
                  "placeholder:text-[#94a3b8] focus:outline-none focus:ring-2 focus:ring-clinic-blue",
                  "border-gray-300"
                )}
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              className={cn(
                "w-full py-3 px-4 rounded-lg font-semibold text-white transition-all",
                "bg-[#f5922e] hover:bg-[#c47022] active:scale-[0.98]",
                "disabled:opacity-60 disabled:cursor-not-allowed"
              )}
            >
              {loading ? "جارٍ التغيير..." : "تغيير كلمة المرور"}
            </button>

            {!isForced && (
              <button
                type="button"
                onClick={() => router.back()}
                className="w-full py-2 text-sm text-[#64748b] hover:text-[#0d2137] transition flex items-center justify-center gap-1"
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
