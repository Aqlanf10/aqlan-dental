"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckCircle2, Loader2, ChevronRight, CalendarDays, MessageCircle, Phone, MapPin, Clock } from "lucide-react";

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

const TIMES = [
  "8:00 ص", "9:00 ص", "10:00 ص", "11:00 ص",
  "12:00 م", "1:00 م", "2:00 م", "3:00 م",
  "4:00 م", "5:00 م", "6:00 م", "7:00 م",
];

interface FormData {
  patientName: string;
  phoneNumber: string;
  email: string;
  serviceType: string;
  preferredDate: string;
  preferredTime: string;
  notes: string;
}

interface FormErrors {
  patientName?: string;
  phoneNumber?: string;
  email?: string;
}

export default function BookPage() {
  const [form, setForm] = useState<FormData>({
    patientName: "",
    phoneNumber: "",
    email: "",
    serviceType: "",
    preferredDate: "",
    preferredTime: "",
    notes: "",
  });
  const [errors, setErrors] = useState<FormErrors>({});
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [serverError, setServerError] = useState("");

  function validate(): boolean {
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

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setLoading(true);
    setServerError("");

    try {
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
        }),
      });

      if (res.ok || res.status === 201) {
        setSuccess(true);
      } else {
        setServerError("حدث خطأ أثناء إرسال طلبك. يرجى المحاولة مرة أخرى أو الاتصال بنا مباشرة.");
      }
    } catch {
      setServerError("تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت وحاول مجدداً.");
    } finally {
      setLoading(false);
    }
  }

  if (success) {
    return (
      <div dir="rtl" className="min-h-[70vh] flex items-center justify-center px-4 py-20 bg-[#F8FAFC]">
        <div className="max-w-md w-full bg-white rounded-3xl shadow-xl p-10 text-center border border-slate-100">
          {/* Badge */}
          <div
            className="inline-block text-xs font-semibold px-3 py-1 rounded-full mb-5 border"
            style={{ backgroundColor: "rgba(135,206,235,0.1)", borderColor: "rgba(135,206,235,0.3)", color: "#0284c7" }}
          >
            تم بنجاح
          </div>

          {/* Icon */}
          <div
            className="w-24 h-24 rounded-full flex items-center justify-center mx-auto mb-6 shadow-lg"
            style={{ background: "linear-gradient(135deg, #87CEEB, #0284c7)" }}
          >
            <CheckCircle2 className="w-12 h-12 text-white" />
          </div>

          <h2 className="text-2xl font-extrabold text-slate-900 mb-2">تم إرسال طلبك بنجاح!</h2>
          <p className="text-slate-400 text-sm mb-4">شكراً لاختيارك مركز د. عقلان الكامل</p>
          <p className="text-slate-500 text-sm leading-relaxed mb-8">
            سيتواصل معك فريقنا على الرقم{" "}
            <strong className="font-semibold" style={{ color: "#87CEEB" }}>{form.phoneNumber}</strong>{" "}
            لتأكيد موعد <strong className="text-slate-900">{form.patientName}</strong> في أقرب وقت ممكن.
          </p>

          <div className="flex flex-col gap-3 mb-6">
            <Link
              href="/home"
              className="text-white font-bold px-6 py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
              style={{ backgroundColor: "#FF8C00" }}
            >
              العودة للرئيسية
              <ChevronRight className="w-4 h-4" />
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
          </div>

          {/* Address line */}
          <div className="flex items-center justify-center gap-1.5 text-xs text-slate-400 border-t border-slate-100 pt-5">
            <MapPin className="w-3.5 h-3.5" style={{ color: "#FF8C00" }} />
            تعز، اليمن — شارع التحرير الأعلى
            <span className="mx-1 text-slate-200">|</span>
            <Clock className="w-3.5 h-3.5" style={{ color: "#87CEEB" }} />
            السبت – الخميس: 8 ص – 8 م
          </div>
        </div>
      </div>
    );
  }

  return (
    <div dir="rtl" className="min-h-screen bg-[#F8FAFC]">
      {/* Page Header */}
      <div className="relative text-white py-14 overflow-hidden" style={{ backgroundColor: "#0F172A" }}>
        {/* Subtle dot pattern */}
        <div
          className="absolute inset-0 opacity-5"
          style={{
            backgroundImage: "radial-gradient(circle, #87CEEB 1px, transparent 1px)",
            backgroundSize: "28px 28px",
          }}
        />
        <div className="relative max-w-2xl mx-auto px-4 text-center">
          {/* Logo */}
          <div className="flex justify-center mb-5">
            <div className="bg-white rounded-2xl p-2 shadow-sm inline-flex">
              <img src="/logo.png" alt="مركز الدكتور عقلان الكامل" className="h-10 w-auto object-contain" />
            </div>
          </div>
          {/* Icon above title */}
          <div
            className="w-14 h-14 rounded-2xl border flex items-center justify-center mx-auto mb-5"
            style={{ backgroundColor: "rgba(135,206,235,0.12)", borderColor: "rgba(135,206,235,0.2)" }}
          >
            <CalendarDays className="w-7 h-7" style={{ color: "#FF8C00" }} />
          </div>
          <h1 className="text-3xl md:text-4xl font-extrabold mb-3">احجز موعدك</h1>
          <p className="text-sm leading-relaxed max-w-lg mx-auto mb-5" style={{ color: "#94a3b8" }}>
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

      <div className="max-w-2xl mx-auto px-4 py-10">
        {/* Form card */}
        <div className="bg-white rounded-3xl shadow-xl border border-slate-100 border-t-4 border-t-[#87CEEB] overflow-hidden">
          <div className="p-8">
            <form onSubmit={handleSubmit} noValidate className="space-y-5">

              {/* ── معلومات الاتصال ── */}
              <div>
                <div className="flex items-center gap-3 mb-5">
                  <div className="flex-1 h-px bg-slate-100" />
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider whitespace-nowrap">
                    معلومات الاتصال
                  </span>
                  <div className="flex-1 h-px bg-slate-100" />
                </div>

                <div className="space-y-5">
                  {/* Name */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      الاسم الكامل <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={form.patientName}
                      onChange={(e) => setForm({ ...form, patientName: e.target.value })}
                      placeholder="أدخل اسمك الكامل"
                      className={`w-full px-4 py-3.5 rounded-xl border text-right transition-all duration-200 outline-none focus:ring-2 ${
                        errors.patientName
                          ? "border-red-400 bg-red-50 focus:ring-red-300"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.patientName && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.patientName}</p>
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
                      onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
                      placeholder="مثال: 04-253028"
                      dir="ltr"
                      className={`w-full px-4 py-3.5 rounded-xl border text-left transition-all duration-200 outline-none focus:ring-2 ${
                        errors.phoneNumber
                          ? "border-red-400 bg-red-50 focus:ring-red-300"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.phoneNumber && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.phoneNumber}</p>
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
                          ? "border-red-400 bg-red-50"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.email && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.email}</p>
                    )}
                  </div>
                </div>
              </div>

              {/* ── تفاصيل الموعد (اختياري) ── */}
              <div>
                <div className="flex items-center gap-3 mb-5 pt-2">
                  <div className="flex-1 h-px bg-slate-100" />
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider whitespace-nowrap">
                    تفاصيل الموعد (اختياري)
                  </span>
                  <div className="flex-1 h-px bg-slate-100" />
                </div>

                <div className="space-y-5">
                  {/* Service */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      نوع الخدمة{" "}
                      <span className="text-slate-400 font-normal">(اختياري)</span>
                    </label>
                    <select
                      value={form.serviceType}
                      onChange={(e) => setForm({ ...form, serviceType: e.target.value })}
                      className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right"
                    >
                      <option value="">اختر الخدمة...</option>
                      {SERVICES.map((s) => (
                        <option key={s} value={s}>{s}</option>
                      ))}
                    </select>
                  </div>

                  {/* Date & Time */}
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        التاريخ المفضل{" "}
                        <span className="text-slate-400 font-normal text-xs">(اختياري)</span>
                      </label>
                      <input
                        type="date"
                        value={form.preferredDate}
                        onChange={(e) => setForm({ ...form, preferredDate: e.target.value })}
                        min={new Date().toISOString().split("T")[0]}
                        className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200"
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        الوقت المفضل{" "}
                        <span className="text-slate-400 font-normal text-xs">(اختياري)</span>
                      </label>
                      <select
                        value={form.preferredTime}
                        onChange={(e) => setForm({ ...form, preferredTime: e.target.value })}
                        className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right"
                      >
                        <option value="">أي وقت</option>
                        {TIMES.map((t) => (
                          <option key={t} value={t}>{t}</option>
                        ))}
                      </select>
                    </div>
                  </div>

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
              </div>

              {serverError && (
                <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center">
                  {serverError}
                </div>
              )}

              {/* Submit button */}
              <button
                type="submit"
                disabled={loading}
                className="w-full disabled:opacity-60 text-white font-bold py-4 rounded-xl text-lg transition-opacity hover:opacity-90 flex items-center justify-center gap-2 shadow-md mt-2"
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
                    <ChevronRight className="w-5 h-5" />
                  </>
                )}
              </button>

              <p className="text-center text-xs text-slate-400">
                بإرسال هذا النموذج أنت توافق على التواصل معك لتأكيد موعدك
              </p>
            </form>
          </div>
        </div>

        {/* Contact help box */}
        <div className="mt-6 bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
          <div className="px-6 pt-6 pb-4 text-center border-b border-slate-100">
            <p className="text-slate-900 font-bold text-base">تفضل بالتواصل المباشر معنا</p>
            <p className="text-slate-400 text-xs mt-1">نرد عليك خلال ساعات العمل: السبت – الخميس 8 ص – 8 م</p>
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
