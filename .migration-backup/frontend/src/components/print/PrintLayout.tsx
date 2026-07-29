"use client";

import { useEffect, useState } from "react";
import { Printer, ArrowRight } from "lucide-react";
import { useClinicBranding, type ClinicBranding } from "@/hooks/useClinicBranding";

// ─── Types ────────────────────────────────────────────────────────────────────

interface PrintLayoutProps {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
  /** Override branding (if already fetched by parent) */
  branding?: ClinicBranding;
  /** Back URL override (defaults to window.history) */
  backUrl?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function PrintLayout({ title, subtitle, children, branding: brandingProp, backUrl }: PrintLayoutProps) {
  const ownBranding = useClinicBranding();
  const branding = brandingProp ?? ownBranding;
  const [logoError, setLogoError] = useState(false);

  // Resolve logo: 1) from settings, 2) /logo.png fallback, 3) text only
  const logoSrc = branding.logoUrl || "/logo.png";
  const showLogo = !logoError;

  // Try /logo.png if the settings logo fails
  const handleLogoError = () => {
    if (logoSrc !== "/logo.png") {
      // Settings logo failed, try /logo.png
      setLogoError(false); // will retry with /logo.png on next render
      // Force re-render by updating branding state hack
    } else {
      // /logo.png also failed, show text only
      setLogoError(true);
    }
  };

  // Reset logoError when logoSrc changes
  useEffect(() => {
    setLogoError(false);
  }, [branding.logoUrl]);

  const handlePrint = () => {
    window.print();
  };

  const handleBack = () => {
    if (backUrl) {
      window.location.href = backUrl;
    } else {
      window.history.back();
  }
  };

  return (
    <div className="print-page" style={{ minHeight: "100vh", background: "#fff", padding: "20px" }}>
      {/* ═══ Control buttons (hidden in print) ═══ */}
      <div className="no-print flex items-center gap-3 mb-6 print:hidden">
        <button
          onClick={handlePrint}
          className="flex items-center gap-2 px-5 py-2.5 text-sm font-bold rounded-xl text-white transition"
          style={{ background: "#3d7ab5" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#2d5e8e")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#3d7ab5")}
        >
          <Printer className="w-4 h-4" />
          طباعة
        </button>
        <button
          onClick={handleBack}
          className="flex items-center gap-2 px-5 py-2.5 text-sm font-medium rounded-xl border border-[#e8f0f9] text-[#64748b] hover:bg-[#f8fafc] transition"
        >
          <ArrowRight className="w-4 h-4" />
          رجوع
        </button>
      </div>

      {/* ═══ Print Header ═══ */}
      <div className="print-header" style={{ borderBottom: "2px solid #3d7ab5", paddingBottom: "16px", marginBottom: "20px" }}>
        <div style={{ display: "flex", alignItems: "center", gap: "16px" }}>
          {/* Logo */}
          {showLogo && (
            <>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={logoSrc}
              alt="شعار المركز"
              onError={handleLogoError}
              style={{ width: "64px", height: "64px", objectFit: "contain", flexShrink: 0 }}
            />
            </>
          )}
          {/* Clinic name and report title */}
          <div style={{ flex: 1 }}>
            <h1 style={{ fontSize: "18px", fontWeight: 800, color: "#0d2137", margin: 0, lineHeight: 1.4 }}>
              {branding.clinicName}
            </h1>
            <h2 style={{ fontSize: "16px", fontWeight: 700, color: "#3d7ab5", margin: "4px 0 0 0", lineHeight: 1.4 }}>
              {title}
            </h2>
            {subtitle && (
              <p style={{ fontSize: "12px", color: "#64748b", margin: "2px 0 0 0" }}>
                {subtitle}
              </p>
            )}
          </div>
          {/* Date stamp */}
          <div style={{ textAlign: "left", fontSize: "11px", color: "#94a3b8", flexShrink: 0 }}>
            <p style={{ margin: 0 }}>تاريخ الطباعة</p>
            <p style={{ margin: 0, fontWeight: 600, color: "#0d2137" }}>
              {new Intl.DateTimeFormat("ar-YE", {
                year: "numeric",
                month: "long",
                day: "numeric",
              }).format(new Date())}
            </p>
          </div>
        </div>
      </div>

      {/* ═══ Print Content ═══ */}
      <div className="print-content" style={{ flex: 1 }}>
        {children}
      </div>

      {/* ═══ Print Footer ═══ */}
      <div className="print-footer" style={{
        marginTop: "32px",
        borderTop: "1px solid #e5e7eb",
        paddingTop: "12px",
        fontSize: "11px",
        color: "#475569",
      }}>
        <div style={{ display: "flex", flexWrap: "wrap", gap: "16px 24px", marginBottom: "8px" }}>
          {branding.phone && (
            <span>هاتف: {branding.phone}</span>
          )}
          {branding.whatsApp && (
            <span>واتساب: {branding.whatsApp}</span>
          )}
          {branding.address && (
            <span>العنوان: {branding.address}</span>
          )}
          {branding.workingHours && (
            <span>ساعات العمل: {branding.workingHours}</span>
          )}
        </div>
        <p style={{ margin: 0, fontSize: "10px", color: "#94a3b8" }}>
          {branding.footer}
        </p>
      </div>
    </div>
  );
}
