/**
 * KeyboardShortcutsHelp — modal that lists the daily-operations keyboard
 * shortcuts (rendered from the KEYBOARD_SHORTCUTS constant).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { X, Keyboard } from "lucide-react";
import { NAVY, BLUE, KEYBOARD_SHORTCUTS } from "../../_lib/constants";

export function KeyboardShortcutsHelp({ open, onClose }: {
  open: boolean; onClose: () => void;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm" onClick={e => e.stopPropagation()}>
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: `${BLUE}15` }}>
            <Keyboard className="w-4.5 h-4.5" style={{ color: BLUE }} />
          </div>
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>اختصارات لوحة المفاتيح</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        <div className="p-4 space-y-2">
          {KEYBOARD_SHORTCUTS.map(s => (
            <div key={s.keys} className="flex items-center justify-between py-1.5">
              <span className="text-xs font-medium" style={{ color: "#475569" }}>{s.description}</span>
              <kbd className="px-2 py-1 rounded text-[11px] font-bold"
                style={{ background: "#f1f5f9", color: NAVY, border: "1px solid #e2e8f0" }}>
                {s.keys}
              </kbd>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
