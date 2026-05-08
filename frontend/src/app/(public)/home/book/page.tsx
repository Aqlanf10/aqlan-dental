"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckCircle2, Loader2, ChevronRight } from "lucide-react";

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
      <div dir="rtl" className="min-h-[60vh] flex items-center justify-center px-4 py-20">
        <div className="max-w-md w-full bg-white rounded-3xl shadow-card p-10 text-center border border-gray-100">
          <div className="w-20 h-20 rounded-full bg-green-50 flex items-center justify-center mx-auto mb-6">
            <CheckCircle2 className="w-10 h-10 text-green-500" />
          </div>
          <h2 className="text-2xl font-extrabold text-clinic-navy mb-3">تم إرسال طلبك بنجاح!</h2>
          <p className="text-gray-500 text-sm leading-relaxed mb-8">
            شكراً لك <strong className="text-clinic-navy">{form.patientName}</strong>. سيتواصل معك فريقنا على الرقم <strong className="text-clinic-blue">{form.phoneNumber}</strong> لتأكيد موعدك في أقرب وقت ممكن.
          </p>
          <div className="flex flex-col gap-3">
            <Link
              href="/home"
              className="bg-clinic-blue hover:bg-blue-600 text-white font-semibold px-6 py-3 rounded-xl transition-colors"
            >
              العودة للرئيسية
            </Link>
            <a
              href="tel:04253028"
              className="border border-gray-200 text-gray-600 hover:border-clinic-blue hover:text-clinic-blue font-semibold px-6 py-3 rounded-xl transition-colors"
            >
              📞 04-253028
            </a>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div dir="rtl" className="min-h-screen bg-gray-50">
      {/* Page Header */}
      <div className="bg-clinic-navy text-white py-12">
        <div className="max-w-2xl mx-auto px-4 text-center">
          <h1 className="text-3xl font-extrabold mb-2">احجز موعدك</h1>
          <p className="text-blue-200 text-sm">أرسل لنا طلب حجز وسنتواصل معك لتأكيد الموعد</p>
          <Link href="/home" className="inline-flex items-center gap-1 text-blue-300 hover:text-white text-xs mt-4 transition-colors">
            <ChevronRight className="w-3 h-3" />
            العودة للرئيسية
          </Link>
        </div>
      </div>

      <div className="max-w-2xl mx-auto px-4 py-10">
        <div className="bg-white rounded-3xl shadow-card border border-gray-100 p-8">
          <form onSubmit={handleSubmit} noValidate className="space-y-6">
            {/* Name */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                الاسم الكامل <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={form.patientName}
                onChange={(e) => setForm({ ...form, patientName: e.target.value })}
                placeholder="أدخل اسمك الكامل"
                className={`w-full px-4 py-3 rounded-xl border text-right ${
                  errors.patientName
                    ? "border-red-400 bg-red-50 focus:ring-red-300"
                    : "border-gray-200 focus:border-clinic-blue"
                } outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all`}
              />
              {errors.patientName && (
                <p className="text-red-500 text-xs mt-1">{errors.patientName}</p>
              )}
            </div>

            {/* Phone */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                رقم الهاتف <span className="text-red-500">*</span>
              </label>
              <input
                type="tel"
                value={form.phoneNumber}
                onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
                placeholder="مثال: 04-253028"
                dir="ltr"
                className={`w-full px-4 py-3 rounded-xl border text-left ${
                  errors.phoneNumber
                    ? "border-red-400 bg-red-50 focus:ring-red-300"
                    : "border-gray-200 focus:border-clinic-blue"
                } outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all`}
              />
              {errors.phoneNumber && (
                <p className="text-red-500 text-xs mt-1">{errors.phoneNumber}</p>
              )}
            </div>

            {/* Email */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                البريد الإلكتروني <span className="text-gray-400 font-normal">(اختياري)</span>
              </label>
              <input
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                placeholder="example@email.com"
                dir="ltr"
                className={`w-full px-4 py-3 rounded-xl border text-left ${
                  errors.email
                    ? "border-red-400 bg-red-50"
                    : "border-gray-200 focus:border-clinic-blue"
                } outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all`}
              />
              {errors.email && (
                <p className="text-red-500 text-xs mt-1">{errors.email}</p>
              )}
            </div>

            {/* Service */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                نوع الخدمة <span className="text-gray-400 font-normal">(اختياري)</span>
              </label>
              <select
                value={form.serviceType}
                onChange={(e) => setForm({ ...form, serviceType: e.target.value })}
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all bg-white text-right"
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
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                  التاريخ المفضل <span className="text-gray-400 font-normal">(اختياري)</span>
                </label>
                <input
                  type="date"
                  value={form.preferredDate}
                  onChange={(e) => setForm({ ...form, preferredDate: e.target.value })}
                  min={new Date().toISOString().split("T")[0]}
                  className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all"
                />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                  الوقت المفضل <span className="text-gray-400 font-normal">(اختياري)</span>
                </label>
                <select
                  value={form.preferredTime}
                  onChange={(e) => setForm({ ...form, preferredTime: e.target.value })}
                  className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all bg-white text-right"
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
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                ملاحظات إضافية <span className="text-gray-400 font-normal">(اختياري)</span>
              </label>
              <textarea
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                rows={3}
                placeholder="أي معلومات إضافية تود إضافتها..."
                className="w-full px-4 py-3 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 transition-all resize-none text-right"
                maxLength={500}
              />
            </div>

            {serverError && (
              <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center">
                {serverError}
              </div>
            )}

            <button
              type="submit"
              disabled={loading}
              className="w-full bg-clinic-blue hover:bg-blue-600 disabled:bg-blue-300 text-white font-bold py-4 rounded-xl text-lg transition-colors flex items-center justify-center gap-2"
            >
              {loading ? (
                <>
                  <Loader2 className="w-5 h-5 animate-spin" />
                  جاري الإرسال...
                </>
              ) : (
                "إرسال طلب الحجز"
              )}
            </button>

            <p className="text-center text-xs text-gray-400">
              بإرسال هذا النموذج أنت توافق على التواصل معك لتأكيد موعدك
            </p>
          </form>
        </div>

        {/* Contact info card */}
        <div className="mt-6 bg-clinic-blue-50 rounded-2xl p-6 border border-clinic-blue-100 text-center">
          <p className="text-clinic-navy font-semibold mb-2">تفضل بالاتصال المباشر</p>
          <p className="text-gray-500 text-sm mb-4">إذا كنت تفضل التواصل الفوري</p>
          <a
            href="tel:04253028"
            className="inline-flex items-center gap-2 bg-clinic-blue text-white font-semibold px-6 py-2.5 rounded-lg text-sm hover:bg-blue-600 transition-colors"
          >
            📞 04-253028
          </a>
        </div>
      </div>
    </div>
  );
}
