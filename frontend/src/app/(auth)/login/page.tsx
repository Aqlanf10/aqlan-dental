"use client";
import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Eye, EyeOff, User as UserIcon, ArrowRight, Loader2, Phone, KeyRound, Globe } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useAuthStore } from "@/stores/authStore";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";

/* ─── Brand colors (Aqlan Dental Pro) ──────────────────────────────────────── */
const BRAND_PRIMARY = "#1a3a5c";       // dark sky — background base
const BRAND_BLUE = "#3d7ab5";          // sky blue accent
const BRAND_ORANGE = "#f5922e";        // orange CTA
const BRAND_ORANGE_DARK = "#c47022";   // orange hover

// ─── Staff Login Schema ──────────────────────────────────────────────────────
const staffSchema = z.object({
  username: z.string().min(1, "اسم المستخدم مطلوب"),
  password: z.string().min(1, "كلمة المرور مطلوبة"),
});
type StaffFormData = z.infer<typeof staffSchema>;

// ─── Doctor Avatars (uses brand palette only) ────────────────────────────────
const DOCTORS = [
  { name: "د. عقلان الكامل", specialty: "أخصائي تقويم الأسنان", color: BRAND_ORANGE, initials: "عك" },
  { name: "د. عائشة غازي", specialty: "طب أسنان عام", color: BRAND_BLUE, initials: "عغ" },
  { name: "د. إيمان الكامل", specialty: "طب أسنان عام", color: BRAND_BLUE, initials: "إك" },
  { name: "د. هشام القدسي", specialty: "طب أسنان عام", color: BRAND_BLUE, initials: "هق" },
  { name: "د. خلدون البريهي", specialty: "أخصائي جراحة وجه وفكين", color: BRAND_BLUE, initials: "خب" },
];

// ─── Shared Styles ───────────────────────────────────────────────────────────
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

// ─── Main Component ──────────────────────────────────────────────────────────
export default function LoginPage() {
  return (
    <div
      className="min-h-screen flex items-center justify-center relative overflow-hidden"
      style={{
        background: `linear-gradient(135deg, ${BRAND_PRIMARY} 0%, #244b73 55%, #2e4a6f 100%)`,
        direction: "rtl",
        fontFamily: "Tajawal, sans-serif",
      }}
    >
      {/* Decorative circles — sky-blue + orange tints */}
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
      {/* Orange accent ring */}
      <div
        className="absolute rounded-full pointer-events-none"
        style={{
          border: `1px solid rgba(245,146,46,0.15)`,
          width: 580,
          height: 580,
          top: "50%",
          left: "50%",
          transform: "translate(-50%, -50%)",
        }}
      />

      <div className="w-full max-w-[900px] px-4 py-8 z-10">
        {/* Logo & Header */}
        <div className="text-center mb-8">
          <div
            className="w-[96px] h-[96px] rounded-3xl flex items-center justify-center mx-auto mb-4"
            style={{
              background: "#fff",
              boxShadow: "0 8px 32px rgba(0,0,0,0.18), 0 0 0 1px rgba(255,255,255,0.08)",
            }}
          >
            <Image
              src="/logo.png"
              alt="Aqlan Dental Pro"
              width={76}
              height={76}
              className="w-[76px] h-[76px] object-contain"
            />
          </div>
          <div className="text-white text-[22px] font-extrabold mb-1">
            Aqlan Dental Pro
          </div>
          <div className="text-[13px]" style={{ color: "rgba(255,255,255,0.65)" }}>
            مركز د. عقلان الكامل لطب وتقويم الأسنان
          </div>
          <div className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.45)" }}>
            تعز — شارع التحرير الأعلى
          </div>
        </div>

        {/* Two Panels */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5 mb-7">
          <Suspense fallback={<div className="rounded-[20px] p-7" style={glassCardStyle}><div className="animate-pulse text-white text-sm">جارٍ التحميل...</div></div>}>
            <StaffLoginPanel />
          </Suspense>
          <PatientLoginPanel />
        </div>

        {/* Public website link */}
        <div className="text-center mb-5">
          <a
            href="/home"
            className="inline-flex items-center gap-2 text-[13px] font-medium transition-colors no-underline"
            style={{ color: "rgba(255,255,255,0.55)" }}
            onMouseEnter={(e) => (e.currentTarget.style.color = "rgba(255,255,255,0.85)")}
            onMouseLeave={(e) => (e.currentTarget.style.color = "rgba(255,255,255,0.55)")}
          >
            <Globe className="w-3.5 h-3.5" />
            زيارة الموقع الرسمي
          </a>
        </div>

        {/* Doctors */}
        <div className="w-full">
          <div className="text-center mb-3 text-[12px]" style={{ color: "rgba(255,255,255,0.45)" }}>
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
                  border: "2px solid rgba(255,255,255,0.2)",
                }}
              >
                {doc.initials}
              </div>
            ))}
          </div>
        </div>

        {/* Footer */}
        <div className="mt-8 text-[11px] text-center" style={{ color: "rgba(255,255,255,0.3)" }}>
          04-253028 · 770-245745 · 711-752823
        </div>
      </div>
    </div>
  );
}

