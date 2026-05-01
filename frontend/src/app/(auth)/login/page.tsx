"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Eye, EyeOff, Phone, Shield, ArrowRight, Loader2 } from "lucide-react";
import Image from "next/image";
import { useAuthStore } from "@/stores/authStore";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";

// ─── Staff Login Schema ──────────────────────────────────────────────────────
const staffSchema = z.object({
  username: z.string().min(1, "اسم المستخدم مطلوب"),
  password: z.string().min(1, "كلمة المرور مطلوبة"),
});
type StaffFormData = z.infer<typeof staffSchema>;

// ─── Doctor Avatars ──────────────────────────────────────────────────────────
const DOCTORS = [
  { name: "د. عقلان الكامل", specialty: "أخصائي تقويم الأسنان", color: "#3d7ab5", initials: "عك" },
  { name: "د. عائشة غازي", specialty: "طب أسنان عام", color: "#f5922e", initials: "عغ" },
  { name: "د. إيمان الكامل", specialty: "طب أسنان عام", color: "#22c55e", initials: "إك" },
  { name: "د. هشام القدسي", specialty: "طب أسنان عام", color: "#a855f7", initials: "هق" },
  { name: "د. خلدون البريهي", specialty: "أخصائي جراحة وجه وفكين", color: "#ef4444", initials: "خب" },
];

// ─── Shared Styles ───────────────────────────────────────────────────────────
const glassCardStyle: React.CSSProperties = {
  background: "rgba(255,255,255,0.05)",
  backdropFilter: "blur(12px)",
  border: "1px solid rgba(255,255,255,0.1)",
};

const inputStyle = (hasError: boolean): React.CSSProperties => ({
  border: hasError ? "1.5px solid rgba(252,165,165,0.7)" : "1.5px solid rgba(255,255,255,0.15)",
  background: "rgba(255,255,255,0.08)",
});

const inputFocusStyle = (hasError: boolean) => ({
  borderColor: hasError ? "rgba(252,165,165,0.7)" : "rgba(61,122,181,0.7)",
});

// ─── Main Component ──────────────────────────────────────────────────────────
export default function LoginPage() {
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

      <div className="w-full max-w-[900px] px-4 py-8 z-10">
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

        {/* Two Panels */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5 mb-7">
          {/* Panel 1: Staff Login */}
          <StaffLoginPanel />

          {/* Panel 2: Patient Portal Login */}
          <PatientLoginPanel />
        </div>

        {/* Doctors — matches ZIP */}
        <div className="w-full">
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

// ─── Staff Login Panel ───────────────────────────────────────────────────────
function StaffLoginPanel() {
  const router = useRouter();
  const { login, isLoading } = useAuthStore();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StaffFormData>({ resolver: zodResolver(staffSchema) });

  const onSubmit = async (data: StaffFormData) => {
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
      className="rounded-[20px] p-7 flex flex-col"
      style={glassCardStyle}
    >
      {/* Panel header */}
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-1">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(61,122,181,0.2)" }}
          >
            <svg className="w-4 h-4 text-[#3d7ab5]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>
          <h2 className="text-lg font-bold text-white">دخول الطاقم</h2>
        </div>
        <p className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.45)" }}>
          للأطباء، الاستقبال، الإدارة، والمحاسبة
        </p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 flex-1">
        {/* Error message */}
        {error && (
          <div className="text-[13px] mb-1" style={{ color: "#fca5a5" }}>
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
            className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors"
            style={inputStyle(!!errors.username)}
            onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!errors.username))}
            onBlur={(e) => (e.currentTarget.style.borderColor = errors.username ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.15)")}
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
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors"
              style={inputStyle(!!errors.password)}
              onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!errors.password))}
              onBlur={(e) => (e.currentTarget.style.borderColor = errors.password ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.15)")}
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute inset-y-0 start-3 flex items-center"
              style={{ color: "rgba(255,255,255,0.4)" }}
            >
              {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
            </button>
          </div>
          {errors.password && (
            <p className="mt-1 text-xs" style={{ color: "#fca5a5" }}>
              {errors.password.message}
            </p>
          )}
        </div>

        {/* Forgot password */}
        <div className="text-left">
          <a href="#" className="text-[12px] no-underline" style={{ color: "#f5922e" }}>
            نسيت كلمة المرور؟
          </a>
        </div>

        {/* Submit button */}
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
              <Loader2 className="w-4 h-4 animate-spin" />
              جارٍ الدخول...
            </>
          ) : (
            "دخول إلى لوحة التحكم"
          )}
        </button>
      </form>
    </div>
  );
}

