"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { useGoogleReCaptcha } from "react-google-recaptcha-v3";
import {
  CheckCircle2,
  Loader2,
  ChevronRight,
  ChevronLeft,
  CalendarDays,
  MessageCircle,
  Phone,
  MapPin,
  Clock,
  AlertCircle,
  User,
  Stethoscope,
  FileText,
  ClipboardCheck,
} from "lucide-react";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

const SERVICES = [
  "تقويم الأسنان",
  "طب الأسنان العام",
  "جراحة الوجه والفكين",
  "تنظيف الأسنان",
  "تجميل الأسنان",
  "علاج جذور",
  "زراعة أسنان",
  "استشارة",
];

interface Doctor {
  id: string;
  name: string;
  specialty: string;
  color: string;
  avatarInitials: string;
}

interface TimeSlot {
  time: string;
  available: boolean;
  reason?: string;
}

interface AvailabilityResponse {
  date: string;
  serviceType: string | null;
  slots: TimeSlot[];
  isClosed: boolean;
  message?: string;
  doctorId?: string;
  doctorName?: string;
}

interface FormData {
  patientName: string;
  phoneNumber: string;
  email: string;
  serviceType: string;
  doctorId: string;
  preferredDate: string;
  preferredTime: string;
  notes: string;
}

interface FormErrors {
  patientName?: string;
  phoneNumber?: string;
  email?: string;
  preferredDate?: string;
  preferredTime?: string;
}

/** Convert "HH:mm" 24h → Arabic display like "9:00 ص" */
function toArabicTime(time24: string): string {
  const [hStr, mStr] = time24.split(":");
  const h = parseInt(hStr, 10);
  const m = mStr;
  if (h === 0) return `12:${m} ص`;
  if (h === 12) return `12:${m} م`;
  if (h < 12) return `${h}:${m} ص`;
  return `${h - 12}:${m} م`;
}

/** Get today's date in YYYY-MM-DD */
function getTodayDate(): string {
  return new Date().toISOString().split("T")[0];
}

/** Format a YYYY-MM-DD date to Arabic display */
function toArabicDate(dateStr: string): string {
  if (!dateStr) return "—";
  const date = new Date(dateStr + "T00:00:00");
  const days = [
    "الأحد",
    "الاثنين",
    "الثلاثاء",
    "الأربعاء",
    "الخميس",
    "الجمعة",
    "السبت",
  ];
  const months = [
    "يناير",
    "فبراير",
    "مارس",
    "أبريل",
    "مايو",
    "يونيو",
    "يوليو",
    "أغسطس",
    "سبتمبر",
    "أكتوبر",
    "نوفمبر",
    "ديسمبر",
  ];
  return `${days[date.getDay()]}، ${date.getDate()} ${months[date.getMonth()]} ${date.getFullYear()}`;
}

const STEPS = [
  { label: "معلومات المريض", icon: User },
  { label: "الخدمة والطبيب", icon: Stethoscope },
  { label: "التاريخ والوقت", icon: CalendarDays },
  { label: "تأكيد الحجز", icon: ClipboardCheck },
] as const;

