/**
 * BulkSmsModal — send SMS reminders to all patients who have an appointment
 * tomorrow (with multi-select).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */


import { useState, useEffect } from "react";
import { CalendarPlus, Loader2, Send, AlertCircle } from "lucide-react";
import { fmtTime, NAVY, BLUE } from "../../_lib/constants";
import type { TodayJourneyItem } from "../../_lib/constants";
import { ModalShell } from "./ModalShell";

export function BulkSmsModal({
  open, onClose, tomorrowItems, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  tomorrowItems: TodayJourneyItem[];
  isPending: boolean;
  onConfirm: (appointmentIds: string[]) => void;
}) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (open && tomorrowItems.length > 0) {
      setSelectedIds(new Set(tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id)));
    }
  }, [open, tomorrowItems]);

  const toggleItem = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    if (selectedIds.size === tomorrowItems.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id)));
    }
  };

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    onConfirm(Array.from(selectedIds));
  };

  return (
    <ModalShell open={open} onClose={onClose} title="تذكيرات مواعيد الغد" icon={Send} iconColor="#3d7ab5" wide>
      <div className="mb-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#f0f5fb" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
        <span className="text-xs font-medium" style={{ color: NAVY }}>
          سيتم إرسال رسالة تذكير لكل مريض له موعد غداً عبر SMS
        </span>
      </div>

      {tomorrowItems.length === 0 ? (
        <div className="text-center py-8" style={{ color: "#94a3b8" }}>
          <CalendarPlus className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-sm font-bold">لا توجد مواعيد غداً</p>
        </div>
      ) : (
        <>
          <div className="flex items-center gap-2 mb-3">
            <input type="checkbox" checked={selectedIds.size === tomorrowItems.length}
              onChange={toggleAll}
              className="w-4 h-4 rounded border-gray-300 accent-[#3d7ab5]" />
            <span className="text-xs font-bold" style={{ color: NAVY }}>
              تحديد الكل ({tomorrowItems.length} مريض)
            </span>
            <span className="text-[10px] mr-auto" style={{ color: "#94a3b8" }}>
              تم تحديد {selectedIds.size}
            </span>
          </div>

          <div className="max-h-[300px] overflow-y-auto space-y-1.5">
            {tomorrowItems.map(item => (
              <label key={item.appointmentId ?? item.patientId}
                className="flex items-center gap-2.5 px-3 py-2 rounded-lg cursor-pointer hover:bg-gray-50"
                style={{ background: item.appointmentId && selectedIds.has(item.appointmentId) ? "#f0f5fb" : undefined }}>
                <input type="checkbox" checked={!!item.appointmentId && selectedIds.has(item.appointmentId)}
                  onChange={() => item.appointmentId && toggleItem(item.appointmentId)}
                  className="w-4 h-4 rounded border-gray-300 accent-[#3d7ab5]" />
                <div className="flex-1">
                  <span className="text-xs font-bold" style={{ color: NAVY }}>{item.patientName}</span>
                  <span className="text-[10px] mx-1.5" style={{ color: "#94a3b8" }}>—</span>
                  <span className="text-[10px]" style={{ color: "#64748b" }}>
                    {item.doctorName} — {fmtTime(item.appointmentTime)}
                  </span>
                </div>
                {item.patientPhone && (
                  <span className="text-[10px]" style={{ color: "#94a3b8" }}>{item.patientPhone}</span>
                )}
              </label>
            ))}
          </div>
        </>
      )}

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={selectedIds.size === 0 || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: selectedIds.size === 0 || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          إرسال {selectedIds.size} تذكير
        </button>
      </div>
    </ModalShell>
  );
}
