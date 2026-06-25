/**
 * RoomSelectDialog — Room selector for call/recall patient.
 *
 * Extracted from daily-operations/page.tsx (Sprint 11B) without behavior
 * changes. RTL Arabic. Frontend-only.
 */

"use client";

import { useState } from "react";

import {
  NAVY,
  BLUE,
} from "../_lib/constants";

export function RoomSelectDialog({ rooms, onConfirm, onCancel, loading }: {
  rooms: { id: string; arabicName: string }[];
  onConfirm: (roomName: string) => void;
  onCancel: () => void;
  loading: boolean;
}) {
  const [selectedRoom, setSelectedRoom] = useState(rooms[0]?.arabicName || "غرفة 1");
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onCancel}>
      <div className="bg-white rounded-xl shadow-xl p-5 min-w-[300px]" onClick={e => e.stopPropagation()}>
        <h3 className="text-sm font-bold mb-3" style={{ color: NAVY }}>اختر الغرفة</h3>
        <div className="grid grid-cols-3 gap-2 mb-4">
          {rooms.map(r => (
            <button key={r.id} onClick={() => setSelectedRoom(r.arabicName)}
              className="px-3 py-2 rounded-lg text-xs font-bold transition"
              style={{ background: selectedRoom === r.arabicName ? BLUE + "20" : "#f5f7fa", color: selectedRoom === r.arabicName ? BLUE : "#64748b", border: selectedRoom === r.arabicName ? `2px solid ${BLUE}` : "2px solid transparent" }}>
              {r.arabicName}
            </button>
          ))}
        </div>
        <div className="flex gap-2">
          <button onClick={() => onConfirm(selectedRoom)} disabled={loading}
            className="flex-1 py-2 rounded-lg text-xs font-bold text-white transition hover:opacity-90 disabled:opacity-50" style={{ background: BLUE }}>
            {loading ? "جاري النداء..." : "تأكيد"}
          </button>
          <button onClick={onCancel} className="flex-1 py-2 rounded-lg text-xs font-bold transition" style={{ background: "#f5f7fa", color: "#64748b" }}>
            إلغاء
          </button>
        </div>
      </div>
    </div>
  );
}