// ─── Staff Login Panel ───────────────────────────────────────────────────────
function StaffLoginPanel() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { login, isLoading } = useAuthStore();
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StaffFormData>({ resolver: zodResolver(staffSchema) });

  /** Resolve the post-login landing page based on user role */
  const getRoleDefaultRoute = (role: string | undefined): string => {
    if (["GeneralDentist", "OralSurgeon", "Orthodontist"].includes(role ?? "")) {
      return "/doctor-clinic";
    }
    if (role === "Accountant") {
      return "/finance-v3";
    }
    // Admin, Reception, Assistant, BranchManager → daily operations
    return "/daily-operations";
  };

  const onSubmit = async (data: StaffFormData) => {
    setError("");
    try {
      const mustChange = await login(data);
      if (mustChange) {
        router.push("/change-password");
      } else {
        // Priority: redirect query param > role-based default
        const redirectUrl = searchParams.get("redirect");
        if (redirectUrl) {
          router.push(redirectUrl);
        } else {
          const role = useAuthStore.getState().user?.role;
          router.push(getRoleDefaultRoute(role));
        }
      }
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { message?: string } }; code?: string; message?: string };
      if (!axiosErr.response) {
        // Network error — server unreachable
        if (axiosErr.code === "ERR_NETWORK" || axiosErr.code === "ECONNREFUSED" || axiosErr.code === "ECONNABORTED") {
          setError("لا يمكن الاتصال بالخادم. تأكد من الاتصال بالإنترنت ثم حاول مرة أخرى.");
        } else {
          setError("لا يمكن الاتصال بالخادم. تأكد من الاتصال بالإنترنت ثم حاول مرة أخرى.");
        }
      } else {
        const msg = axiosErr.response?.data?.message;
        setError(msg || "اسم المستخدم أو كلمة المرور غير صحيحة");
      }
    }
  };

  return (
    <div className="rounded-[20px] p-7 flex flex-col" style={glassCardStyle}>
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-1">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(61,122,181,0.25)" }}
          >
            <svg className="w-4 h-4" style={{ color: BRAND_BLUE }} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </div>
          <h2 className="text-lg font-bold text-white">دخول الطاقم</h2>
        </div>
        <p className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.5)" }}>
          للأطباء، الاستقبال، الإدارة، والمحاسبة
        </p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 flex-1">
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
            onBlur={(e) => (e.currentTarget.style.borderColor = errors.username ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
          />
          {errors.username && (
            <p className="mt-1 text-xs" style={{ color: "#fca5a5" }}>
              {errors.username.message}
            </p>
          )}
        </div>

        <div>
          <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
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
              onBlur={(e) => (e.currentTarget.style.borderColor = errors.password ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
            />
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="absolute inset-y-0 start-3 flex items-center"
              style={{ color: "rgba(255,255,255,0.5)" }}
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

        <div className="text-center pt-1">
          <Link
            href="/forgot-password"
            className="text-[13px] hover:underline transition"
            style={{ color: "rgba(61,122,181,0.9)" }}
          >
            نسيت كلمة السر؟
          </Link>
        </div>

        {/* Submit — Brand Orange CTA */}
        <button
          type="submit"
          disabled={isLoading}
          className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
          style={{
            background: isLoading ? BRAND_ORANGE_DARK : BRAND_ORANGE,
            boxShadow: "0 4px 12px rgba(245,146,46,0.3)",
          }}
          onMouseEnter={(e) => !isLoading && (e.currentTarget.style.background = BRAND_ORANGE_DARK)}
          onMouseLeave={(e) => (e.currentTarget.style.background = isLoading ? BRAND_ORANGE_DARK : BRAND_ORANGE)}
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

// ─── Patient Login Panel ─────────────────────────────────────────────────────
function PatientLoginPanel() {
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
      setAuth(data.profile, data.accessToken);
      router.push("/portal");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "اسم المستخدم أو كلمة المرور غير صحيحة");
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

  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code || code.length < 6) {
      setError("أدخل رمز التحقق المكون من 6 أرقام");
      return;
    }
    if (!newPassword || newPassword.length < 4) {
      setError("أدخل كلمة مرور جديدة (4 أحرف على الأقل)");
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
      setAuth(data.profile, data.accessToken);
      router.push("/portal");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || "رمز التحقق غير صحيح أو منتهي الصلاحية");
    } finally {
      setLoading(false);
    }
  };

  /* Patient panel uses a sky-blue tint to distinguish from staff panel */
  const patientPanelStyle: React.CSSProperties = {
    background: "rgba(61,122,181,0.08)",
    backdropFilter: "blur(12px)",
    border: "1px solid rgba(61,122,181,0.2)",
  };

  return (
    <div className="rounded-[20px] p-7 flex flex-col" style={patientPanelStyle}>
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-1">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{ background: "rgba(61,122,181,0.25)" }}
          >
            <UserIcon className="w-4 h-4" style={{ color: BRAND_BLUE }} />
          </div>
          <h2 className="text-lg font-bold text-white">دخول المرضى</h2>
        </div>
        <p className="text-[12px] mt-1" style={{ color: "rgba(255,255,255,0.5)" }}>
          لمتابعة المواعيد، العلاجات، الوصفات، والمدفوعات
        </p>
      </div>

      {step === "login" ? (
        <form onSubmit={handleLogin} className="space-y-4 flex-1">
          {error && (
            <div className="text-[13px]" style={{ color: "#fca5a5" }}>
              {error}
            </div>
          )}

          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
              اسم المستخدم
            </label>
            <input
              type="text"
              value={username}
              onChange={(e) => { setUsername(e.target.value); setError(""); }}
              placeholder="GM0001"
              dir="ltr"
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left placeholder:text-white/30"
              style={inputStyle(!!error)}
              onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
              onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
            />
            <p className="mt-1 text-[11px]" style={{ color: "rgba(255,255,255,0.35)" }}>
              رقم الملف (مثل GM0001)
            </p>
          </div>

          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
              كلمة المرور
            </label>
            <div className="relative">
              <input
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => { setPassword(e.target.value); setError(""); }}
                placeholder="••••••"
                dir="ltr"
                className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left placeholder:text-white/30"
                style={inputStyle(!!error)}
                onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
                onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute inset-y-0 start-3 flex items-center"
                style={{ color: "rgba(255,255,255,0.5)" }}
              >
                {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          <div className="text-left">
            <button
              type="button"
              onClick={() => { setStep("forgot"); setError(""); setSuccess(""); }}
              className="text-[12px] no-underline bg-transparent border-none cursor-pointer"
              style={{ color: BRAND_BLUE }}
            >
              نسيت كلمة المرور؟
            </button>
          </div>

          {/* Login — Sky Blue (secondary brand) */}
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
            style={{
              background: loading ? "#2d5e8e" : BRAND_BLUE,
              boxShadow: "0 4px 12px rgba(61,122,181,0.3)",
            }}
            onMouseEnter={(e) => !loading && (e.currentTarget.style.background = "#2d5e8e")}
            onMouseLeave={(e) => (e.currentTarget.style.background = loading ? "#2d5e8e" : BRAND_BLUE)}
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                جارٍ الدخول...
              </>
            ) : (
              "دخول إلى بوابة المريض"
            )}
          </button>
        </form>
      ) : step === "forgot" ? (
        <form onSubmit={handleForgotPassword} className="space-y-4 flex-1">
          {error && (
            <div className="text-[13px]" style={{ color: "#fca5a5" }}>
              {error}
            </div>
          )}

          <div className="text-center">
            <div
              className="w-10 h-10 rounded-lg flex items-center justify-center mx-auto mb-2"
              style={{ background: "rgba(61,122,181,0.2)" }}
            >
              <Phone className="w-5 h-5" style={{ color: BRAND_BLUE }} />
            </div>
            <p className="text-[12px]" style={{ color: "rgba(255,255,255,0.55)" }}>
              أدخل رقم هاتفك المسجل عندنا لإرسال رمز التحقق عبر واتساب
            </p>
          </div>

          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
              رقم الهاتف
            </label>
            <input
              type="tel"
              value={phoneNumber}
              onChange={(e) => { setPhoneNumber(e.target.value); setError(""); }}
              placeholder="770123456"
              dir="ltr"
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left placeholder:text-white/30"
              style={inputStyle(!!error)}
              onFocus={(e) => Object.assign(e.currentTarget.style, inputFocusStyle(!!error))}
              onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
            />
            <p className="mt-1 text-[11px]" style={{ color: "rgba(255,255,255,0.35)" }}>
              أدخل الرقم بدون رمز الدولة (967+)
            </p>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-[10px] text-white text-[15px] font-bold border-none cursor-pointer transition-colors flex items-center justify-center gap-2"
            style={{
              background: loading ? "#2d5e8e" : BRAND_BLUE,
            }}
            onMouseEnter={(e) => !loading && (e.currentTarget.style.background = "#2d5e8e")}
            onMouseLeave={(e) => (e.currentTarget.style.background = loading ? "#2d5e8e" : BRAND_BLUE)}
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

          <button
            type="button"
            onClick={() => { setStep("login"); setError(""); setSuccess(""); }}
            className="w-full py-2 text-[12px] transition flex items-center justify-center gap-1 bg-transparent border-none cursor-pointer"
            style={{ color: "rgba(255,255,255,0.5)" }}
          >
            <ArrowRight className="w-3 h-3" />
            العودة إلى تسجيل الدخول
          </button>
        </form>
      ) : (
        <form onSubmit={handleResetPassword} className="space-y-4 flex-1">
          {success && (
            <div className="text-[13px]" style={{ color: "#86efac" }}>
              {success}
            </div>
          )}

          {error && (
            <div className="text-[13px]" style={{ color: "#fca5a5" }}>
              {error}
            </div>
          )}

          <div className="text-center">
            <div
              className="w-10 h-10 rounded-lg flex items-center justify-center mx-auto mb-2"
              style={{ background: "rgba(61,122,181,0.2)" }}
            >
              <KeyRound className="w-5 h-5" style={{ color: BRAND_BLUE }} />
            </div>
            <p className="text-[12px]" style={{ color: "rgba(255,255,255,0.55)" }}>
              أدخل الرمز المرسل وكلمة المرور الجديدة
            </p>
          </div>

          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
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
              onBlur={(e) => (e.currentTarget.style.borderColor = error ? "rgba(252,165,165,0.7)" : "rgba(255,255,255,0.18)")}
              autoFocus
            />
          </div>

          <div>
            <label className="block text-[13px] font-semibold mb-1.5" style={{ color: "rgba(255,255,255,0.75)" }}>
              كلمة المرور الجديدة
            </label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => { setNewPassword(e.target.value); setError(""); }}
              placeholder="أدخل كلمة مرور جديدة"
              dir="ltr"
              className="w-full py-2.5 px-3.5 rounded-[10px] text-white text-sm outline-none transition-colors text-left placeholder:text-white/30"
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
              background: loading ? "#2d5e8e" : BRAND_BLUE,
            }}
            onMouseEnter={(e) => !loading && (e.currentTarget.style.background = "#2d5e8e")}
            onMouseLeave={(e) => (e.currentTarget.style.background = loading ? "#2d5e8e" : BRAND_BLUE)}
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                جارٍ التحقق...
              </>
            ) : (
              "إعادة تعيين كلمة المرور"
            )}
          </button>

          <button
            type="button"
            onClick={() => { setStep("forgot"); setError(""); setSuccess(""); setCode(""); }}
            className="w-full py-2 text-[12px] transition flex items-center justify-center gap-1 bg-transparent border-none cursor-pointer"
            style={{ color: "rgba(255,255,255,0.5)" }}
          >
            <ArrowRight className="w-3 h-3" />
            إعادة إرسال الرمز
          </button>
        </form>
      )}
    </div>
  );
}
