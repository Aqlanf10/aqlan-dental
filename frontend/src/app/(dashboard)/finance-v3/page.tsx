"use client";

import {
  Wallet,
  FileText,
  Receipt,
  HandCoins,
  Landmark,
  Vault,
  TrendingDown,
  Truck,
  Award,
  Banknote,
  BarChart3,
  ClipboardCheck,
  AlertTriangle,
  Construction,
  ShieldX,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";

/* ─── Brand constants ───────────────────────────────────────────────────────── */
const BRAND_PRIMARY = "#1a3a5c";
const BRAND_BG = "#f8f9fb";

/* ─── Financial domain definition ──────────────────────────────────────────── */
interface FinanceDomain {
  key: string;
  label: string;
  description: string;
  icon: React.ElementType;
  color: string;
  bgLight: string;
}

const FINANCE_DOMAINS: FinanceDomain[] = [
  {
    key: "patient-accounts",
    label: "حسابات المرضى",
    description: "أرصدة المرضى، كشوف الحسابات، والمتابعة المالية",
    icon: Wallet,
    color: "#2563eb",
    bgLight: "#eff6ff",
  },
  {
    key: "invoices",
    label: "الفواتير",
    description: "إنشاء وإصدار وإدارة فواتير الخدمات العلاجية",
    icon: FileText,
    color: "#7c3aed",
    bgLight: "#f5f3ff",
  },
  {
    key: "collections",
    label: "التحصيل والإيصالات",
    description: "تسجيل المدفوعات، إصدار الإيصالات، والاسترداد",
    icon: Receipt,
    color: "#059669",
    bgLight: "#ecfdf5",
  },
  {
    key: "contracts",
    label: "العقود وخطط السداد",
    description: "عقود الأقساط، خطط السداد، والمتابعة",
    icon: HandCoins,
    color: "#d97706",
    bgLight: "#fffbeb",
  },
  {
    key: "cashier",
    label: "الصندوق اليومي",
    description: "فتح وإغلاق الصندوق، التسوية، ومتابعة العهدة",
    icon: Vault,
    color: "#dc2626",
    bgLight: "#fef2f2",
  },
  {
    key: "treasuries",
    label: "الخزائن والحسابات",
    description: "إدارة الخزائن، الحسابات البنكية، والتحويلات",
    icon: Landmark,
    color: "#0891b2",
    bgLight: "#ecfeff",
  },
  {
    key: "expenses",
    label: "المصروفات",
    description: "المصروفات التشغيلية، الاعتماد، والصرف",
    icon: TrendingDown,
    color: "#9333ea",
    bgLight: "#faf5ff",
  },
  {
    key: "suppliers",
    label: "الموردون",
    description: "فواتير الموردين، المدفوعات، وكشوف الحسابات",
    icon: Truck,
    color: "#0d9488",
    bgLight: "#f0fdfa",
  },
  {
    key: "commissions",
    label: "العمولات",
    description: "احتساب واعتماد وصرف عمولات الأطباء",
    icon: Award,
    color: "#ea580c",
    bgLight: "#fff7ed",
  },
  {
    key: "salaries",
    label: "الرواتب والسلف",
    description: "رواتب الموظفين، السلف، والخصومات",
    icon: Banknote,
    color: "#4f46e5",
    bgLight: "#eef2ff",
  },
  {
    key: "reports",
    label: "التقارير المالية",
    description: "الأرباح والخسائر، الملخص اليومي، وتقارير الحركة",
    icon: BarChart3,
    color: "#1d4ed8",
    bgLight: "#eff6ff",
  },
  {
    key: "audit",
    label: "سجل المراجعة",
    description: "سجل عمليات التعديل والإلغاء والمراجعة المالية",
    icon: ClipboardCheck,
    color: "#64748b",
    bgLight: "#f8fafc",
  },
];

/* ─── Access Denied State ──────────────────────────────────────────────────── */
function AccessDenied() {
  return (
    <div
      className="min-h-screen flex items-center justify-center"
      style={{ backgroundColor: BRAND_BG, direction: "rtl" }}
    >
      <div
        className="rounded-2xl border p-8 max-w-md text-center"
        style={{
          backgroundColor: "#fff",
          borderColor: "#fecaca",
        }}
      >
        <div
          className="w-16 h-16 rounded-full flex items-center justify-center mx-auto mb-4"
          style={{ backgroundColor: "#fef2f2" }}
        >
          <ShieldX className="w-8 h-8" style={{ color: "#dc2626" }} />
        </div>
        <h2
          className="text-xl font-bold mb-2"
          style={{ color: BRAND_PRIMARY }}
        >
          غير مصرح بالوصول
        </h2>
        <p className="text-sm" style={{ color: "#6b7280" }}>
          هذه الشاشة متاحة فقط للمسؤول والمحاسب. إذا كنت تحتاج الوصول إلى
          تسجيل التحصيل، يرجى استخدام شاشة التشغيل اليومي.
        </p>
      </div>
    </div>
  );
}

/* ─── Finance V3 Landing Page ──────────────────────────────────────────────── */
export default function FinanceV3Page() {
  const { user } = useAuthStore();

  // Enforce Admin/Accountant access or finance.view permission
  const isAuthorized =
    user?.role === "Admin" ||
    user?.role === "Accountant" ||
    hasPermission(user, PERMISSION_KEYS.PAYMENTS_VIEW);

  if (!isAuthorized) {
    return <AccessDenied />;
  }

  return (
    <div
      className="min-h-screen"
      style={{ backgroundColor: BRAND_BG, direction: "rtl" }}
    >
      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <div
        className="px-6 pt-6 pb-4"
        style={{
          background: `linear-gradient(135deg, ${BRAND_PRIMARY} 0%, #244b73 100%)`,
        }}
      >
        <div className="max-w-6xl mx-auto">
          <div className="flex items-center gap-3 mb-3">
            <div
              className="w-11 h-11 rounded-xl flex items-center justify-center"
              style={{ backgroundColor: "rgba(255,255,255,0.15)" }}
            >
              <Wallet className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-2xl font-extrabold text-white">
                المالية الجديدة
              </h1>
              <p className="text-sm" style={{ color: "rgba(255,255,255,0.6)" }}>
                وحدة المحاسبة والمراجعة المالية — قيد إعادة البناء
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ── Notice Banner ──────────────────────────────────────────────────── */}
      <div className="px-6 -mt-1">
        <div className="max-w-6xl mx-auto">
          <div
            className="rounded-xl p-4 flex items-start gap-3 border"
            style={{
              backgroundColor: "#fffbeb",
              borderColor: "#fbbf24",
            }}
          >
            <AlertTriangle
              className="w-5 h-5 flex-shrink-0 mt-0.5"
              style={{ color: "#d97706" }}
            />
            <div>
              <p className="font-bold text-sm" style={{ color: "#92400e" }}>
                تجري إعادة بناء وحدة المحاسبة والمراجعة المالية
              </p>
              <p className="text-sm mt-1" style={{ color: "#a16207" }}>
                يستمر تسجيل تحصيل المرضى من شاشة التشغيل اليومي وفق سير العمل
                المعتمد، بينما لا تستخدم هذه الشاشة الجديدة لإدخال أو تعديل
                قيود مالية مباشرة حتى اكتمال الاعتماد.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ── Under Construction Notice ──────────────────────────────────────── */}
      <div className="px-6 mt-4">
        <div className="max-w-6xl mx-auto">
          <div
            className="rounded-xl p-4 flex items-center gap-3 border"
            style={{
              backgroundColor: "#f0f9ff",
              borderColor: "#93c5fd",
            }}
          >
            <Construction
              className="w-5 h-5 flex-shrink-0"
              style={{ color: "#2563eb" }}
            />
            <div>
              <p className="font-bold text-sm" style={{ color: "#1e40af" }}>
                مراحل البناء
              </p>
              <p className="text-sm mt-1" style={{ color: "#1d4ed8" }}>
                المرحلة ١: التدقيق والتأسيس ← المرحلة ٢: دفتر الأستاذ
                والنموذج ← المرحلة ٣: الفواتير والتحصيل ← المرحلة ٤: الصندوق
                والخزائن ← المرحلة ٥: التقارير والاعتماد
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ── Domain Grid ────────────────────────────────────────────────────── */}
      <div className="px-6 py-6">
        <div className="max-w-6xl mx-auto">
          <h2
            className="text-lg font-bold mb-4"
            style={{ color: BRAND_PRIMARY }}
          >
            المجالات المالية
          </h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {FINANCE_DOMAINS.map((domain) => {
              const Icon = domain.icon;
              return (
                <div
                  key={domain.key}
                  className="rounded-xl border p-4 transition-all duration-200 cursor-default"
                  style={{
                    backgroundColor: "#fff",
                    borderColor: "#e5e7eb",
                    opacity: 0.7,
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = domain.color;
                    e.currentTarget.style.boxShadow = `0 4px 12px ${domain.color}20`;
                    e.currentTarget.style.opacity = "1";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = "#e5e7eb";
                    e.currentTarget.style.boxShadow = "none";
                    e.currentTarget.style.opacity = "0.7";
                  }}
                >
                  <div className="flex items-center gap-3 mb-3">
                    <div
                      className="w-10 h-10 rounded-lg flex items-center justify-center"
                      style={{ backgroundColor: domain.bgLight }}
                    >
                      <Icon
                        className="w-5 h-5"
                        style={{ color: domain.color }}
                      />
                    </div>
                    <h3
                      className="font-bold text-sm"
                      style={{ color: BRAND_PRIMARY }}
                    >
                      {domain.label}
                    </h3>
                  </div>
                  <p className="text-xs leading-relaxed" style={{ color: "#6b7280" }}>
                    {domain.description}
                  </p>
                  <div className="mt-3 flex items-center gap-1.5">
                    <Construction
                      className="w-3 h-3"
                      style={{ color: "#d97706" }}
                    />
                    <span
                      className="text-[11px] font-medium"
                      style={{ color: "#d97706" }}
                    >
                      قيد الإنشاء
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {/* ── Admin Info Section ─────────────────────────────────────────────── */}
      <div className="px-6 pb-6">
        <div className="max-w-6xl mx-auto">
          <div
            className="rounded-xl border p-5"
            style={{
              backgroundColor: "#fff",
              borderColor: "#e5e7eb",
            }}
          >
            <h3
              className="font-bold text-sm mb-3"
              style={{ color: BRAND_PRIMARY }}
            >
              معلومات للمسؤول والمحاسب
            </h3>
            <div className="space-y-2 text-xs" style={{ color: "#4b5563" }}>
              <p>
                • النظام المالي الحالي (V1 + V2) يحتوي على عيوب هيكلية تم
                توثيقها في مواصفات Finance V3 Foundation.
              </p>
              <p>
                • بيانات المالية الحالية هي بيانات تجريبية فقط وسيتم تنظيفها
                بعد اعتماد النظام الجديد.
              </p>
              <p>
                • يتم إعادة بناء الوحدة المالية بالكامل لضمان سلامة دفتر
                الأستاذ وحماية البيانات المالية.
              </p>
              <p>
                • الشاشات المالية القديمة (/finance و /finance-v2) تم إخفاؤها
                من القائمة الجانبية مؤقتاً، لكن المسارات المباشرة وواجهات API لا
                تزال تعمل لضمان استمرار التشغيل اليومي.
              </p>
              <p>
                • تسجيل تحصيل المرضى يستمر من شاشة التشغيل اليومي
                (/daily-operations) وفق سير العمل المعتمد ولا يتأثر بإعادة
                البناء.
              </p>
              <p>
                • للاطلاع على تفاصيل المواصفات:
                <code
                  className="px-1.5 py-0.5 rounded text-[11px] mx-1"
                  style={{
                    backgroundColor: "#f1f5f9",
                    color: "#475569",
                  }}
                >
                  docs/finance-v3/FINANCE-V3-FOUNDATION.md
                </code>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