export default function BookPage() {
  const [step, setStep] = useState(1);
  const [form, setForm] = useState<FormData>({
    patientName: "",
    phoneNumber: "",
    email: "",
    serviceType: "",
    doctorId: "",
    preferredDate: "",
    preferredTime: "",
    notes: "",
  });
  const [errors, setErrors] = useState<FormErrors>({});
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [serverError, setServerError] = useState("");
  const [isConflict, setIsConflict] = useState(false);

  // ── Doctors state ──
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [doctorsLoaded, setDoctorsLoaded] = useState(false);

  // ── Availability state ──
  const [slots, setSlots] = useState<TimeSlot[]>([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsError, setSlotsError] = useState("");
  const [isClosed, setIsClosed] = useState(false);
  const [closedMessage, setClosedMessage] = useState("");
  const [availabilityDoctorName, setAvailabilityDoctorName] = useState("");
  const { executeRecaptcha } = useGoogleReCaptcha();

  // ── Fetch doctors on page load ──
  useEffect(() => {
    async function fetchDoctors() {
      try {
        const res = await fetch(`${API_URL}/api/public/doctors`);
        if (res.ok) {
          const data = await res.json();
          setDoctors(Array.isArray(data) ? data : []);
        }
      } catch {
        // silently fail — doctor selection is optional
      } finally {
        setDoctorsLoaded(true);
      }
    }
    fetchDoctors();
  }, []);

  // ── Fetch availability when date or doctor changes ──
  const fetchAvailability = useCallback(
    async (date: string, doctorId: string, serviceType: string) => {
      if (!date) {
        setSlots([]);
        setSlotsError("");
        setIsClosed(false);
        setClosedMessage("");
        setAvailabilityDoctorName("");
        return;
      }

      setSlotsLoading(true);
      setSlotsError("");
      setIsClosed(false);
      setClosedMessage("");
      setAvailabilityDoctorName("");

      try {
        const params = new URLSearchParams({ date });
        if (doctorId) params.set("doctorId", doctorId);
        if (serviceType) params.set("serviceType", serviceType);

        const res = await fetch(
          `${API_URL}/api/public/booking-availability?${params}`
        );

        if (!res.ok) {
          const data = await res.json().catch(() => ({}));
          setSlotsError(data?.message || "تعذّر تحميل الأوقات المتاحة");
          setSlots([]);
          setSlotsLoading(false);
          return;
        }

        const data: AvailabilityResponse = await res.json();

        if (data.isClosed) {
          setIsClosed(true);
          setClosedMessage(data.message || "");
          setSlots([]);
        } else {
          setSlots(data.slots || []);
        }
        setAvailabilityDoctorName(data.doctorName || "");
      } catch {
        setSlotsError("تعذّر الاتصال بالخادم لتحميل الأوقات");
        setSlots([]);
      } finally {
        setSlotsLoading(false);
      }
    },
    []
  );

  // Re-fetch when date, doctor, or serviceType changes
  useEffect(() => {
    setForm((prev) => ({ ...prev, preferredTime: "" }));

    const timer = setTimeout(() => {
      fetchAvailability(form.preferredDate, form.doctorId, form.serviceType);
    }, 300);

    return () => clearTimeout(timer);
  }, [form.preferredDate, form.doctorId, form.serviceType, fetchAvailability]);

  // ── Step validation ──
  function validateStep1(): boolean {
    const errs: FormErrors = {};
    if (!form.patientName.trim()) errs.patientName = "الاسم مطلوب";
    if (!form.phoneNumber.trim()) errs.phoneNumber = "رقم الهاتف مطلوب";
    else if (!/^[\d\s\-\+\(\)]{7,20}$/.test(form.phoneNumber.trim())) {
      errs.phoneNumber = "رقم الهاتف غير صحيح";
    }
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
      errs.email = "البريد الإلكتروني غير صحيح";
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  }

  function validateStep3(): boolean {
    const errs: FormErrors = {};
    if (!form.preferredDate) {
      errs.preferredDate = "التاريخ المفضل مطلوب";
    } else {
      const today = getTodayDate();
      if (form.preferredDate < today) {
        errs.preferredDate = "لا يمكن اختيار تاريخ سابق";
      }
      const day = new Date(form.preferredDate + "T00:00:00").getDay();
      if (day === 5) {
        errs.preferredDate =
          "المركز مغلق يوم الجمعة، يرجى اختيار يوم آخر";
      }
    }
    if (!form.preferredTime) {
      errs.preferredTime = "يرجى اختيار وقت مناسب من الأوقات المتاحة.";
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  }

  function goNext() {
    setServerError("");
    setIsConflict(false);
    if (step === 1 && validateStep1()) {
      setStep(2);
    } else if (step === 2) {
      setStep(3);
    } else if (step === 3 && validateStep3()) {
      setStep(4);
    }
  }

  function goBack() {
    setServerError("");
    setIsConflict(false);
    setErrors({});
    if (step > 1) setStep(step - 1);
  }

  // ── Submit ──
  async function handleSubmit() {
    // Re-validate all before submit
    const step1Valid = validateStep1();
    const step3Valid = validateStep3();
    if (!step1Valid || !step3Valid) {
      // Go back to the first step with errors
      if (!step1Valid) setStep(1);
      else setStep(3);
      return;
    }

    setLoading(true);
    setServerError("");
    setIsConflict(false);

    try {
      // Generate reCAPTCHA token if available
      let recaptchaToken: string | undefined;
      if (executeRecaptcha) {
        recaptchaToken = await executeRecaptcha('booking_submit');
      }

      const res = await fetch(`${API_URL}/api/public/booking-requests`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          patientName: form.patientName.trim(),
          phoneNumber: form.phoneNumber.trim(),
          email: form.email.trim() || null,
          serviceType: form.serviceType || null,
          preferredDate: form.preferredDate || null,
          preferredTime: form.preferredTime || null,
          notes: form.notes.trim() || null,
          doctorId: form.doctorId || null,
          recaptchaToken,
          website_url: "", // honeypot field — bots will fill this
        }),
      });

      if (res.ok || res.status === 201) {
        setSuccess(true);
      } else if (res.status === 409) {
        const data = await res.json().catch(() => ({}));
        const msg =
          data?.message ||
          "تعذّر إرسال الطلب. يرجى المحاولة مرة أخرى أو اختيار وقت آخر.";
        setServerError(msg);
        setIsConflict(true);
        // Refresh availability in case it was a slot conflict
        if (form.preferredDate) {
          fetchAvailability(
            form.preferredDate,
            form.doctorId,
            form.serviceType
          );
        }
      } else if (res.status === 400) {
        const data = await res.json().catch(() => ({}));
        setServerError(
          data?.message || "يرجى التأكد من صحة البيانات المدخلة."
        );
        setIsConflict(false);
      } else {
        setServerError(
          "حدث خطأ أثناء إرسال طلبك. يرجى المحاولة مرة أخرى أو الاتصال بنا مباشرة."
        );
        setIsConflict(false);
      }
    } catch {
      setServerError(
        "تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت وحاول مجدداً."
      );
      setIsConflict(false);
    } finally {
      setLoading(false);
    }
  }

  // ── Helpers ──
  const selectedDoctor = doctors.find((d) => d.id === form.doctorId);
  const availableSlots = slots.filter((s) => s.available);
  const hasNoAvailableSlots = slots.length > 0 && availableSlots.length === 0;
  const canSubmit =
    form.patientName.trim() !== "" &&
    form.phoneNumber.trim() !== "" &&
    /^[\d\s\-\+\(\)]{7,20}$/.test(form.phoneNumber.trim()) &&
    form.preferredDate !== "" &&
    form.preferredTime !== "" &&
    !errors.preferredDate;

  // ── Success screen ──
  if (success) {
    return (
      <div
        dir="rtl"
        className="min-h-[70vh] flex items-center justify-center px-4 py-20 bg-[#F8FAFC]"
      >
        <div className="max-w-md w-full bg-white rounded-3xl shadow-xl p-8 sm:p-10 text-center border border-slate-100">
          <div
            className="inline-block text-xs font-semibold px-3 py-1 rounded-full mb-5 border"
            style={{
              backgroundColor: "rgba(135,206,235,0.1)",
              borderColor: "rgba(135,206,235,0.3)",
              color: "#0284c7",
            }}
          >
            تم بنجاح
          </div>

          <div
            className="w-24 h-24 rounded-full flex items-center justify-center mx-auto mb-6 shadow-lg"
            style={{
              background: "linear-gradient(135deg, #87CEEB, #0284c7)",
            }}
          >
            <CheckCircle2 className="w-12 h-12 text-white" />
          </div>

          <h2 className="text-2xl font-extrabold text-slate-900 mb-3">
            تم إرسال طلب الحجز بنجاح
          </h2>
          <p className="text-slate-500 text-sm leading-relaxed mb-6">
            سيتواصل معك فريق الاستقبال لتأكيد الموعد.
          </p>

          {/* Appointment details */}
          <div className="bg-slate-50 rounded-2xl p-5 mb-6 text-right border border-slate-100">
            <div className="space-y-3">
              <div className="flex items-center gap-3">
                <CalendarDays
                  className="w-4 h-4 flex-shrink-0"
                  style={{ color: "#FF8C00" }}
                />
                <div>
                  <div className="text-xs text-slate-400">التاريخ</div>
                  <div className="text-sm font-bold text-slate-800">
                    {toArabicDate(form.preferredDate)}
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <Clock
                  className="w-4 h-4 flex-shrink-0"
                  style={{ color: "#87CEEB" }}
                />
                <div>
                  <div className="text-xs text-slate-400">الوقت</div>
                  <div className="text-sm font-bold text-slate-800" dir="ltr">
                    {toArabicTime(form.preferredTime)}
                  </div>
                </div>
              </div>
              {selectedDoctor && (
                <div className="flex items-center gap-3">
                  <User
                    className="w-4 h-4 flex-shrink-0"
                    style={{ color: "#0284c7" }}
                  />
                  <div>
                    <div className="text-xs text-slate-400">الطبيب</div>
                    <div className="text-sm font-bold text-slate-800">
                      {selectedDoctor.name}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="flex flex-col gap-3 mb-6">
            <Link
              href="/home"
              className="text-white font-bold px-6 py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
              style={{ backgroundColor: "#FF8C00" }}
            >
              العودة للرئيسية
              <ChevronLeft className="w-4 h-4" />
            </Link>
            <a
              href="https://wa.me/967770245745"
              target="_blank"
              rel="noopener noreferrer"
              className="bg-green-500 hover:bg-green-600 text-white font-semibold px-6 py-3.5 rounded-xl transition-colors inline-flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-4 h-4" />
              تواصل عبر واتساب
            </a>
            <a
              href="tel:04253028"
              className="text-white font-semibold px-6 py-3.5 rounded-xl transition-colors inline-flex items-center justify-center gap-2"
              style={{ backgroundColor: "#87CEEB" }}
            >
              <Phone className="w-4 h-4" />
              04-253028
            </a>
          </div>

          <div className="flex items-center justify-center gap-1.5 text-xs text-slate-400 border-t border-slate-100 pt-5">
            <MapPin
              className="w-3.5 h-3.5"
              style={{ color: "#FF8C00" }}
            />
            تعز، اليمن — شارع التحرير الأعلى
            <span className="mx-1 text-slate-200">|</span>
            <Clock
              className="w-3.5 h-3.5"
              style={{ color: "#87CEEB" }}
            />
            السبت – الخميس: 8 ص – 8 م
          </div>
        </div>
      </div>
    );
  }

  // ── Step indicator ──
  function StepIndicator() {
    return (
      <div className="flex items-center justify-center gap-0 mb-8">
        {STEPS.map((s, i) => {
          const stepNum = i + 1;
          const isActive = step === stepNum;
          const isCompleted = step > stepNum;
          const Icon = s.icon;

          return (
            <div key={stepNum} className="flex items-center">
              {/* Step circle + label */}
              <div className="flex flex-col items-center">
                <div
                  className={`w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300 border-2 ${
                    isCompleted
                      ? "border-[#87CEEB] bg-[#87CEEB] text-white"
                      : isActive
                      ? "border-[#FF8C00] bg-[#FF8C00] text-white shadow-md"
                      : "border-slate-200 bg-white text-slate-400"
                  }`}
                >
                  {isCompleted ? (
                    <CheckCircle2 className="w-5 h-5" />
                  ) : (
                    <Icon className="w-4 h-4" />
                  )}
                </div>
                <span
                  className={`text-[10px] sm:text-xs mt-1.5 font-semibold text-center max-w-[72px] leading-tight transition-colors ${
                    isCompleted
                      ? "text-[#0284c7]"
                      : isActive
                      ? "text-[#FF8C00]"
                      : "text-slate-400"
                  }`}
                >
                  {s.label}
                </span>
              </div>
              {/* Connector line */}
              {i < STEPS.length - 1 && (
                <div
                  className={`w-6 sm:w-10 h-0.5 mx-1 mt-[-16px] transition-colors duration-300 ${
                    step > stepNum ? "bg-[#87CEEB]" : "bg-slate-200"
                  }`}
                />
              )}
            </div>
          );
        })}
      </div>
    );
  }

  // ── Step 1: Patient info ──
  function Step1() {
    return (
      <div className="space-y-5">
        <div className="text-center mb-6">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
            style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
          >
            <User className="w-7 h-7" style={{ color: "#87CEEB" }} />
          </div>
          <h3 className="text-lg font-bold text-slate-900">
            معلومات المريض
          </h3>
          <p className="text-sm text-slate-400 mt-1">
            أدخل بياناتك للتواصل والتأكيد
          </p>
        </div>

        {/* Name */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            الاسم الكامل <span className="text-red-500">*</span>
          </label>
          <input
            type="text"
            value={form.patientName}
            onChange={(e) =>
              setForm({ ...form, patientName: e.target.value })
            }
            placeholder="أدخل اسمك الكامل"
            className={`w-full px-4 py-3.5 rounded-xl border text-right transition-all duration-200 outline-none focus:ring-2 ${
              errors.patientName
                ? "border-red-400 bg-red-50 focus:ring-red-300"
                : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
            }`}
          />
          {errors.patientName && (
            <p className="text-red-500 text-xs mt-1.5 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5" />
              {errors.patientName}
            </p>
          )}
        </div>

        {/* Phone */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            رقم الهاتف <span className="text-red-500">*</span>
          </label>
          <input
            type="tel"
            value={form.phoneNumber}
            onChange={(e) =>
              setForm({ ...form, phoneNumber: e.target.value })
            }
            placeholder="مثال: 04-253028"
            dir="ltr"
            className={`w-full px-4 py-3.5 rounded-xl border text-left transition-all duration-200 outline-none focus:ring-2 ${
              errors.phoneNumber
                ? "border-red-400 bg-red-50 focus:ring-red-300"
                : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
            }`}
          />
          {errors.phoneNumber && (
            <p className="text-red-500 text-xs mt-1.5 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5" />
              {errors.phoneNumber}
            </p>
          )}
        </div>

        {/* Email */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            البريد الإلكتروني{" "}
            <span className="text-slate-400 font-normal">(اختياري)</span>
          </label>
          <input
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            placeholder="example@email.com"
            dir="ltr"
            className={`w-full px-4 py-3.5 rounded-xl border text-left transition-all duration-200 outline-none focus:ring-2 ${
              errors.email
                ? "border-red-400 bg-red-50 focus:ring-red-300"
                : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
            }`}
          />
          {errors.email && (
            <p className="text-red-500 text-xs mt-1.5 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5" />
              {errors.email}
            </p>
          )}
        </div>
      </div>
    );
  }

  // ── Step 2: Service & Doctor ──
  function Step2() {
    return (
      <div className="space-y-5">
        <div className="text-center mb-6">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
            style={{ backgroundColor: "rgba(255,140,0,0.1)" }}
          >
            <Stethoscope className="w-7 h-7" style={{ color: "#FF8C00" }} />
          </div>
          <h3 className="text-lg font-bold text-slate-900">
            نوع الخدمة والطبيب
          </h3>
          <p className="text-sm text-slate-400 mt-1">
            اختر الخدمة المطلوبة والطبيب المفضل
          </p>
        </div>

        {/* Service Type */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            نوع الخدمة
          </label>
          <select
            value={form.serviceType}
            onChange={(e) =>
              setForm({ ...form, serviceType: e.target.value })
            }
            className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right"
          >
            <option value="">اختر الخدمة...</option>
            {SERVICES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>

        {/* Doctor Selection */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            الطبيب المفضل
          </label>
          {!doctorsLoaded ? (
            <div className="flex items-center gap-2 text-slate-400 text-sm py-3">
              <Loader2 className="w-4 h-4 animate-spin" />
              جاري تحميل قائمة الأطباء...
            </div>
          ) : (
            <select
              value={form.doctorId}
              onChange={(e) =>
                setForm({
                  ...form,
                  doctorId: e.target.value,
                  preferredTime: "",
                })
              }
              className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right"
            >
              <option value="">أي طبيب متاح</option>
              {doctors.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} — {d.specialty}
                </option>
              ))}
            </select>
          )}
          {/* Show selected doctor preview */}
          {selectedDoctor && (
            <div className="mt-3 flex items-center gap-3 p-3 rounded-xl bg-slate-50 border border-slate-100">
              <div
                className="w-10 h-10 rounded-xl flex items-center justify-center text-white font-bold text-sm flex-shrink-0"
                style={{ backgroundColor: selectedDoctor.color }}
              >
                {selectedDoctor.avatarInitials}
              </div>
              <div>
                <div className="text-sm font-bold text-slate-800">
                  {selectedDoctor.name}
                </div>
                <div
                  className="text-xs font-semibold"
                  style={{ color: selectedDoctor.color }}
                >
                  {selectedDoctor.specialty}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    );
  }

  // ── Step 3: Date & Time ──
  function Step3() {
    return (
      <div className="space-y-5">
        <div className="text-center mb-6">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
            style={{ backgroundColor: "rgba(135,206,235,0.12)" }}
          >
            <CalendarDays
              className="w-7 h-7"
              style={{ color: "#87CEEB" }}
            />
          </div>
          <h3 className="text-lg font-bold text-slate-900">
            التاريخ والوقت
          </h3>
          <p className="text-sm text-slate-400 mt-1">
            اختر الموعد المناسب لك
          </p>
        </div>

        {/* Date */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            التاريخ المفضل <span className="text-red-500">*</span>
          </label>
          <input
            type="date"
            value={form.preferredDate}
            onChange={(e) =>
              setForm({
                ...form,
                preferredDate: e.target.value,
                preferredTime: "",
              })
            }
            min={getTodayDate()}
            className={`w-full px-4 py-3.5 rounded-xl border transition-all duration-200 outline-none focus:ring-2 ${
              errors.preferredDate
                ? "border-red-400 bg-red-50 focus:ring-red-300"
                : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
            }`}
          />
          {errors.preferredDate && (
            <p className="text-red-500 text-xs mt-1.5 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5" />
              {errors.preferredDate}
            </p>
          )}
        </div>

        {/* Time slots */}
        {form.preferredDate && !errors.preferredDate && (
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-3">
              {availabilityDoctorName
                ? `أوقات د. ${availabilityDoctorName} المتاحة`
                : "الوقت المفضل"}
              <span className="text-red-500"> *</span>
            </label>

            {/* Loading state */}
            {slotsLoading && (
              <div className="flex items-center justify-center py-8 gap-2 text-slate-400">
                <Loader2 className="w-5 h-5 animate-spin" />
                <span className="text-sm">
                  جاري تحميل الأوقات المتاحة...
                </span>
              </div>
            )}

            {/* Error loading slots */}
            {slotsError && !slotsLoading && (
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-amber-700 text-sm text-center flex items-center justify-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                {slotsError}
              </div>
            )}

            {/* Friday / Closed message */}
            {isClosed && !slotsLoading && (
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-amber-700 text-sm text-center">
                {closedMessage ||
                  "المركز مغلق يوم الجمعة، يرجى اختيار يوم آخر."}
              </div>
            )}

            {/* No available slots */}
            {hasNoAvailableSlots &&
              !slotsLoading &&
              !isClosed &&
              !slotsError && (
                <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 text-slate-500 text-sm text-center">
                  لا توجد أوقات متاحة في هذا اليوم، يرجى اختيار يوم آخر.
                </div>
              )}

            {/* Time slot grid */}
            {slots.length > 0 && !slotsLoading && !isClosed && !slotsError && (
              <div className="grid grid-cols-4 sm:grid-cols-5 gap-2">
                {slots.map((slot) => {
                  const isSelected = form.preferredTime === slot.time;
                  const isUnavailable = !slot.available;

                  return (
                    <button
                      key={slot.time}
                      type="button"
                      disabled={isUnavailable}
                      onClick={() =>
                        setForm({ ...form, preferredTime: slot.time })
                      }
                      title={
                        isUnavailable
                          ? slot.reason || "محجوز"
                          : toArabicTime(slot.time)
                      }
                      className={`
                        relative px-2 py-2.5 rounded-xl text-sm font-medium transition-all duration-150 border
                        ${
                          isUnavailable
                            ? "bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed line-through"
                            : isSelected
                            ? "border-orange-500 text-white shadow-md scale-[1.02]"
                            : "bg-white border-slate-200 text-slate-700 hover:border-[#87CEEB] hover:bg-sky-50 cursor-pointer"
                        }
                      `}
                      style={
                        isSelected && !isUnavailable
                          ? { backgroundColor: "#FF8C00" }
                          : undefined
                      }
                    >
                      <span className="block text-center" dir="ltr">
                        {toArabicTime(slot.time)}
                      </span>
                      {isUnavailable && (
                        <span className="absolute -top-1 -left-1 w-4 h-4 bg-red-400 rounded-full flex items-center justify-center">
                          <span className="text-white text-[8px] font-bold">
                            ✕
                          </span>
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Selected time indicator */}
            {form.preferredTime && !slotsLoading && (
              <p className="mt-2 text-xs text-slate-400 text-center">
                الوقت المختار:{" "}
                <strong className="text-slate-700" dir="ltr">
                  {toArabicTime(form.preferredTime)}
                </strong>
              </p>
            )}
          </div>
        )}
        {/* Hint if no date selected yet */}
        {!form.preferredDate && (
          <p className="text-xs text-slate-400 text-center py-2">
            اختر التاريخ أولاً لعرض الأوقات المتاحة
          </p>
        )}

        {errors.preferredTime && form.preferredDate && (
          <p className="text-red-500 text-xs flex items-center gap-1">
            <AlertCircle className="w-3.5 h-3.5" />
            {errors.preferredTime}
          </p>
        )}
      </div>
    );
  }

  // ── Step 4: Notes & Confirmation ──
  function Step4() {
    return (
      <div className="space-y-5">
        <div className="text-center mb-6">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-3"
            style={{ backgroundColor: "rgba(5,150,105,0.1)" }}
          >
            <ClipboardCheck
              className="w-7 h-7"
              style={{ color: "#059669" }}
            />
          </div>
          <h3 className="text-lg font-bold text-slate-900">
            ملاحظات وتأكيد
          </h3>
          <p className="text-sm text-slate-400 mt-1">
            راجع بياناتك وأضف أي ملاحظات قبل الإرسال
          </p>
        </div>

        {/* Summary Card */}
        <div className="bg-slate-50 rounded-2xl p-5 border border-slate-100">
          <h4 className="text-sm font-bold text-slate-800 mb-4 flex items-center gap-2">
            <FileText className="w-4 h-4" style={{ color: "#87CEEB" }} />
            ملخص الحجز
          </h4>
          <div className="space-y-3">
            <div className="flex justify-between items-center py-2 border-b border-slate-100">
              <span className="text-xs text-slate-400">الاسم</span>
              <span className="text-sm font-semibold text-slate-800">
                {form.patientName}
              </span>
            </div>
            <div className="flex justify-between items-center py-2 border-b border-slate-100">
              <span className="text-xs text-slate-400">الهاتف</span>
              <span
                className="text-sm font-semibold text-slate-800"
                dir="ltr"
              >
                {form.phoneNumber}
              </span>
            </div>
            <div className="flex justify-between items-center py-2 border-b border-slate-100">
              <span className="text-xs text-slate-400">الخدمة</span>
              <span className="text-sm font-semibold text-slate-800">
                {form.serviceType || "—"}
              </span>
            </div>
            <div className="flex justify-between items-center py-2 border-b border-slate-100">
              <span className="text-xs text-slate-400">الطبيب</span>
              <span className="text-sm font-semibold text-slate-800">
                {selectedDoctor ? selectedDoctor.name : "أي طبيب متاح"}
              </span>
            </div>
            <div className="flex justify-between items-center py-2 border-b border-slate-100">
              <span className="text-xs text-slate-400">التاريخ</span>
              <span className="text-sm font-semibold text-slate-800">
                {toArabicDate(form.preferredDate)}
              </span>
            </div>
            <div className="flex justify-between items-center py-2">
              <span className="text-xs text-slate-400">الوقت</span>
              <span
                className="text-sm font-semibold text-slate-800"
                dir="ltr"
              >
                {form.preferredTime
                  ? toArabicTime(form.preferredTime)
                  : "—"}
              </span>
            </div>
          </div>
        </div>

        {/* No time selected warning */}
        {!form.preferredTime && (
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-3 text-amber-700 text-sm text-center flex items-center justify-center gap-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            يرجى اختيار وقت مناسب من الأوقات المتاحة.
          </div>
        )}

        {/* Notes */}
        <div>
          <label className="block text-sm font-semibold text-slate-700 mb-1.5">
            ملاحظات إضافية{" "}
            <span className="text-slate-400 font-normal">(اختياري)</span>
          </label>
          <textarea
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            rows={3}
            placeholder="أي معلومات إضافية تود إضافتها..."
            className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 resize-none text-right"
            maxLength={500}
          />
        </div>
      </div>
    );
  }

  // ── Main render ──
  return (
    <div dir="rtl" className="min-h-screen bg-[#F8FAFC]">
      {/* Page Header */}
      <div
        className="relative text-white py-14 overflow-hidden"
        style={{ backgroundColor: "#0F172A" }}
      >
        <div
          className="absolute inset-0 opacity-5"
          style={{
            backgroundImage:
              "radial-gradient(circle, #87CEEB 1px, transparent 1px)",
            backgroundSize: "28px 28px",
          }}
        />
        <div className="relative max-w-2xl mx-auto px-4 text-center">
          <div className="flex justify-center mb-5">
            <div className="bg-white rounded-2xl p-2 shadow-sm inline-flex">
              <img
                src="/logo.png"
                alt="مركز الدكتور عقلان الكامل"
                className="h-10 w-auto object-contain"
              />
            </div>
          </div>
          <div
            className="w-14 h-14 rounded-2xl border flex items-center justify-center mx-auto mb-5"
            style={{
              backgroundColor: "rgba(135,206,235,0.12)",
              borderColor: "rgba(135,206,235,0.2)",
            }}
          >
            <CalendarDays className="w-7 h-7" style={{ color: "#FF8C00" }} />
          </div>
          <h1 className="text-3xl md:text-4xl font-extrabold mb-3">
            احجز موعدك
          </h1>
          <p
            className="text-sm leading-relaxed max-w-lg mx-auto mb-5"
            style={{ color: "#94a3b8" }}
          >
            أرسل طلبك وسيقوم فريق الاستقبال بالتواصل معك لتأكيد الموعد.
          </p>
          <Link
            href="/home"
            className="inline-flex items-center gap-1 text-xs transition-colors hover:text-white"
            style={{ color: "rgba(135,206,235,0.6)" }}
          >
            <ChevronRight className="w-3 h-3" />
            العودة للرئيسية
          </Link>
        </div>
      </div>

      <div className="max-w-2xl mx-auto px-4 py-8 sm:py-10">
        {/* Form card */}
        <div className="bg-white rounded-3xl shadow-xl border border-slate-100 border-t-4 border-t-[#87CEEB] overflow-hidden">
          <div className="p-6 sm:p-8">
            {/* Step Indicator */}
            {StepIndicator()}

            {/* Step Content */}
            {step === 1 && Step1()}
            {step === 2 && Step2()}
            {step === 3 && Step3()}
            {step === 4 && Step4()}

            {/* Server error */}
            {serverError && step === 4 && (
              <div className="mt-5 space-y-3">
                <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center flex items-start justify-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{serverError}</span>
                </div>
                {isConflict && (
                  <a
                    href="https://wa.me/967770245745?text=مرحباً، بخصوص طلب الحجز"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="block bg-green-50 border border-green-200 rounded-xl p-3 text-green-700 text-sm text-center hover:bg-green-100 transition-colors"
                  >
                    <span className="flex items-center justify-center gap-2">
                      <MessageCircle className="w-4 h-4" />
                      تواصل معنا عبر واتساب للمساعدة
                    </span>
                  </a>
                )}
              </div>
            )}

            {/* Navigation buttons */}
            <div className="flex gap-3 mt-8">
              {step > 1 && (
                <button
                  type="button"
                  onClick={goBack}
                  className="flex-1 border border-slate-200 text-slate-700 font-semibold py-3.5 rounded-xl transition-all hover:bg-slate-50 hover:border-slate-300 inline-flex items-center justify-center gap-2"
                >
                  <ChevronRight className="w-4 h-4" />
                  السابق
                </button>
              )}
              {step < 4 ? (
                <button
                  type="button"
                  onClick={goNext}
                  className="flex-1 text-white font-bold py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
                  style={{ backgroundColor: "#FF8C00" }}
                >
                  التالي
                  <ChevronLeft className="w-4 h-4" />
                </button>
              ) : (
                <button
                  type="button"
                  onClick={handleSubmit}
                  disabled={loading || !canSubmit}
                  className="flex-1 disabled:opacity-60 text-white font-bold py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
                  style={{ backgroundColor: "#FF8C00" }}
                >
                  {loading ? (
                    <>
                      <Loader2 className="w-5 h-5 animate-spin" />
                      جاري الإرسال...
                    </>
                  ) : (
                    <>
                      إرسال طلب الحجز
                      <CheckCircle2 className="w-5 h-5" />
                    </>
                  )}
                </button>
              )}
            </div>

            <p className="text-center text-xs text-slate-400 mt-5">
              بإرسال هذا النموذج أنت توافق على التواصل معك لتأكيد موعدك
            </p>
          </div>
        </div>

        {/* Contact help box */}
        <div className="mt-6 bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
          <div className="px-6 pt-6 pb-4 text-center border-b border-slate-100">
            <p className="text-slate-900 font-bold text-base">
              تفضل بالتواصل المباشر معنا
            </p>
            <p className="text-slate-400 text-xs mt-1">
              نرد عليك خلال ساعات العمل: السبت – الخميس 8 ص – 8 م
            </p>
          </div>
          <div className="p-5 grid grid-cols-2 gap-3">
            <a
              href="tel:04253028"
              className="text-white font-semibold px-4 py-3 rounded-xl text-sm transition-opacity hover:opacity-90 flex items-center justify-center gap-2"
              style={{ backgroundColor: "#87CEEB" }}
            >
              <Phone className="w-4 h-4" />
              04-253028
            </a>
            <a
              href="https://wa.me/967770245745"
              target="_blank"
              rel="noopener noreferrer"
              className="bg-green-500 hover:bg-green-600 text-white font-semibold px-4 py-3 rounded-xl text-sm transition-colors flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-4 h-4" />
              واتساب
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}
