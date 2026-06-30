/**
 * ModalShell — shared overlay wrapper used by every modal in this folder.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { X } from "lucide-react";

export function ModalShell({ open, onClose, title, icon: Icon, iconColor, children, wide }: {
  open: boolean; onClose: () => void; title: string;
  icon?: React.ElementType; iconColor?: string; children: React.ReactNode; wide?: boolean;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className={`bg-white rounded-2xl shadow-xl ${wide ? "w-full max-w-2xl" : "w-full max-w-md"} max-h-[90vh] overflow-y-auto`}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          {Icon && (
            <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: (iconColor ?? "#3d7ab5") + "15" }}>
              <Icon className="w-4.5 h-4.5" style={{ color: iconColor ?? "#3d7ab5" }} />
            </div>
          )}
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>{title}</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        {/* Body */}
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}