// ─── Patient Login Panel (Option B: embedded flow) ───────────────────────────
function PatientLoginPanel() {
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
    <div
      className="rounded-[20px] p-7 flex flex-col"
      style={{
        background: "rgba(245,146,46,0.04)",
        backdropFilter: "blur(12px)",
        border: "1px solid rgba(245,146,46,0.12)",
      }}
    >
      {/* Panel header */}
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-1">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(245,146,46,0.15)" }}
          >
            <Phone className="w-4 h-4 text-[#f5922e]" />
          </div>
          <h2 className="text-lg font-bold text-white">دخول المرضى</h2>
        </div>
        <p className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.45)" }}>
          لمتابعة المواعيد، العلاجات، الوصفات، والمدفوعات
        </p>
      </div>

      {step === "phone" ? (
        <form onSubmit={handleSendCode} className="space-y-4 flex-1">
          {/* Error */}
          {error && (
            <div className="text-[13px]" style={{ color: "#fca5a5" }}>
              {error}
            </div>
          )}

          {/* Phone input */}
          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.7)" }}>
              رقم الهاتف
            </label>
            <input
              type="tel"
              value={phoneNumber}
              onChange={(e) => { setPhoneNumber(e.target.value); setError(""); }}
              placeholder="770123456"
              dir="ltr"
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left placeholder:text-white/25"
              style={inputStyle(!!error)}
              onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
              onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.15)")}
            />
            <p className="mt-1 text-[11px]" style={{ color: "rgba(255,255,255,0.3)" }}>
              أدخل الرقم بدون رمز الدولة (967+)
            </p>
          </div>

          {/* Send code button */}
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
            style={{
              background: loading ? "#c47022" : "#f5922e",
            }}
            onMouseEnter={(e) => !loading && (e.currentTarget.style.background = "#c47022")}
            onMouseLeave={(e) => (e.currentTarget.style.background = loading ? "#c47022" : "#f5922e")}
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                جارٍ الإرسال...
              </>
            ) : (
              "إرسال رمز التحقق"
            )}
          </button>
        </form>
      ) : (
        <form onSubmit={handleVerify} className="space-y-4 flex-1">
          {/* Success message */}
          {success && (
            <div className="text-[13px]" style={{ color: "#86efac" }}>
              {success}
            </div>
          )}

          {/* Error */}
          {error && (
            <div className="text-[13px]" style={{ color: "#fca5a5" }}>
              {error}
            </div>
          )}

          {/* Verify header */}
          <div className="text-center">
            <div
              className="w-10 h-10 rounded-lg flex items-center justify-center mx-auto mb-2"
              style={{ background: "rgba(245,146,46,0.15)" }}
            >
              <Shield className="w-5 h-5 text-[#f5922e]" />
            </div>
            <p className="text-[12px]" style={{ color: "rgba(255,255,255,0.5)" }}>
              أدخل الرمز المرسل إلى <span className="font-mono text-white/70" dir="ltr">{phoneNumber}</span>
            </p>
          </div>

          {/* Code input */}
          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.7)" }}>
              رمز التحقق
            </label>
            <input
              type="text"
              value={code}
              onChange={(e) => { setCode(e.target.value.replace(/\D/g, "").slice(0, 6)); setError(""); }}
              placeholder="000000"
              dir="ltr"
              maxLength={6}
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-center tracking-[0.5em] font-mono placeholder:text-white/20"
              style={inputStyle(!!error)}
              onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
              onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.15)")}
              autoFocus
            />
          </div>

          {/* Verify button */}
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
            style={{
              background: loading ? "#c47022" : "#f5922e",
            }}
            onMouseEnter={(e) => !loading && (e.currentTarget.style.background = "#c47022")}
            onMouseLeave={(e) => (e.currentTarget.style.background = loading ? "#c47022" : "#f5922e")}
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                جارٍ التحقق...
              </>
            ) : (
              "دخول إلى بوابة المريض"
            )}
          </button>

          {/* Change phone */}
          <button
            type="button"
            onClick={() => { setStep("phone"); setError(""); setSuccess(""); setCode(""); }}
            className="w-full py-2 text-[12px] transition flex items-center justify-center gap-1"
            style={{ color: "rgba(255,255,255,0.4)" }}
          >
            <ArrowRight className="w-3 h-3" />
            تغيير رقم الهاتف
          </button>
        </form>
      )}
    </div>
  );
}
