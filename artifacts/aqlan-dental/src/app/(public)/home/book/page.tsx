
import { useState, useEffect, useCallback, useRef } from "react";
import Link from "@/lib/nextLinkCompat";
import { useGoogleReCaptcha } from "react-google-recaptcha-v3";
import { useClinicBranding } from "@/hooks/useClinicBranding";
import { localDateString } from "@/lib/utils";
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
} from "lucide-react";

const API_URL = import.meta.env.VITE_API_URL ?? "";
const PUBLIC_API_TIMEOUT_MS = 8_000;

async function fetchPublicApi(input: string, init?: RequestInit): Promise<Response> {
  const controller = new AbortController();
  const timeout = window.setTimeout(
    () => controller.abort(),
    PUBLIC_API_TIMEOUT_MS
  );

  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    window.clearTimeout(timeout);
  }
}

/** Hardcoded fallback if API fails */
const FALLBACK_SERVICES = [
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
  serviceType?: string;
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
  return localDateString();
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

function getQuickBookingDates(): string[] {
  const dates: string[] = [];
  const cursor = new Date(`${getTodayDate()}T00:00:00`);

  while (dates.length < 3) {
    cursor.setDate(cursor.getDate() + 1);
    if (cursor.getDay() === 5) continue;
    dates.push(
      [
        cursor.getFullYear(),
        String(cursor.getMonth() + 1).padStart(2, "0"),
        String(cursor.getDate()).padStart(2, "0"),
      ].join("-")
    );
  }

  return dates;
}

const FALLBACK_WHATSAPP = "967770245745";

export default function BookPage() {
  const { whatsApp: clinicWhatsApp, phone: clinicPhone, address: clinicAddress } = useClinicBranding();
  const waNumber = clinicWhatsApp || FALLBACK_WHATSAPP;
  const telHref = `tel:${clinicPhone.replace(/[^\d+]/g, "")}`;
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
  const submittingRef = useRef(false);

  // ── Services state ──
  const [services, setServices] = useState<string[]>(FALLBACK_SERVICES);

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
  const availabilityRequestRef = useRef(0);
  const { executeRecaptcha } = useGoogleReCaptcha();

  // ── Fetch services on page load ──
  useEffect(() => {
    async function fetchServices() {
      try {
        const res = await fetchPublicApi(`${API_URL}/api/public/booking-services`);
        if (res.ok) {
          const data = await res.json();
          if (Array.isArray(data) && data.length > 0) {
            // API returns ClinicService objects — use ArabicName field
            const names = data.map((s: { arabicName?: string; name?: string; ArabicName?: string }) =>
              s.arabicName || s.ArabicName || s.name || ""
            ).filter(Boolean);
            if (names.length > 0) setServices(names);
          }
        }
      } catch {
        // silently fail — fallback services are already set
      }
    }
    fetchServices();
  }, []);

  // ── Fetch doctors on page load ──
  useEffect(() => {
    async function fetchDoctors() {
      try {
        const res = await fetchPublicApi(`${API_URL}/api/public/doctors`);
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
      const requestId = ++availabilityRequestRef.current;

      if (!date) {
        setSlots([]);
        setSlotsError("");
        setSlotsLoading(false);
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

        const res = await fetchPublicApi(
          `${API_URL}/api/public/booking-availability?${params}`
        );

        if (requestId !== availabilityRequestRef.current) return;

        if (!res.ok) {
          const data = await res.json().catch(() => ({}));
          setSlotsError(data?.message || "تعذّر تحميل الأوقات المتاحة");
          setSlots([]);
          return;
        }

        const data: AvailabilityResponse = await res.json();
        if (requestId !== availabilityRequestRef.current) return;

        if (data.isClosed) {
          setIsClosed(true);
          setClosedMessage(data.message || "");
          setSlots([]);
        } else {
          setSlots(data.slots || []);
        }
        setAvailabilityDoctorName(data.doctorName || "");
      } catch (error) {
        if (requestId !== availabilityRequestRef.current) return;
        setSlotsError(
          error instanceof DOMException && error.name === "AbortError"
            ? "استغرق تحميل الأوقات وقتاً أطول من المتوقع"
            : "تعذّر الاتصال بالخادم لتحميل الأوقات"
        );
        setSlots([]);
      } finally {
        if (requestId === availabilityRequestRef.current) {
          setSlotsLoading(false);
        }
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

  // ── Form validation ──
  function validateForm(): boolean {
    const errs: FormErrors = {};
    if (!form.patientName.trim()) errs.patientName = "الاسم مطلوب";
    if (!form.phoneNumber.trim()) errs.phoneNumber = "رقم الهاتف مطلوب";
    else if (!/^[\d\s\-\+\(\)]{7,20}$/.test(form.phoneNumber.trim())) {
      errs.phoneNumber = "رقم الهاتف غير صحيح";
    }
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
      errs.email = "البريد الإلكتروني غير صحيح";
    }
    if (!form.serviceType) errs.serviceType = "اختر نوع الخدمة";
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

  // ── Submit ──
  async function handleSubmit() {
    if (submittingRef.current) return;
    if (!validateForm()) return;

    submittingRef.current = true;
    setLoading(true);
    setServerError("");
    setIsConflict(false);

    try {
      // Generate reCAPTCHA token if available. A reCAPTCHA failure must never
      // block the booking: the backend treats the token as optional and only
      // validates it when present. Without this inner guard, a failed
      // executeRecaptcha (script blocked, or domain not whitelisted in the
      // Google reCAPTCHA console) throws and surfaces as a misleading
      // "تعذّر الاتصال بالخادم" network error.
      let recaptchaToken: string | undefined;
      if (executeRecaptcha) {
        try {
          recaptchaToken = await executeRecaptcha('booking_submit');
        } catch {
          recaptchaToken = undefined;
        }
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
      submittingRef.current = false;
      setLoading(false);
    }
  }

  // ── Helpers ──
  const selectedDoctor = doctors.find((d) => d.id === form.doctorId);
  const availableSlots = slots.filter((s) => s.available);
  const hasNoAvailableSlots = slots.length > 0 && availableSlots.length === 0;
  const quickBookingDates = getQuickBookingDates();

  // ── Success screen ──
  if (success) {
    return (
      <div
       
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
              href={`https://wa.me/${waNumber}`}
              target="_blank"
              rel="noopener noreferrer"
              className="bg-green-500 hover:bg-green-600 text-white font-semibold px-6 py-3.5 rounded-xl transition-colors inline-flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-4 h-4" />
              تواصل عبر واتساب
            </a>
            <a
              href={telHref}
              className="text-white font-semibold px-6 py-3.5 rounded-xl transition-colors inline-flex items-center justify-center gap-2"
              style={{ backgroundColor: "#87CEEB" }}
            >
              <Phone className="w-4 h-4" />
              {clinicPhone}
            </a>
          </div>

          <div className="flex items-center justify-center gap-1.5 text-xs text-slate-400 border-t border-slate-100 pt-5">
            <MapPin
              className="w-3.5 h-3.5"
              style={{ color: "#FF8C00" }}
            />
            {clinicAddress}
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

  // ── Patient info ──
  function Step1() {
    return (
      <section className="space-y-4" aria-labelledby="patient-info-title">
        <div className="flex items-center gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-sky-50">
            <User className="h-4 w-4 text-sky-600" />
          </span>
          <div>
            <h2 id="patient-info-title" className="font-bold text-slate-900">
              بيانات التواصل
            </h2>
            <p className="text-xs text-slate-500">
              الاسم والهاتف مطلوبان، والبريد اختياري
            </p>
          </div>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {/* Name */}
          <div>
          <label
            htmlFor="booking-name"
            className="block text-sm font-semibold text-slate-700 mb-1.5"
          >
            الاسم الكامل <span className="text-red-500">*</span>
          </label>
          <input
            id="booking-name"
            type="text"
            value={form.patientName}
            onChange={(e) => {
              setForm({ ...form, patientName: e.target.value });
              setErrors((current) => ({ ...current, patientName: undefined }));
            }}
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
          <label
            htmlFor="booking-phone"
            className="block text-sm font-semibold text-slate-700 mb-1.5"
          >
            رقم الهاتف <span className="text-red-500">*</span>
          </label>
          <input
            id="booking-phone"
            type="tel"
            value={form.phoneNumber}
            onChange={(e) => {
              setForm({ ...form, phoneNumber: e.target.value });
              setErrors((current) => ({ ...current, phoneNumber: undefined }));
            }}
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

          <div className="sm:col-span-2">
            <label
              htmlFor="booking-email"
              className="block text-sm font-semibold text-slate-700 mb-1.5"
            >
              البريد الإلكتروني{" "}
              <span className="font-normal text-slate-400">(اختياري)</span>
            </label>
            <input
              id="booking-email"
              type="email"
              value={form.email}
              onChange={(e) => {
                setForm({ ...form, email: e.target.value });
                setErrors((current) => ({ ...current, email: undefined }));
              }}
              placeholder="example@email.com"
              dir="ltr"
              className={`w-full px-4 py-3 rounded-xl border bg-white text-left outline-none transition-all focus:ring-2 ${
                errors.email
                  ? "border-red-400 focus:ring-red-300"
                  : "border-slate-200 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
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
      </section>
    );
  }

  // ── Service ──
  function Step2() {
    return (
      <section className="space-y-4" aria-labelledby="service-title">
        <div className="flex items-center gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-orange-50">
            <Stethoscope className="h-4 w-4 text-orange-500" />
          </span>
          <div>
            <h2 id="service-title" className="font-bold text-slate-900">
              سبب الزيارة
            </h2>
            <p className="text-xs text-slate-500">اختر الخدمة الأقرب لاحتياجك</p>
          </div>
        </div>

        {/* Service Type */}
        <div>
          <label
            htmlFor="booking-service"
            className="block text-sm font-semibold text-slate-700 mb-1.5"
          >
            نوع الخدمة <span className="text-red-500">*</span>
          </label>
          <select
            id="booking-service"
            value={form.serviceType}
            onChange={(e) => {
              setForm({ ...form, serviceType: e.target.value });
              setErrors((current) => ({ ...current, serviceType: undefined }));
            }}
            className={`w-full px-4 py-3.5 rounded-xl border hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right ${
              errors.serviceType ? "border-red-400" : "border-slate-200"
            }`}
          >
            <option value="">اختر الخدمة...</option>
            {services.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
          {errors.serviceType && (
            <p className="text-red-500 text-xs mt-1.5 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5" />
              {errors.serviceType}
            </p>
          )}
        </div>
      </section>
    );
  }

  // ── Date & Time ──
  function Step3() {
    return (
      <section className="space-y-4" aria-labelledby="appointment-time-title">
        <div className="flex items-center gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-sky-50">
            <CalendarDays className="h-4 w-4 text-sky-600" />
          </span>
          <div>
            <h2 id="appointment-time-title" className="font-bold text-slate-900">
              الموعد المفضل
            </h2>
            <p className="text-xs text-slate-500">اختر اليوم ثم أحد الأوقات المتاحة</p>
          </div>
        </div>

        {/* Date */}
        <div>
          <label
            htmlFor="booking-date"
            className="block text-sm font-semibold text-slate-700 mb-1.5"
          >
            التاريخ المفضل <span className="text-red-500">*</span>
          </label>
          <input
            id="booking-date"
            type="date"
            value={form.preferredDate}
            onChange={(e) => {
              setForm({
                ...form,
                preferredDate: e.target.value,
                preferredTime: "",
              });
              setErrors((current) => ({
                ...current,
                preferredDate: undefined,
                preferredTime: undefined,
              }));
            }}
            min={getTodayDate()}
            className={`w-full px-4 py-3.5 rounded-xl border transition-all duration-200 outline-none focus:ring-2 ${
              errors.preferredDate
                ? "border-red-400 bg-red-50 focus:ring-red-300"
                : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
            }`}
          />
          <div className="mt-3">
            <p className="mb-2 text-xs font-medium text-slate-500">
              أقرب أيام العمل
            </p>
            <div className="grid grid-cols-3 gap-2">
              {quickBookingDates.map((date) => (
                <button
                  key={date}
                  type="button"
                  aria-label={`اختر ${toArabicDate(date)}`}
                  aria-pressed={form.preferredDate === date}
                  onClick={() => {
                    setForm({
                      ...form,
                      preferredDate: date,
                      preferredTime: "",
                    });
                    setErrors((current) => ({
                      ...current,
                      preferredDate: undefined,
                      preferredTime: undefined,
                    }));
                  }}
                  className={`rounded-xl border px-2 py-2 text-xs font-semibold transition-colors ${
                    form.preferredDate === date
                      ? "border-orange-500 bg-orange-50 text-orange-700"
                      : "border-slate-200 bg-white text-slate-600 hover:border-sky-300 hover:bg-sky-50"
                  }`}
                >
                  {new Intl.DateTimeFormat("ar-YE", {
                    weekday: "short",
                    day: "numeric",
                    month: "numeric",
                  }).format(new Date(`${date}T00:00:00`))}
                </button>
              ))}
            </div>
          </div>
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
              <div className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-center text-sm text-amber-700">
                <div className="flex items-center justify-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  {slotsError}
                </div>
                <button
                  type="button"
                  onClick={() =>
                    void fetchAvailability(
                      form.preferredDate,
                      form.doctorId,
                      form.serviceType
                    )
                  }
                  className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 font-semibold text-amber-800 transition-colors hover:bg-amber-100"
                >
                  إعادة المحاولة
                </button>
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
                      aria-pressed={isSelected}
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
      </section>
    );
  }

  // ── Optional details ──
  function Step4() {
    return (
      <details className="group rounded-2xl border border-slate-200 bg-slate-50/70">
        <summary className="cursor-pointer list-none px-4 py-3 text-sm font-semibold text-slate-700">
          <span className="flex items-center justify-between gap-3">
            إضافة طبيب أو تفاصيل اختيارية
            <span className="text-lg leading-none text-slate-400 transition-transform group-open:rotate-45">
              +
            </span>
          </span>
        </summary>
        <div className="grid gap-4 border-t border-slate-200 p-4">
          <div>
            <label
              htmlFor="booking-doctor"
              className="block text-sm font-semibold text-slate-700 mb-1.5"
            >
              الطبيب المفضل
            </label>
            {!doctorsLoaded ? (
              <div className="flex items-center gap-2 text-slate-400 text-sm py-3">
                <Loader2 className="w-4 h-4 animate-spin" />
                جاري تحميل قائمة الأطباء...
              </div>
            ) : (
              <select
                id="booking-doctor"
                value={form.doctorId}
                onChange={(e) =>
                  setForm({
                    ...form,
                    doctorId: e.target.value,
                    preferredTime: "",
                  })
                }
                className="w-full px-4 py-3 rounded-xl border border-slate-200 bg-white text-right outline-none transition-all focus:border-[#87CEEB] focus:ring-2 focus:ring-[#87CEEB]/20"
              >
                <option value="">أي طبيب متاح</option>
                {doctors.map((doctor) => (
                  <option key={doctor.id} value={doctor.id}>
                    {doctor.name} — {doctor.specialty}
                  </option>
                ))}
              </select>
            )}
          </div>

          <div>
            <label
              htmlFor="booking-notes"
              className="block text-sm font-semibold text-slate-700 mb-1.5"
            >
              ملاحظات إضافية
            </label>
            <textarea
              id="booking-notes"
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
              rows={2}
              placeholder="مثال: استشارة تقويم أو حالة طارئة..."
              className="w-full resize-none rounded-xl border border-slate-200 bg-white px-4 py-3 text-right outline-none transition-all focus:border-[#87CEEB] focus:ring-2 focus:ring-[#87CEEB]/20"
              maxLength={500}
            />
          </div>
        </div>
      </details>
    );
  }

  // ── Main render ──
  return (
    <div className="min-h-screen bg-[#F8FAFC]">
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
              {/* eslint-disable-next-line @next/next/no-img-element */}
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
          <form
            className="space-y-6 p-5 sm:p-7"
            onSubmit={(event) => {
              event.preventDefault();
              void handleSubmit();
            }}
            noValidate
          >
            <div className="rounded-xl bg-sky-50 px-4 py-3 text-center text-sm font-medium text-sky-800">
              نموذج واحد سريع — الحجز يستغرق أقل من دقيقة
            </div>

            {Step1()}
            <div className="border-t border-slate-100" />
            {Step2()}
            <div className="border-t border-slate-100" />
            {Step3()}
            {Step4()}

            {/* Server error */}
            {serverError && (
              <div className="mt-5 space-y-3">
                <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center flex items-start justify-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{serverError}</span>
                </div>
                {isConflict && (
                  <a
                    href={`https://wa.me/${waNumber}?text=${encodeURIComponent("مرحباً، بخصوص طلب الحجز")}`}
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

            <button
              type="submit"
              disabled={loading}
              className="w-full disabled:cursor-not-allowed disabled:opacity-60 text-white font-bold py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
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

            <p className="text-center text-xs text-slate-400 mt-5">
              بإرسال هذا النموذج أنت توافق على التواصل معك لتأكيد موعدك
            </p>
          </form>
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
              href={telHref}
              className="text-white font-semibold px-4 py-3 rounded-xl text-sm transition-opacity hover:opacity-90 flex items-center justify-center gap-2"
              style={{ backgroundColor: "#87CEEB" }}
            >
              <Phone className="w-4 h-4" />
              {clinicPhone}
            </a>
            <a
              href={`https://wa.me/${waNumber}`}
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
