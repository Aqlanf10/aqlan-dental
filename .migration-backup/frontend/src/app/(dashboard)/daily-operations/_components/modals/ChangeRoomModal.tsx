/**
 * ChangeRoomModal — pick a new treatment room for a patient.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState } from "react";
import { Loader2, Building2 } from "lucide-react";
import { inputCls } from "../../_lib/constants";
import type { RoomOption } from "../../_lib/constants";
import { ModalShell } from "./ModalShell";

export function ChangeRoomModal({
  open, onClose, rooms, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  rooms: RoomOption[];
  isPending: boolean;
  onConfirm: (roomName: string) => void;
}) {
  const [roomId, setRoomId] = useState("");

  const handleSubmit = () => {
    if (!roomId) return;
    const selectedRoom = rooms.find(r => r.id === roomId);
    onConfirm(selectedRoom?.arabicName ?? roomId);
    setRoomId("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="تغيير الغرفة" icon={Building2} iconColor="#3d7ab5">
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الغرفة الجديدة *</label>
          <select value={roomId} onChange={e => setRoomId(e.target.value)} className={inputCls()}>
            <option value="">اختر الغرفة</option>
            {rooms.map(r => <option key={r.id} value={r.id}>{r.arabicName}</option>)}
          </select>
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!roomId || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: !roomId || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Building2 className="w-4 h-4" />}
          تغيير الغرفة
        </button>
      </div>
    </ModalShell>
  );
}
