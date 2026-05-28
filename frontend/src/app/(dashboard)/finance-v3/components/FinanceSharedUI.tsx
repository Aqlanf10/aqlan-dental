"use client";

import React from "react";
import { X, ShieldX } from "lucide-react";

/* ═══════════════════════════════════════════════════════════════════════════════
   Microsoft Fluent 2 Design Tokens
   ═══════════════════════════════════════════════════════════════════════════════ */
export const tokens = {
  /* Surface */
  bg:              "#faf9f8",
  card:            "#ffffff",
  cardHover:       "#f3f2f1",
  /* Brand */
  brand:           "#0078d4",
  brandLight:      "#deecf9",
  /* Text */
  textPrimary:     "#323130",
  textSecondary:   "#605e5c",
  textTertiary:    "#a19f9d",
  textOnBrand:     "#ffffff",
  /* Borders */
  border:          "#edebe9",
  /* Semantic */
  warningBg:       "#fff4ce",
  warningBorder:   "#ffb900",
  warningText:     "#8a6914",
  infoBg:          "#deecf9",
  infoBorder:      "#0078d4",
  infoText:        "#0b5fa5",
  successBg:       "#dff6dd",
  successBorder:   "#107c10",
  dangerBg:        "#fde7e9",
  dangerBorder:    "#d13438",
  dangerText:      "#a4262c",
  /* Shadows */
  shadow2:         "0 1.6px 3.6px rgba(0,0,0,.132), 0 .3px .9px rgba(0,0,0,.108)",
  shadow4:         "0 3.2px 7.2px rgba(0,0,0,.132), 0 .6px 1.8px rgba(0,0,0,.108)",
} as const;

/* ═══════════════════════════════════════════════════════════════════════════════
   Shared UI Primitives
   ═══════════════════════════════════════════════════════════════════════════════ */

/* ── KPI Card ── */
export function KpiCard({ label, value, sublabel, color, icon }: { label: string; value: string; sublabel?: string; color: string; icon: React.ReactNode }) {
  return (
    <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
      <div className="flex items-center gap-2 mb-2">
        <div className="w-7 h-7 rounded-md flex items-center justify-center" style={{ backgroundColor: `${color}15` }}>{icon}</div>
        <span className="text-xs font-medium" style={{ color: tokens.textTertiary }}>{label}</span>
      </div>
      <p className="text-base font-bold" style={{ color }}>{value}</p>
      {sublabel && <p className="text-[11px] mt-0.5" style={{ color: tokens.textTertiary }}>{sublabel}</p>}
    </div>
  );
}

/* ── Section header ── */
export function SectionHeader({ title, action }: { title: string; action?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between mb-4">
      <h3 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>{title}</h3>
      {action}
    </div>
  );
}

/* ── Modal wrapper ── */
export function Modal({ open, onClose, title, children, wide }: { open: boolean; onClose: () => void; title: string; children: React.ReactNode; wide?: boolean }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div
        className="relative rounded-lg shadow-2xl w-full overflow-y-auto max-h-[85vh]"
        style={{ backgroundColor: tokens.card, maxWidth: wide ? 700 : 480, boxShadow: tokens.shadow4 }}
      >
        <div className="flex items-center justify-between px-5 py-4 border-b" style={{ borderColor: tokens.border }}>
          <h3 className="text-base font-bold" style={{ color: tokens.textPrimary }}>{title}</h3>
          <button onClick={onClose} className="w-7 h-7 rounded-md flex items-center justify-center transition-colors" style={{ color: tokens.textSecondary }} onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }} onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}>
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

/* ── Confirm dialog ── */
export function ConfirmDialog({ open, onClose, onConfirm, title, message, confirmLabel, danger }: { open: boolean; onClose: () => void; onConfirm: () => void; title: string; message: string; confirmLabel?: string; danger?: boolean }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative rounded-lg shadow-2xl w-full max-w-sm p-6" style={{ backgroundColor: tokens.card, boxShadow: tokens.shadow4 }}>
        <h3 className="text-base font-bold mb-2" style={{ color: tokens.textPrimary }}>{title}</h3>
        <p className="text-sm leading-relaxed mb-5" style={{ color: tokens.textSecondary }}>{message}</p>
        <div className="flex items-center gap-3">
          <button onClick={onClose} className="flex-1 px-4 py-2 rounded-md text-sm font-medium" style={{ color: tokens.textSecondary, border: `1px solid ${tokens.border}` }}>إلغاء</button>
          <button onClick={onConfirm} className="flex-1 px-4 py-2 rounded-md text-sm font-semibold text-white" style={{ backgroundColor: danger ? tokens.dangerBorder : tokens.brand }}>{confirmLabel ?? "تأكيد"}</button>
        </div>
      </div>
    </div>
  );
}

