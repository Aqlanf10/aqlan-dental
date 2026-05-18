"use client";
import { useEffect, useState } from "react";
import { Plus, Edit3, Power, PowerOff, X, Save, DoorOpen } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

// ─── Types ────────────────────────────────────────────────────────────────────

interface RoomRow {
  id: string;
  arabicName: string;
  englishName: string;
  code: string;
  roomType: string;
  isActive: boolean;
  sortOrder: number;
}

interface RoomForm {
  arabicName: string;
  englishName: string;
  code: string;
  roomType: string;
  sortOrder: number;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const ROOM_TYPE_LABELS: Record<string, string> = {
  Treatment: "علاجية",
  Surgery: "جراحية",
  Radiology: "أشعة",
  Reception: "استقبال",
  Other: "أخرى",
};

const ROOM_TYPE_COLORS: Record<string, string> = {
  Treatment: "bg-emerald-50 text-emerald-700",
  Surgery: "bg-rose-50 text-rose-700",
  Radiology: "bg-cyan-50 text-cyan-700",
  Reception: "bg-amber-50 text-amber-700",
  Other: "bg-gray-50 text-gray-700",
};

const ROOM_TYPES = Object.keys(ROOM_TYPE_LABELS);

const EMPTY_FORM: RoomForm = {
  arabicName: "",
  englishName: "",
  code: "",
  roomType: "Treatment",
  sortOrder: 0,
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function RoomsSettingsPage() {
  const [rooms, setRooms] = useState<RoomRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<RoomForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const load = () => {
    setLoading(true);
    api
      .get<RoomRow[]>("/api/settings/rooms")
      .then((r) => setRooms(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const handleOpenAdd = () => {
    setForm(EMPTY_FORM);
    setEditingId(null);
    setFormError("");
    setShowForm(true);
  };

  const handleOpenEdit = (r: RoomRow) => {
    setForm({
      arabicName: r.arabicName,
      englishName: r.englishName,
      code: r.code,
      roomType: r.roomType,
      sortOrder: r.sortOrder,
    });
    setEditingId(r.id);
    setFormError("");
    setShowForm(true);
  };

  const handleCloseForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.arabicName.trim()) {
      setFormError("اسم الغرفة بالعربية مطلوب");
      return;
    }
    if (!form.code.trim()) {
      setFormError("كود الغرفة مطلوب");
      return;
    }

    setSaving(true);
    setFormError("");

    try {
      const payload = {
        arabicName: form.arabicName,
        englishName: form.englishName || null,
        code: form.code,
        roomType: form.roomType,
        sortOrder: form.sortOrder,
      };

      if (editingId) {
        await api.put(`/api/settings/rooms/${editingId}`, payload);
      } else {
        await api.post("/api/settings/rooms", payload);
      }

      handleCloseForm();
      load();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setFormError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const handleToggle = async (id: string, isActive: boolean) => {
    setTogglingId(id);
    try {
      if (isActive) {
        await api.patch(`/api/settings/rooms/${id}/deactivate`);
      } else {
        await api.patch(`/api/settings/rooms/${id}/activate`);
      }
      load();
    } catch {
    } finally {
      setTogglingId(null);
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link
          href="/settings"
          className="text-gray-400 hover:text-gray-700 transition text-sm"
        >
          الإعدادات
        </Link>
        <span className="text-gray-300">/</span>
        <h1 className="text-2xl font-extrabold text-gray-900">غرف العيادة</h1>
      </div>
      <p className="text-sm text-gray-500 -mt-3">إدارة الغرف وتوزيعها على الأقسام</p>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{rooms.length} غرفة</p>
        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          غرفة جديدة
        </button>
      </div>

      {/* Form Modal */}
      {showForm && (
        <form
          onSubmit={handleSubmit}
          className="bg-gray-50 rounded-xl border border-gray-200 p-5 space-y-4"
        >
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold text-gray-800">
              {editingId ? "تعديل الغرفة" : "إضافة غرفة جديدة"}
            </p>
            <button
              type="button"
              onClick={handleCloseForm}
              className="text-gray-400 hover:text-gray-700 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {formError && (
            <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{formError}</p>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            {/* Arabic Name */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الاسم بالعربية <span className="text-red-500">*</span>
              </label>
              <input
                value={form.arabicName}
                onChange={(e) => setForm({ ...form, arabicName: e.target.value })}
                className={inputCls}
                placeholder="غرفة علاج ١"
              />
            </div>
            {/* English Name */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الاسم بالإنجليزية
              </label>
              <input
                value={form.englishName}
                onChange={(e) => setForm({ ...form, englishName: e.target.value })}
                className={inputCls}
                placeholder="Treatment Room 1"
                dir="ltr"
              />
            </div>
            {/* Code */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الكود <span className="text-red-500">*</span>
              </label>
              <input
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                className={inputCls}
                placeholder="RM-001"
                dir="ltr"
              />
            </div>
            {/* Room Type */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">نوع الغرفة</label>
              <select
                value={form.roomType}
                onChange={(e) => setForm({ ...form, roomType: e.target.value })}
                className={inputCls}
              >
                {ROOM_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {ROOM_TYPE_LABELS[t]}
                  </option>
                ))}
              </select>
            </div>
            {/* Sort Order */}
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">ترتيب العرض</label>
              <input
                type="number"
                min={0}
                value={form.sortOrder}
                onChange={(e) =>
                  setForm({ ...form, sortOrder: parseInt(e.target.value) || 0 })
                }
                className={inputCls}
                dir="ltr"
              />
            </div>
          </div>

          {/* Submit */}
          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={handleCloseForm}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
            >
              <Save className="w-4 h-4" />
              {saving ? "جارٍ الحفظ..." : editingId ? "تحديث الغرفة" : "إضافة الغرفة"}
            </button>
          </div>
        </form>
      )}

      {/* Loading */}
      {loading && (
        <div className="animate-pulse space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && rooms.length === 0 && (
        <div className="text-center py-16">
          <DoorOpen className="w-12 h-12 text-gray-300 mx-auto mb-3" />
          <p className="text-gray-500 font-medium">لا توجد غرف</p>
          <p className="text-sm text-gray-400 mt-1">
            أضف أول غرفة بالضغط على زر &quot;غرفة جديدة&quot;
          </p>
        </div>
      )}

      {/* Table */}
      {!loading && rooms.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {[
                  "الاسم",
                  "الكود",
                  "نوع الغرفة",
                  "الترتيب",
                  "الحالة",
                  "إجراءات",
                ].map((h) => (
                  <th
                    key={h}
                    className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rooms.map((r) => (
                <tr key={r.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{r.arabicName}</div>
                    {r.englishName && (
                      <div className="text-xs text-gray-400 mt-0.5" dir="ltr">
                        {r.englishName}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-600" dir="ltr">
                    {r.code}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        ROOM_TYPE_COLORS[r.roomType] ?? "bg-gray-50 text-gray-700"
                      )}
                    >
                      {ROOM_TYPE_LABELS[r.roomType] ?? r.roomType}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-700">{r.sortOrder}</td>
                  <td className="px-4 py-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        r.isActive
                          ? "bg-green-50 text-green-700"
                          : "bg-gray-100 text-gray-500"
                      )}
                    >
                      {r.isActive ? "نشط" : "معطّل"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleOpenEdit(r)}
                        title="تعديل الغرفة"
                        className="text-gray-400 hover:text-clinic-blue transition p-1"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleToggle(r.id, r.isActive)}
                        disabled={togglingId === r.id}
                        title={r.isActive ? "تعطيل الغرفة" : "تفعيل الغرفة"}
                        className="text-gray-400 hover:text-gray-700 transition p-1 disabled:opacity-50"
                      >
                        {r.isActive ? (
                          <PowerOff className="w-4 h-4" />
                        ) : (
                          <Power className="w-4 h-4 text-green-600" />
                        )}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
