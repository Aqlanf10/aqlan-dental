
import { useState, useEffect, type FormEvent } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Users, Plus, X, Download, Trash2, UserPlus, RefreshCw,
  AlertTriangle, Layers, Search,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { cn, formatArabicDate } from "@/lib/utils";
import type {
  PatientSegmentDto,
  PatientSegmentMemberDto,
  PatientSegmentsListResponse,
  CreatePatientSegmentRequest,
} from "@/types/patientSegment";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";

/* ─── Built-in segment color helpers ───────────────────────────────────────── */
// Cards use the segment.Color for the left accent stripe + the icon chip.
// Falls back to a neutral gray when color is missing (defensive — built-ins always have one).
function hexToRgba(hex: string | null | undefined, alpha: number): string {
  if (!hex) return `rgba(107,114,128,${alpha})`;
  const clean = hex.replace("#", "");
  if (clean.length !== 6) return `rgba(107,114,128,${alpha})`;
  const r = parseInt(clean.slice(0, 2), 16);
  const g = parseInt(clean.slice(2, 4), 16);
  const b = parseInt(clean.slice(4, 6), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}

/* ─── CSV export ─────────────────────────────────────────────────────────────── */
function exportMembersCsv(segment: PatientSegmentDto, members: PatientSegmentMemberDto[]) {
  const headers = ["رقم المريض", "الاسم", "الهاتف", "السبب / السياق", "تاريخ الإضافة"];
  const rows = members.map((m) => [
    m.patientNumber,
    m.fullName,
    m.phone ?? "",
    m.reason ?? "",
    new Date(m.addedAt).toLocaleDateString("ar-YE"),
  ]);
  const csv = [headers, ...rows].map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(",")).join("\n");
  // BOM (﻿) so Excel detects UTF-8 and renders Arabic correctly.
  const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `segment-${segment.key.replace(/[^a-z0-9-]/gi, "_")}-${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

/* ─── Segment Card ──────────────────────────────────────────────────────────── */
function SegmentCard({
  segment,
  onView,
  onDelete,
}: {
  segment: PatientSegmentDto;
  onView: () => void;
  onDelete?: () => void;
}) {
  return (
    <div
      className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden hover:shadow-md transition-shadow flex flex-col"
      style={{ borderRightWidth: 4, borderRightColor: segment.color ?? "#6b7280" }}
    >
      <div className="p-4 flex-1">
        <div className="flex items-start gap-3">
          <div
            className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{ background: hexToRgba(segment.color, 0.12) }}
          >
            <Layers
              className="w-5 h-5"
              style={{ color: segment.color ?? "#6b7280" }}
            />
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="font-bold text-gray-900 text-sm truncate">{segment.name}</h3>
              {segment.isBuiltIn ? (
                <span className="text-[10px] font-bold bg-cyan-50 text-cyan-700 px-1.5 py-0.5 rounded-full">
                  جاهزة
                </span>
              ) : segment.isDynamic ? (
                <span className="text-[10px] font-bold bg-purple-50 text-purple-700 px-1.5 py-0.5 rounded-full">
                  ديناميكية
                </span>
              ) : (
                <span className="text-[10px] font-bold bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded-full">
                  مخصصة
                </span>
              )}
            </div>
            {segment.description ? (
              <p className="text-xs text-gray-500 mt-1 line-clamp-2">{segment.description}</p>
            ) : null}
          </div>
        </div>
        <div className="mt-3 flex items-center gap-1.5">
          <Users className="w-4 h-4 text-gray-400" />
          <span className="text-2xl font-extrabold text-gray-900">{segment.memberCount}</span>
          <span className="text-xs text-gray-500">مريض</span>
        </div>
      </div>
      <div className="border-t border-gray-50 px-4 py-2.5 flex items-center justify-between gap-2 bg-gray-50/40">
        <button
          onClick={onView}
          className="text-xs font-bold text-cyan-700 hover:text-cyan-800 flex items-center gap-1"
        >
          <Users className="w-3.5 h-3.5" />
          عرض الأعضاء
        </button>
        {!segment.isBuiltIn && onDelete ? (
          <button
            onClick={onDelete}
            className="text-xs font-medium text-red-500 hover:text-red-700 flex items-center gap-1"
          >
            <Trash2 className="w-3.5 h-3.5" />
            حذف
          </button>
        ) : null}
      </div>
    </div>
  );
}

/* ─── Members Modal ─────────────────────────────────────────────────────────── */
function MembersModal({
  segment,
  onClose,
}: {
  segment: PatientSegmentDto;
  onClose: () => void;
}) {
  const [search, setSearch] = useState("");
  const queryClient = useQueryClient();

  const { data: members, isLoading } = useQuery({
    queryKey: ["segment-members", segment.key],
    queryFn: async () => {
      const res = await api.get<PatientSegmentMemberDto[]>(
        `/api/patient-segments/${encodeURIComponent(segment.key)}/members`
      );
      return res.data;
    },
  });

  const filtered = (members ?? []).filter((m) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      m.fullName.toLowerCase().includes(q) ||
      m.patientNumber.toLowerCase().includes(q) ||
      (m.phone ?? "").toLowerCase().includes(q)
    );
  });

  const removeMutation = useMutation({
    mutationFn: async (patientId: string) => {
      await api.delete(`/api/patient-segments/${segment.id}/members/${patientId}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["segment-members", segment.key] });
      queryClient.invalidateQueries({ queryKey: ["patient-segments"] });
      toast.success("تم حذف المريض من المجموعة");
    },
    onError: () => toast.error("فشل حذف المريض"),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-2xl mx-4 max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className="w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0"
              style={{ background: hexToRgba(segment.color, 0.12) }}
            >
              <Layers className="w-4 h-4" style={{ color: segment.color ?? "#6b7280" }} />
            </div>
            <div className="min-w-0">
              <h2 className="text-lg font-bold text-gray-900 truncate">{segment.name}</h2>
              <p className="text-xs text-gray-500">
                {members?.length ?? 0} مريض
                {segment.isBuiltIn ? " · مجموعة ديناميكية تُحسب تلقائياً" : ""}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 flex-shrink-0">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-4">
          {/* Toolbar */}
          <div className="flex items-center gap-2">
            <div className="relative flex-1">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="بحث بالاسم أو الرقم أو الهاتف..."
                className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <button
              onClick={() => members && exportMembersCsv(segment, members)}
              disabled={!members || members.length === 0}
              className="flex items-center gap-1.5 bg-green-600 text-white px-3 py-2 rounded-lg text-sm font-medium hover:bg-green-700 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              title="تصدير CSV"
            >
              <Download className="w-4 h-4" />
              <span className="hidden sm:inline">تصدير CSV</span>
            </button>
          </div>

          {/* List */}
          {isLoading ? (
            <div className="space-y-2 animate-pulse">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="h-14 bg-gray-100 rounded-lg" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="text-center py-10 text-gray-400">
              <Users className="w-8 h-8 mx-auto mb-2" />
              <p className="font-medium">
                {search.trim() ? "لا توجد نتائج مطابقة" : "لا يوجد مرضى في هذه المجموعة"}
              </p>
            </div>
          ) : (
            <div className="space-y-1.5 max-h-[50vh] overflow-y-auto">
              {filtered.map((m) => (
                <div
                  key={`${m.patientId}-${m.id}`}
                  className="flex items-center justify-between border border-gray-100 rounded-lg p-3 hover:bg-gray-50 transition-colors"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-medium text-gray-900 text-sm">{m.fullName}</span>
                      <span className="text-[10px] bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded-full font-mono">
                        #{m.patientNumber}
                      </span>
                    </div>
                    <div className="flex items-center gap-3 mt-0.5 text-xs text-gray-500">
                      {m.phone ? <span dir="ltr">{m.phone}</span> : null}
                      {m.reason ? (
                        <span className="text-gray-400 truncate">· {m.reason}</span>
                      ) : null}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0 mr-2">
                    <span className="text-[10px] text-gray-400">
                      {formatArabicDate(m.addedAt)}
                    </span>
                    {!segment.isBuiltIn && (
                      <button
                        onClick={() => {
                          if (confirm(`حذف "${m.fullName}" من المجموعة؟`)) {
                            removeMutation.mutate(m.patientId);
                          }
                        }}
                        className="text-red-400 hover:text-red-600"
                        title="حذف من المجموعة"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ─── Create Segment Modal ──────────────────────────────────────────────────── */
const SEGMENT_COLOR_PRESETS = [
  "#1a3a5c", "#3d7ab5", "#16a34a", "#f5922e",
  "#dc2626", "#6366f1", "#9333ea", "#0891b2",
];

function CreateSegmentModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<CreatePatientSegmentRequest>({
    name: "",
    description: "",
    color: SEGMENT_COLOR_PRESETS[0],
  });

  const mutation = useMutation({
    mutationFn: async (data: CreatePatientSegmentRequest) => {
      await api.post("/api/patient-segments", data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["patient-segments"] });
      toast.success("تم إنشاء المجموعة");
      onClose();
    },
    onError: () => toast.error("فشل إنشاء المجموعة"),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) {
      toast.error("اسم المجموعة مطلوب");
      return;
    }
    mutation.mutate({
      name: form.name.trim(),
      description: form.description?.trim() || undefined,
      color: form.color,
    });
  };

  const inputClass =
    "w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-md mx-4">
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-lg font-bold text-gray-900">إنشاء مجموعة مخصصة</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">اسم المجموعة *</label>
            <input
              required
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              placeholder="مثال: مرضى المتابعة الشهرية"
              className={inputClass}
            />
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">الوصف</label>
            <textarea
              value={form.description ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              placeholder="وصف مختصر للمجموعة..."
              rows={2}
              className={inputClass}
            />
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">اللون</label>
            <div className="flex items-center gap-2 flex-wrap">
              {SEGMENT_COLOR_PRESETS.map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => setForm((f) => ({ ...f, color: c }))}
                  className={cn(
                    "w-8 h-8 rounded-full transition-transform",
                    form.color === c ? "ring-2 ring-offset-2 ring-gray-400 scale-110" : "hover:scale-105"
                  )}
                  style={{ background: c }}
                  aria-label={`لون ${c}`}
                />
              ))}
            </div>
          </div>
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الحفظ..." : "إنشاء"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ─── Add Member Modal ──────────────────────────────────────────────────────── */
function AddMemberModal({
  segmentId,
  onClose,
}: {
  segmentId: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null);

  // Debounce the search input by 300ms — the /api/patients endpoint does a full
  // LIKE scan so we avoid hammering it on every keystroke.
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(t);
  }, [search]);

  const { data: searchResults, isFetching } = useQuery({
    queryKey: ["patient-search", debouncedSearch],
    queryFn: async () => {
      if (!debouncedSearch.trim()) return [];
      const params = new URLSearchParams({ search: debouncedSearch, pageSize: "10" });
      const res = await api.get<PaginatedResponse<PatientListItem>>(`/api/patients?${params}`);
      return res.data.data;
    },
    enabled: debouncedSearch.trim().length > 0,
  });

  const addMutation = useMutation({
    mutationFn: async (patientId: string) => {
      await api.post(`/api/patient-segments/${segmentId}/members`, { patientId });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["segment-members"] });
      queryClient.invalidateQueries({ queryKey: ["patient-segments"] });
      toast.success("تمت إضافة المريض إلى المجموعة");
      onClose();
    },
    onError: () => toast.error("فشل إضافة المريض"),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 max-h-[80vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">إضافة مريض للمجموعة</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6 space-y-4">
          <div className="relative">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              autoFocus
              placeholder="ابحث بالاسم أو رقم المريض..."
              className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="space-y-1.5 max-h-[40vh] overflow-y-auto">
            {isFetching ? (
              <div className="text-center py-6 text-gray-400 text-sm">جارٍ البحث...</div>
            ) : !debouncedSearch.trim() ? (
              <div className="text-center py-6 text-gray-400 text-sm">
                اكتب في خانة البحث للعثور على مريض
              </div>
            ) : (searchResults ?? []).length === 0 ? (
              <div className="text-center py-6 text-gray-400 text-sm">لا توجد نتائج</div>
            ) : (
              (searchResults ?? []).map((p) => (
                <button
                  key={p.id}
                  onClick={() => {
                    setSelectedPatientId(p.id);
                    addMutation.mutate(p.id);
                  }}
                  disabled={addMutation.isPending}
                  className={cn(
                    "w-full flex items-center justify-between border border-gray-100 rounded-lg p-3 hover:bg-cyan-50 hover:border-cyan-200 transition-colors text-right disabled:opacity-50",
                    selectedPatientId === p.id && "bg-cyan-50 border-cyan-300"
                  )}
                >
                  <div className="min-w-0">
                    <p className="font-medium text-gray-900 text-sm truncate">{p.fullName}</p>
                    <p className="text-xs text-gray-500 mt-0.5">
                      #{p.patientNumber}
                      {p.phone ? ` · ${p.phone}` : ""}
                    </p>
                  </div>
                  <UserPlus className="w-4 h-4 text-cyan-700 flex-shrink-0" />
                </button>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─── Main Page ─────────────────────────────────────────────────────────────── */
export default function PatientSegmentsPage() {
  const queryClient = useQueryClient();
  const [selectedSegment, setSelectedSegment] = useState<PatientSegmentDto | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [addMemberTo, setAddMemberTo] = useState<PatientSegmentDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PatientSegmentDto | null>(null);

  const { data, isLoading, refetch, isFetching } = useQuery({
    queryKey: ["patient-segments"],
    queryFn: async () => {
      const res = await api.get<PatientSegmentsListResponse>("/api/patient-segments");
      return res.data;
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/api/patient-segments/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["patient-segments"] });
      toast.success("تم حذف المجموعة");
      setDeleteTarget(null);
    },
    onError: () => toast.error("فشل حذف المجموعة"),
  });

  const builtIn = data?.builtIn ?? [];
  const custom  = data?.custom  ?? [];
  const totalMembers = [...builtIn, ...custom].reduce((s, x) => s + x.memberCount, 0);

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between gap-3 flex-wrap">
          <div className="flex items-center gap-3">
            <div className="w-1 h-8 rounded-full" style={{ background: "#3d7ab5" }} />
            <div>
              <h1 className="text-2xl font-extrabold" style={{ color: "#0d2137" }}>
                مجموعات المرضى
              </h1>
              <p className="text-sm mt-0.5" style={{ color: "#94a3b8" }}>
                شرائح المرضى الجاهزة والمخصصة لإدارة المتابعة والاستدعاء
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => refetch()}
              disabled={isFetching}
              className="flex items-center gap-1.5 border border-gray-200 text-gray-600 px-3 py-2 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors disabled:opacity-50"
              title="تحديث"
            >
              <RefreshCw className={cn("w-4 h-4", isFetching && "animate-spin")} />
              <span className="hidden sm:inline">تحديث</span>
            </button>
            <button
              onClick={() => setShowCreate(true)}
              className="flex items-center gap-2 bg-cyan-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
            >
              <Plus className="w-4 h-4" />
              مجموعة جديدة
            </button>
          </div>
        </div>

        {/* Summary strip */}
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">إجمالي المجموعات</p>
            <p className="text-xl font-bold text-gray-900 mt-0.5">
              {builtIn.length + custom.length}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">مجموعات جاهزة</p>
            <p className="text-xl font-bold text-cyan-700 mt-0.5">{builtIn.length}</p>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">إجمالي الأعضاء</p>
            <p className="text-xl font-bold text-gray-900 mt-0.5">{totalMembers}</p>
          </div>
        </div>

        {/* Built-in dynamic segments */}
        <section className="space-y-3">
          <div className="flex items-center gap-2">
            <h2 className="text-base font-bold text-gray-900">المجموعات الديناميكية الجاهزة</h2>
            <span className="text-[10px] font-bold bg-cyan-50 text-cyan-700 px-2 py-0.5 rounded-full">
              تُحسب تلقائياً
            </span>
          </div>
          <p className="text-xs text-gray-500 -mt-1">
            تُحدَّث لحظياً من بيانات النظام — مفيدة للمتابعة اليومية والاستدعاء.
          </p>
          {isLoading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-40 bg-gray-100 rounded-xl animate-pulse" />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {builtIn.map((s) => (
                <SegmentCard
                  key={s.key}
                  segment={s}
                  onView={() => setSelectedSegment(s)}
                />
              ))}
            </div>
          )}
        </section>

        {/* Custom segments */}
        <section className="space-y-3">
          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-gray-900">المجموعات المخصصة</h2>
              {custom.length > 0 && (
                <span className="text-[10px] font-bold bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">
                  {custom.length}
                </span>
              )}
            </div>
          </div>
          {custom.length === 0 ? (
            <div className="bg-white rounded-xl border border-dashed border-gray-200 p-8 text-center">
              <Layers className="w-8 h-8 mx-auto mb-2 text-gray-300" />
              <p className="text-sm font-medium text-gray-700">لا توجد مجموعات مخصصة بعد</p>
              <p className="text-xs text-gray-500 mt-1">
                أنشئ مجموعة لإضافة مرضى محددين يدوياً (مثال: مرضى المتابعة الشهرية).
              </p>
              <button
                onClick={() => setShowCreate(true)}
                className="mt-3 inline-flex items-center gap-1.5 bg-cyan-700 text-white px-3 py-1.5 rounded-lg text-xs font-medium hover:bg-cyan-800 transition-colors"
              >
                <Plus className="w-3.5 h-3.5" />
                إنشاء أول مجموعة
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {custom.map((s) => (
                <SegmentCard
                  key={s.id}
                  segment={s}
                  onView={() => setSelectedSegment(s)}
                  onDelete={() => setDeleteTarget(s)}
                />
              ))}
            </div>
          )}
        </section>

        {/* Low-data warning */}
        {!isLoading && totalMembers === 0 && (
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
            <div className="text-sm text-amber-800">
              <p className="font-medium">جميع المجموعات فارغة حالياً</p>
              <p className="text-amber-700 mt-0.5">
                هذا طبيعي عند بدء استخدام النظام — ستظهر بيانات المجموعات الديناميكية تلقائياً
                بمجرد تسجيل زيارات وفواتير وعقود وطلبات مختبر للمرضى.
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Floating "Add member" button when a custom segment is open */}
      {selectedSegment && !selectedSegment.isBuiltIn && (
        <button
          onClick={() => setAddMemberTo(selectedSegment)}
          className="fixed bottom-6 left-6 z-40 bg-cyan-700 text-white rounded-full shadow-lg hover:bg-cyan-800 transition-colors px-4 py-3 flex items-center gap-2 text-sm font-bold"
          title="إضافة مريض للمجموعة"
        >
          <UserPlus className="w-4 h-4" />
          إضافة مريض
        </button>
      )}

      {selectedSegment && (
        <MembersModal
          segment={selectedSegment}
          onClose={() => setSelectedSegment(null)}
        />
      )}
      {showCreate && (
        <CreateSegmentModal onClose={() => setShowCreate(false)} />
      )}
      {addMemberTo && (
        <AddMemberModal
          segmentId={addMemberTo.id}
          onClose={() => setAddMemberTo(null)}
        />
      )}
      {deleteTarget && (
        <ConfirmDialog
          open={true}
          title="حذف المجموعة"
          message={`هل أنت متأكد من حذف مجموعة "${deleteTarget.name}"؟ سيتم حذف جميع الأعضاء المرتبطين بها.`}
          confirmLabel="حذف"
          variant="danger"
          onConfirm={() => deleteMutation.mutate(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </ErrorBoundary>
  );
}
