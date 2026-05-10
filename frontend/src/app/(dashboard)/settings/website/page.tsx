"use client";

import { useEffect, useState, useCallback } from "react";
import { Save, Globe, Phone, MapPin, Clock, MessageCircle, Image, Type, FileText, ExternalLink, ArrowRight } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";

// ─── Types ────────────────────────────────────────────────────────────────────
interface WebsiteSettings {
  clinicName: string;
  heroTitle: string;
  heroSubtitle: string;
  marketingSlogan: string;
  aboutText: string;
  phone: string;
  whatsapp: string;
  address: string;
  workingHours: string;
  facebook: string;
  instagram: string;
  logoUrl: string;
  heroImageUrl: string;
  servicesSectionTitle: string;
  bookingButtonText: string;
  whatsappButtonText: string;
}

const DEFAULTS: WebsiteSettings = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  heroTitle: "ابتسامة تجمع بين دقة العلم ولمسة الفن",
  heroSubtitle: "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
  marketingSlogan: "قيادة طبية… وابتسامة بثقة",
  aboutText: "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
  phone: "04-253028",
  whatsapp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  facebook: "",
  instagram: "",
  logoUrl: "",
  heroImageUrl: "",
  servicesSectionTitle: "حلول طبية متكاملة لابتسامة صحية وواثقة",
  bookingButtonText: "احجز موعدك الآن",
  whatsappButtonText: "تواصل عبر الواتساب",
};

type FieldDef = {
  key: keyof WebsiteSettings;
  label: string;
  icon: React.ElementType;
  type?: "text" | "textarea";
  dir?: "rtl" | "ltr";
  placeholder?: string;
};