/* ── Status badge ── */
export function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { bg: string; text: string; label: string }> = {
    Draft:           { bg: tokens.infoBg,     text: tokens.infoText,    label: "مسودة" },
    Issued:          { bg: tokens.brandLight,  text: tokens.brand,      label: "صادرة" },
    Paid:            { bg: tokens.successBg,   text: tokens.successBorder, label: "مدفوعة" },
    PartiallyPaid:   { bg: tokens.warningBg,   text: tokens.warningText, label: "مدفوعة جزئياً" },
    Cancelled:       { bg: tokens.cardHover,   text: tokens.textTertiary, label: "ملغاة" },
    Pending:         { bg: tokens.warningBg,   text: tokens.warningText, label: "قيد المراجعة" },
    Approved:        { bg: tokens.successBg,   text: tokens.successBorder, label: "معتمد" },
    Rejected:        { bg: tokens.dangerBg,    text: tokens.dangerText, label: "مرفوض" },
    Active:          { bg: tokens.successBg,   text: tokens.successBorder, label: "نشط" },
    Completed:       { bg: tokens.brandLight,  text: tokens.brand,      label: "مكتمل" },
    Open:            { bg: tokens.successBg,   text: tokens.successBorder, label: "مفتوحة" },
    Closed:          { bg: tokens.cardHover,   text: tokens.textTertiary, label: "مقفولة" },
    Overdue:         { bg: tokens.dangerBg,    text: tokens.dangerText, label: "متأخرة" },
  };
  const cfg = map[status] ?? { bg: tokens.cardHover, text: tokens.textSecondary, label: status };
  return (
    <span className="inline-flex text-[11px] font-semibold px-2 py-0.5 rounded-full" style={{ backgroundColor: cfg.bg, color: cfg.text }}>
      {cfg.label}
    </span>
  );
}

/* ── Data table ── */
export function DataTable<T>({ columns, data, onRowClick, keyField }: { columns: { key: string; label: string; render?: (row: T) => React.ReactNode }[]; data: T[]; onRowClick?: (row: T) => void; keyField: keyof T }) {
  // Zero-state safety: guard against undefined/null data arrays
  const safeData = data ?? [];
  return (
    <div className="overflow-x-auto rounded-lg border" style={{ borderColor: tokens.border }}>
      <table className="w-full text-sm">
        <thead>
          <tr style={{ backgroundColor: tokens.cardHover }}>
            {columns.map((col) => (
              <th key={col.key} className="text-right px-4 py-2.5 font-semibold text-xs" style={{ color: tokens.textSecondary }}>{col.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {safeData.map((row) => (
            <tr
              key={String(row[keyField])}
              className="transition-colors cursor-default"
              style={{ borderBottom: `1px solid ${tokens.border}` }}
              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }}
              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
            >
              {columns.map((col) => (
                <td key={col.key} className="px-4 py-2.5" style={{ color: tokens.textPrimary }}>
                  {col.render ? col.render(row) : String((row as Record<string, unknown>)[col.key] ?? "—")}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ── Loading skeleton ── */
export function LoadingSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="rounded-md h-10 animate-pulse" style={{ backgroundColor: tokens.cardHover }} />
      ))}
    </div>
  );
}

/* ── Empty state ── */
export function EmptyState({ icon: Icon, message }: { icon: React.ElementType; message: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-6">
      <div className="w-14 h-14 rounded-full flex items-center justify-center mb-4" style={{ backgroundColor: tokens.brandLight }}>
        <Icon className="w-7 h-7" style={{ color: tokens.brand }} />
      </div>
      <p className="text-sm" style={{ color: tokens.textSecondary }}>{message}</p>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Form field styles
   ═══════════════════════════════════════════════════════════════════════════════ */
export const inputStyle: React.CSSProperties = {
  width: "100%",
  border: `1px solid ${tokens.border}`,
  borderRadius: 6,
  padding: "8px 12px",
  fontSize: 14,
  outline: "none",
  color: tokens.textPrimary,
  backgroundColor: tokens.card,
};

export const labelStyle: React.CSSProperties = {
  display: "block",
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textSecondary,
  marginBottom: 4,
};

export const btnPrimary: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 20px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textOnBrand,
  backgroundColor: tokens.brand,
  border: "none",
  cursor: "pointer",
};

export const btnDanger: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 20px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 600,
  color: tokens.textOnBrand,
  backgroundColor: tokens.dangerBorder,
  border: "none",
  cursor: "pointer",
};

export const btnGhost: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 6,
  padding: "8px 16px",
  borderRadius: 6,
  fontSize: 13,
  fontWeight: 500,
  color: tokens.textSecondary,
  backgroundColor: "transparent",
  border: `1px solid ${tokens.border}`,
  cursor: "pointer",
};

/* ═══════════════════════════════════════════════════════════════════════════════
   Access Denied
   ═══════════════════════════════════════════════════════════════════════════════ */
export function AccessDenied() {
  return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: tokens.bg, direction: "rtl" }}>
      <div className="rounded-lg border p-8 max-w-md text-center" style={{ backgroundColor: tokens.card, borderColor: tokens.dangerBorder, boxShadow: tokens.shadow4 }}>
        <div className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4" style={{ backgroundColor: tokens.dangerBg }}>
          <ShieldX className="w-7 h-7" style={{ color: tokens.dangerBorder }} />
        </div>
        <h2 className="text-lg font-bold mb-2" style={{ color: tokens.textPrimary }}>غير مصرح بالوصول</h2>
        <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
          هذه الشاشة متاحة فقط للمسؤول والمحاسب. إذا كنت تحتاج الوصول إلى تسجيل التحصيل، يرجى استخدام شاشة التشغيل اليومي.
        </p>
      </div>
    </div>
  );
}
