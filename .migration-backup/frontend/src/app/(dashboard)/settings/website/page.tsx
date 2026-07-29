"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import {
  Save, Globe, Phone, MapPin, Clock, MessageCircle, Type,
  FileText, ExternalLink, ArrowRight, Upload, X, Loader2, ImageIcon,
} from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { upload } from "@/lib/api";

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
    title: "محتوى الصفحة الرئيسية",
    fields: [
      { key: "heroTitle", label: "عنوان البانر الرئيسي", icon: Type, placeholder: "ابتسامة تجمع بين دقة العلم ولمسة الفن" },
      { key: "heroSubtitle", label: "نص البانر الفرعي", icon: FileText, type: "textarea", placeholder: "وصف مختصر عن المركز..." },
      { key: "aboutText", label: "نص قسم عن المركز", icon: FileText, type: "textarea", placeholder: "وصف تفصيلي عن المركز..." },
      { key: "servicesSectionTitle", label: "عنوان قسم الخدمات", icon: Type, placeholder: "حلول طبية متكاملة لابتسامة صحية وواثقة" },
    ],
  },
  {
    title: "الهوية والعنوان",
    fields: [
      { key: "clinicName", label: "اسم المركز", icon: Globe, placeholder: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان" },
      { key: "marketingSlogan", label: "الشعار التسويقي", icon: Type, placeholder: "قيادة طبية… وابتسامة بثقة" },
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

// ─── Allowed image types ──────────────────────────────────────────────────────
const ALLOWED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];
const ALLOWED_IMAGE_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];
const MAX_IMAGE_SIZE = 5 * 1024 * 1024; // 5 MB

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";
const textareaCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-y min-h-[80px]";

// ─── Helper: resolve image URL ────────────────────────────────────────────────
// NAV-CEPH-FIX (Part 2): relative path → Next.js rewrite proxies /uploads/* same-origin.
function resolveImageUrl(url: string | null | undefined): string | null {
  if (!url || url.trim() === "") return null;
  if (url.startsWith("http")) return url;
  // Relative URL from backend (e.g. /uploads/xxx.webp) — stays same-origin via the rewrite.
  return url;
}

// ─── Image Upload Card Component ──────────────────────────────────────────────
function ImageUploadCard({
  title,
  uploadLabel,
  fieldKey,
  value,
  onChange,
  previewHeight = "h-36",
}: {
  title: string;
  uploadLabel: string;
  fieldKey: keyof WebsiteSettings;
  value: string;
  onChange: (key: keyof WebsiteSettings, val: string) => void;
  previewHeight?: string;
}) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState("");
  const [imgError, setImgError] = useState(false);

  const resolvedUrl = resolveImageUrl(value);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset
    setUploadError("");
    setImgError(false);

    // Validate type
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      setUploadError("نوع الملف غير مدعوم. المسموح: JPG، PNG، WEBP");
      e.target.value = "";
      return;
    }

    // Validate extension
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_IMAGE_EXTENSIONS.includes(ext)) {
      setUploadError("امتداد الملف غير مدعوم. المسموح: .jpg, .jpeg, .png, .webp");
      e.target.value = "";
      return;
    }

    // Validate size
    if (file.size > MAX_IMAGE_SIZE) {
      setUploadError("حجم الصورة يتجاوز 5 ميجابايت");
      e.target.value = "";
      return;
    }

    // Upload
    setUploading(true);
    try {
      // FE-05: Use the api.upload helper instead of fetch() so the JWT is injected
      // automatically and the 401 refresh interceptor works (previously this used
      // a manual Authorization header that broke when the token expired).
      const formData = new FormData();
      formData.append("file", file);

      const data = await upload<{ url: string }>("/api/uploads", formData);
      onChange(fieldKey, data.url);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "تعذّر رفع الصورة";
      setUploadError(message);
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  const handleRemove = () => {
    onChange(fieldKey, "");
    setImgError(false);
  };

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      {/* Card header */}
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50/60 flex items-center justify-between">
        <h2 className="text-sm font-bold text-gray-800 flex items-center gap-2">
          <ImageIcon className="w-4 h-4 text-gray-400" />
          {title}
        </h2>
        {value && (
          <button
            type="button"
            onClick={handleRemove}
            className="text-xs text-red-500 hover:text-red-700 font-medium flex items-center gap-1 transition"
          >
            <X className="w-3.5 h-3.5" />
            حذف الصورة
          </button>
        )}
      </div>

      <div className="p-5 space-y-4">
        {/* Preview */}
        {resolvedUrl && !imgError ? (
          <div className={`relative ${previewHeight} rounded-lg overflow-hidden bg-gray-50 border border-gray-200`}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={resolvedUrl}
              alt={title}
              className="w-full h-full object-contain"
              onError={() => setImgError(true)}
            />
          </div>
        ) : value && imgError ? (
          <div className={`${previewHeight} rounded-lg bg-red-50 border border-red-200 flex items-center justify-center`}>
            <span className="text-sm text-red-500 font-medium">تعذّر عرض الصورة</span>
          </div>
        ) : (
          <div className={`${previewHeight} rounded-lg bg-gray-50 border-2 border-dashed border-gray-200 flex flex-col items-center justify-center gap-2`}>
            <ImageIcon className="w-8 h-8 text-gray-300" />
            <span className="text-xs text-gray-400">لا توجد صورة</span>
          </div>
        )}

        {/* Help text */}
        <p className="text-xs text-gray-400">
          يمكنك رفع صورة من جهازك أو إدخال رابط صورة مباشر.
        </p>

        {/* Upload button */}
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-50 transition"
          >
            {uploading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                جاري رفع الصورة...
              </>
            ) : (
              <>
                <Upload className="w-4 h-4" />
                {uploadLabel}
              </>
            )}
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
            onChange={handleFileSelect}
            className="hidden"
          />
        </div>

        {/* Upload error */}
        {uploadError && (
          <p className="text-xs text-red-500 font-medium">{uploadError}</p>
        )}

        {/* URL input */}
        <div>
          <label className="text-xs font-medium text-gray-500 mb-1 block">
            رابط الصورة
          </label>
          <input
            type="text"
            value={value ?? ""}
            onChange={(e) => {
              onChange(fieldKey, e.target.value);
              setImgError(false);
            }}
            className={inputCls}
            placeholder="https://..."
            dir="ltr"
          />
        </div>
      </div>
    </div>
  );
}

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
    <div className="space-y-5 max-w-5xl">
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

      {/* ─── Image Cards (Logo + Hero) ──────────────────────────────── */}
      <ImageUploadCard
        title="الشعار"
        uploadLabel="رفع شعار من الجهاز"
        fieldKey="logoUrl"
        value={settings.logoUrl}
        onChange={handleChange}
        previewHeight="h-28"
      />

      <ImageUploadCard
        title="صورة الواجهة الرئيسية"
        uploadLabel="رفع صورة الواجهة"
        fieldKey="heroImageUrl"
        value={settings.heroImageUrl}
        onChange={handleChange}
        previewHeight="h-44"
      />

      {/* ─── Text Sections ──────────────────────────────────────────── */}
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