const SECTIONS: { title: string; fields: FieldDef[] }[] = [
  {
    title: "الهوية والعنوان",
    fields: [
      { key: "clinicName", label: "اسم المركز", icon: Globe, placeholder: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان" },
      { key: "marketingSlogan", label: "الشعار التسويقي", icon: Type, placeholder: "قيادة طبية… وابتسامة بثقة" },
      { key: "logoUrl", label: "رابط الشعار (Logo URL)", icon: Image, dir: "ltr", placeholder: "https://..." },
      { key: "heroImageUrl", label: "رابط صورة البانر (Hero Image URL)", icon: Image, dir: "ltr", placeholder: "https://..." },
    ],
  },
  {
    title: "محتوى الصفحة الرئيسية",
    fields: [
      { key: "heroTitle", label: "عنوان البانر الرئيسي", icon: Type, placeholder: "ابتسامة تجمع بين دقة العلم ولمسة الفن" },
      { key: "heroSubtitle", label: "نص البانر الفرعي", icon: FileText, type: "textarea", placeholder: "وصف مختصر عن المركز..." },
      { key: "aboutText", label: "نص قسم عن المركز", icon: FileText, type: "textarea", placeholder: "وصف تفصيلي عن المركز..." },
      { key: "servicesSectionTitle", label: "عنوان قسم الخدمات", icon: Type, placeholder: "حلول طبية متكاملة لابتسامة صحية وواثقة" },
    ],
  },
  {
    title: "التواصل والموقع",
    fields: [
      { key: "phone", label: "رقم الهاتف", icon: Phone, dir: "ltr", placeholder: "04-253028" },
      { key: "whatsapp", label: "رقم الواتساب (مع رمز الدولة)", icon: MessageCircle, dir: "ltr", placeholder: "967770245745" },
      { key: "address", label: "العنوان", icon: MapPin, placeholder: "تعز، اليمن — شارع التحرير الأعلى" },
      { key: "workingHours", label: "ساعات العمل", icon: Clock, placeholder: "السبت – الخميس: 8 ص – 8 م" },
    ],
  },
  {
    title: "السوشيال ميديا",
    fields: [
      { key: "facebook", label: "رابط فيسبوك", icon: Globe, dir: "ltr", placeholder: "https://facebook.com/..." },
      { key: "instagram", label: "رابط إنستغرام", icon: Globe, dir: "ltr", placeholder: "https://instagram.com/..." },
    ],
  },
  {
    title: "نصوص الأزرار",
    fields: [
      { key: "bookingButtonText", label: "نص زر الحجز", icon: Type, placeholder: "احجز موعدك الآن" },
      { key: "whatsappButtonText", label: "نص زر الواتساب", icon: MessageCircle, placeholder: "تواصل عبر الواتساب" },
    ],
  },
];

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";
const textareaCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-y min-h-[80px]";

// ─── Component ────────────────────────────────────────────────────────────────
export default function WebsiteSettingsPage() {
  const [settings, setSettings] = useState<WebsiteSettings>({ ...DEFAULTS });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  const loadSettings = useCallback(() => {
    setLoading(true);
    setError("");
    api
      .get<WebsiteSettings>("/api/settings/website")
      .then((r) => {
        const data = r.data;
        // Merge with defaults for null safety
        const merged: WebsiteSettings = { ...DEFAULTS };
        for (const key of Object.keys(DEFAULTS) as (keyof WebsiteSettings)[]) {
          merged[key] = data?.[key] ?? DEFAULTS[key];
        }
        setSettings(merged);
      })
      .catch(() => {
        // Fallback to defaults — page still works
        setSettings({ ...DEFAULTS });
      })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    loadSettings();
  }, [loadSettings]);

  const handleChange = (key: keyof WebsiteSettings, value: string) => {
    setSettings((prev) => ({ ...prev, [key]: value }));
  };

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    setError("");
    try {
      const res = await api.put<WebsiteSettings>("/api/settings/website", settings);
      const merged: WebsiteSettings = { ...DEFAULTS };
      for (const key of Object.keys(DEFAULTS) as (keyof WebsiteSettings)[]) {
        merged[key] = res.data?.[key] ?? DEFAULTS[key];
      }
      setSettings(merged);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("حدث خطأ أثناء الحفظ. حاول مرة أخرى.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-5 max-w-5xl">
        <div className="animate-pulse space-y-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5 max-w-5xl" dir="rtl">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">إعدادات الموقع</h1>
          <p className="text-sm text-gray-500 mt-0.5">تحكم بمحتوى الصفحة الرئيسية للموقع العام</p>
        </div>
        <Link
          href="/home"
          target="_blank"
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition"
        >
          <ExternalLink className="w-3.5 h-3.5" />
          معاينة الموقع
        </Link>
      </div>

      {/* Sections */}
      {SECTIONS.map((section) => (
        <div key={section.title} className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50/60">
            <h2 className="text-sm font-bold text-gray-800">{section.title}</h2>
          </div>
          <div className="p-5 space-y-4">
            {section.fields.map(({ key, label, icon: Icon, type, dir, placeholder }) => (
              <div key={key}>
                <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-1.5">
                  <Icon className="w-4 h-4 text-gray-400" />
                  {label}
                </label>
                {type === "textarea" ? (
                  <textarea
                    value={settings[key] ?? ""}
                    onChange={(e) => handleChange(key, e.target.value)}
                    className={textareaCls}
                    placeholder={placeholder}
                    dir={dir ?? "rtl"}
                    rows={3}
                  />
                ) : (
                  <input
                    type="text"
                    value={settings[key] ?? ""}
                    onChange={(e) => handleChange(key, e.target.value)}
                    className={inputCls}
                    placeholder={placeholder}
                    dir={dir ?? "rtl"}
                  />
                )}
              </div>
            ))}
          </div>
        </div>
      ))}

      {/* Save bar */}
      <div className="sticky bottom-0 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-xl shadow-sm p-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-6 py-2.5 text-sm font-bold rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition shadow-sm"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
          </button>
          {saved && (
            <span className="text-sm text-green-600 font-medium flex items-center gap-1">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              تم الحفظ بنجاح
            </span>
          )}
          {error && <span className="text-sm text-red-600 font-medium">{error}</span>}
        </div>
        <button
          onClick={() => setSettings({ ...DEFAULTS })}
          className="text-xs text-gray-400 hover:text-gray-600 transition"
        >
          استعادة الافتراضي
        </button>
      </div>

      {/* Back link */}
      <div className="pt-2">
        <Link
          href="/settings"
          className="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition"
        >
          <ArrowRight className="w-4 h-4" />
          العودة إلى الإعدادات
        </Link>
      </div>
    </div>
  );
}
