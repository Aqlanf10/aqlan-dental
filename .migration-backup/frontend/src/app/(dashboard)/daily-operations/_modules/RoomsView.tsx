"use client";

import { useEffect, useState, useCallback } from "react";
import api from "@/lib/api";
import {
  RefreshCw, Loader2, Building2, Plus, X, Edit3, Trash2,
} from "lucide-react";
import { toast } from "@/stores/toastStore";
import { NAVY, BLUE, ORANGE } from "../_lib/constants";

interface Room {
  id: string;
  arabicName: string;
  englishName?: string;
  code?: string;
  roomType?: string;
  isActive: boolean;
}

export default function RoomsView() {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [editRoom, setEditRoom] = useState<Room | null>(null);
  const [formName, setFormName] = useState("");
  const [formCode, setFormCode] = useState("");
  const [formType, setFormType] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchRooms = useCallback(async () => {
    try {
      const { data } = await api.get<Room[]>("/api/settings/rooms");
      setRooms(data);
    } catch { /* ignore */ } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchRooms(); }, [fetchRooms]);

  const handleSave = async () => {
    if (!formName.trim()) { toast.error("أدخل اسم الغرفة"); return; }
    setSaving(true);
    try {
      if (editRoom) {
        await api.put(`/api/settings/rooms/${editRoom.id}`, {
          arabicName: formName,
          code: formCode || undefined,
          roomType: formType || undefined,
        });
        toast.success("تم تحديث الغرفة");
      } else {
        await api.post("/api/settings/rooms", {
          arabicName: formName,
          code: formCode || undefined,
          roomType: formType || undefined,
        });
        toast.success("تمت إضافة الغرفة");
      }
      setShowAddModal(false);
      setEditRoom(null);
      setFormName(""); setFormCode(""); setFormType("");
      fetchRooms();
    } catch { toast.error("فشل الحفظ"); }
    finally { setSaving(false); }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("هل أنت متأكد من تعطيل هذه الغرفة؟")) return;
    try {
      await api.patch(`/api/settings/rooms/${id}/deactivate`);
      toast.success("تم تعطيل الغرفة");
      fetchRooms();
    } catch { toast.error("فشل تعطيل الغرفة"); }
  };

  const openEdit = (room: Room) => {
    setEditRoom(room);
    setFormName(room.arabicName);
    setFormCode(room.code ?? "");
    setFormType(room.roomType ?? "");
    setShowAddModal(true);
  };

  const openAdd = () => {
    setEditRoom(null);
    setFormName(""); setFormCode(""); setFormType("");
    setShowAddModal(true);
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header bar */}
      <div className="flex-shrink-0 flex items-center gap-3 px-3 py-2 border-b" style={{ borderColor: "#f1f5f9", background: "#fff" }}>
        <button onClick={openAdd}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90"
          style={{ background: ORANGE }}>
          <Plus className="w-3.5 h-3.5" />إضافة غرفة
        </button>
        <div className="flex-1" />
        <span className="text-[11px] font-bold" style={{ color: "#94a3b8" }}>{rooms.length} غرفة</span>
        <button onClick={fetchRooms} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
          <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} style={{ color: "#64748b" }} />
        </button>
      </div>

      {/* Rooms grid */}
      <div className="flex-1 overflow-auto p-3">
        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
          </div>
        ) : rooms.length === 0 ? (
          <div className="text-center py-20" style={{ color: "#94a3b8" }}>
            <Building2 className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="font-medium">لا توجد غرف مسجلة</p>
          </div>
        ) : (
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
            {rooms.map(room => (
              <div key={room.id} className="rounded-xl border p-4 hover:shadow-sm transition relative group"
                style={{ background: "#fff", borderColor: "#e5e7eb" }}>
                <div className="flex items-center justify-between mb-2">
                  <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ background: BLUE + "15" }}>
                    <Building2 className="w-5 h-5" style={{ color: BLUE }} />
                  </div>
                  <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
                    <button onClick={() => openEdit(room)} className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-gray-100" title="تعديل">
                      <Edit3 className="w-3.5 h-3.5" style={{ color: "#64748b" }} />
                    </button>
                    <button onClick={() => handleDelete(room.id)} className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-red-50" title="حذف">
                      <Trash2 className="w-3.5 h-3.5" style={{ color: "#ef4444" }} />
                    </button>
                  </div>
                </div>
                <div className="text-sm font-bold" style={{ color: NAVY }}>{room.arabicName}</div>
                {room.code && <div className="text-[11px] mt-0.5" style={{ color: "#94a3b8" }}>كود: {room.code}</div>}
                {room.roomType && <div className="text-[11px] mt-0.5" style={{ color: "#94a3b8" }}>النوع: {room.roomType}</div>}
                <div className="mt-2">
                  <span className="text-[10px] px-2 py-0.5 rounded-full font-bold"
                    style={{ background: room.isActive !== false ? "#f0fdf4" : "#fef2f2", color: room.isActive !== false ? "#16a34a" : "#ef4444" }}>
                    {room.isActive !== false ? "نشطة" : "معطلة"}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Add/Edit Modal */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setShowAddModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e => e.stopPropagation()}>
            <div className="p-5 border-b flex items-center justify-between">
              <h3 className="font-bold" style={{ color: NAVY }}>{editRoom ? "تعديل الغرفة" : "إضافة غرفة"}</h3>
              <button onClick={() => setShowAddModal(false)} className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100">
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="p-5 space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1" style={{ color: NAVY }}>اسم الغرفة (عربي) *</label>
                <input type="text" value={formName} onChange={e => setFormName(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 focus:border-[#3d7ab5] outline-none text-sm" placeholder="مثال: غرفة 1" />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1" style={{ color: NAVY }}>الكود</label>
                <input type="text" value={formCode} onChange={e => setFormCode(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 focus:border-[#3d7ab5] outline-none text-sm" placeholder="مثال: R1" />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1" style={{ color: NAVY }}>النوع</label>
                <select value={formType} onChange={e => setFormType(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 focus:border-[#3d7ab5] outline-none text-sm bg-white">
                  <option value="">— اختر —</option>
                  <option value="Treatment">علاج</option>
                  <option value="Surgery">جراحة</option>
                  <option value="Ortho">تقويم</option>
                  <option value="Consultation">استشارة</option>
                  <option value="Other">أخرى</option>
                </select>
              </div>
            </div>
            <div className="p-5 pt-0 flex gap-3">
              <button onClick={() => setShowAddModal(false)}
                className="flex-1 py-2.5 rounded-xl border border-gray-200 text-sm font-medium hover:bg-gray-50" style={{ color: "#64748b" }}>إلغاء</button>
              <button onClick={handleSave} disabled={saving}
                className="flex-1 py-2.5 rounded-xl text-white text-sm font-bold transition hover:opacity-90 disabled:opacity-50" style={{ background: BLUE }}>
                {saving ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : editRoom ? "تحديث" : "إضافة"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
