"use client";
import { useState, useEffect, useCallback, useRef } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  Search, UserPlus, Eye, Pencil,
  Download, MoreVertical, Archive, RotateCcw,
  MessageCircle, Phone, Copy, CalendarPlus, Printer,
  AlertCircle, RefreshCw, Coins, User, Activity
} from "lucide-react";
import type { PatientListItem, PatientProfile } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { GENDER_LABELS, formatPhoneForWhatsApp, normalizePhone } from "@/lib/utils";
import { isClinicalRole, isAccountantRole } from "@/lib/roles";
import { useAuthStore } from "@/stores/authStore";
import { toast } from "@/stores/toastStore";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import {
  PatientContextMenu,
  type ContextMenuPosition,
} from "@/components/patients/PatientContextMenu";

interface Doctor { id: string; name: string; }

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number | null;
  totalOutstanding: number | null;
  prescriptionsCount: number;
}

function exportCsv(patients: PatientListItem[]) {
  const headers = ["رقم المريض", "الاسم", "الجنس", "العمر", "الهاتف", "الطبيب", "تاريخ التسجيل"];
  const rows = patients.map((p) => [
    p.patientNumber, p.fullName, GENDER_LABELS[p.gender ?? ""] ?? "",
    String(p.age ?? ""), p.phone ?? "", p.primaryDoctorName ?? "",
    new Date(p.createdAt).toLocaleDateString("ar-YE"),
  ]);
  const csv = [headers, ...rows].map((r) => r.map((c) => `"${c}"`).join(",")).join("\n");
  const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a"); a.href = url; a.download = "patients.csv"; a.click();
  URL.revokeObjectURL(url);
}

export function PatientTable() {
  const router = useRouter();
  const { user } = useAuthStore();
  const isAdmin     = user?.role?.toLowerCase() === "admin";
  // isClinicalRole covers: Doctor, Orthodontist, GeneralDentist, OralSurgeon
  const isDoctor    = isClinicalRole(user?.role);
  const isAccountant = isAccountantRole(user?.role);

  const [data, setData] = useState<PaginatedResponse<PatientListItem> | null>(null);
  const [search, setSearch] = useState("");
  const [gender, setGender] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [status, setStatus] = useState<"active" | "archived" | "all">("active");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  // Split-pane selected patient state
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedProfile, setSelectedProfile] = useState<PatientProfile | null>(null);
  const [selectedSummary, setSelectedSummary] = useState<PatientSummary | null>(null);
  const [loadingDetails, setLoadingDetails] = useState(false);

  const selectedFullName = selectedProfile
    ? `${selectedProfile.firstName} ${selectedProfile.middleName ?? ""} ${selectedProfile.lastName}`.replace(/\s+/g, " ").trim()
    : "";

  // Context menu
  const [ctxMenu, setCtxMenu] = useState<{ patient: PatientListItem; position: ContextMenuPosition } | null>(null);
  // Actions row menu
  const [rowMenuId, setRowMenuId] = useState<string | null>(null);
  const rowMenuRef = useRef<HTMLDivElement>(null);

  // Confirm dialog
  const [confirm, setConfirm] = useState<{
    open: boolean;
    patient: PatientListItem | null;
    action: "archive" | "restore";
  }>({ open: false, patient: null, action: "archive" });

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  // Close row menu on outside click
  useEffect(() => {
    const handle = (e: MouseEvent) => {
      if (rowMenuRef.current && !rowMenuRef.current.contains(e.target as Node)) {
        setRowMenuId(null);
      }
    };
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, []);

  const fetchPatients = useCallback(async () => {
    setLoading(true);
    setLoadError(false);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: "20", status });
      if (search) params.set("search", search);
      if (gender) params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params}`
      );
      setData(res);
    } catch {
      setLoadError(true);
      toast.error("تعذر تحميل بيانات المرضى حالياً");
    }
    setLoading(false);
  }, [page, search, gender, doctorId, status]);

  useEffect(() => {
    const timer = setTimeout(fetchPatients, 300);
    return () => clearTimeout(timer);
  }, [fetchPatients]);

  useEffect(() => { setPage(1); }, [search, gender, doctorId, status]);

  // Load details on selection change
  useEffect(() => {
    if (!selectedId) {
      setSelectedProfile(null);
      setSelectedSummary(null);
      return;
    }
    setLoadingDetails(true);
    Promise.all([
      api.get<PatientProfile>(`/api/patients/${selectedId}`),
      api.get<PatientSummary>(`/api/patients/${selectedId}/summary`).catch(() => ({ data: null })),
    ])
      .then(([profileRes, summaryRes]) => {
        setSelectedProfile(profileRes.data);
        if (summaryRes.data) {
          setSelectedSummary(summaryRes.data);
        } else {
          setSelectedSummary(null);
        }
      })
      .catch(() => {
        toast.error("تعذر تحميل تفاصيل المريض المختار");
      })
      .finally(() => {
        setLoadingDetails(false);
      });
  }, [selectedId]);

  // Auto-select first item when list changes
  useEffect(() => {
    if (data?.data && data.data.length > 0) {
      const exists = data.data.some((p) => p.id === selectedId);
      if (!exists) {
        setSelectedId(data.data[0].id);
      }
    } else {
      setSelectedId(null);
    }
  }, [data, selectedId]);

  const handleExport = async () => {
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "1000", status });
      if (search) params.set("search", search);
      if (gender) params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(`/api/patients?${params}`);
      exportCsv(res.data);
    } catch { /* ignore */ }
  };

  const handleContextMenu = (e: React.MouseEvent, patient: PatientListItem) => {
    e.preventDefault();
    setCtxMenu({ patient, position: { x: e.clientX, y: e.clientY } });
  };

  const doArchive = async (patient: PatientListItem) => {
    try {
      await api.put(`/api/patients/${patient.id}/archive`);
      await fetchPatients();
    } catch (e) { console.error("[Patients] Failed to archive patient:", e); }
  };

  const doRestore = async (patient: PatientListItem) => {
    try {
      await api.put(`/api/patients/${patient.id}/restore`);
      await fetchPatients();
    } catch (e) { console.error("[Patients] Failed to restore patient:", e); }
  };

  const handleConfirm = () => {
    if (!confirm.patient) return;
    if (confirm.action === "archive") doArchive(confirm.patient);
    else doRestore(confirm.patient);
    setConfirm({ open: false, patient: null, action: "archive" });
  };

  return (
    <div className="space-y-5" dir="rtl">
      {/* Microsoft-Style Top Command Bar */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] p-4 space-y-3 shadow-sm">
        {/* Row 1: Search & Primary Actions */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          {/* Right: Integrated Search */}
          <div className="relative flex-1 min-w-[280px] max-w-sm">
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث برقم المريض، الاسم، الهاتف أو الواتساب..."
              className="w-full h-10 pe-10 ps-4 text-sm rounded-lg outline-none transition-all duration-200"
              style={{
                border: "1.5px solid #dce8f5",
                background: "#f8fafc",
                color: "#0d2137",
                direction: "rtl",
                fontFamily: "Tajawal",
              }}
              onFocus={(e) => (e.target.style.borderColor = "#3d7ab5")}
              onBlur={(e) => (e.target.style.borderColor = "#dce8f5")}
            />
            <span className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: "#94a3b8" }}>
              <Search className="w-4 h-4" />
            </span>
          </div>

          {/* Left: Command Group */}
          <div className="flex flex-wrap items-center gap-2">
            <Link
              href="/patients/new"
              className="h-10 flex items-center gap-2 px-4 text-white text-sm font-bold rounded-lg transition-all duration-200"
              style={{ background: "#3d7ab5" }}
              onMouseEnter={(e) => (e.currentTarget.style.background = "#2d5e8e")}
              onMouseLeave={(e) => (e.currentTarget.style.background = "#3d7ab5")}
            >
              <UserPlus className="w-4 h-4" />
              مريض جديد
            </Link>

            <button
              disabled={!selectedId}
              onClick={() => selectedId && router.push(`/patients/${selectedId}/edit`)}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Pencil className="w-4 h-4 text-slate-500" />
              تعديل البيانات
            </button>

            <button
              disabled={!selectedId}
              onClick={() => selectedId && router.push(`/patients/${selectedId}`)}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Eye className="w-4 h-4 text-slate-500" />
              فتح الملف الكامل
            </button>

            <button
              disabled={!selectedId}
              onClick={() => selectedId && router.push(`/appointments/new?patientId=${selectedId}`)}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <CalendarPlus className="w-4 h-4 text-slate-500" />
              موعد جديد
            </button>

            <button
              onClick={() => {
                if (selectedProfile) {
                  router.push(`/daily-operations?search=${encodeURIComponent(selectedFullName)}`);
                } else {
                  router.push("/daily-operations");
                }
              }}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50"
            >
              <Activity className="w-4 h-4 text-slate-500" />
              التشغيل اليومي
            </button>

            <button
              disabled={!selectedId}
              onClick={() => selectedId && router.push(`/patients/${selectedId}/print/summary`)}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Printer className="w-4 h-4 text-slate-500" />
              طباعة ملف المريض
            </button>

            <button
              onClick={handleExport}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50"
            >
              <Download className="w-4 h-4 text-slate-500" />
              تصدير
            </button>
          </div>
        </div>

        {/* Row 2: Secondary Compact Filters */}
        <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-slate-100">
          <span className="text-xs font-bold text-slate-400 ml-2">تصفية القائمة:</span>
          {/* Status filter */}
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as "active" | "archived" | "all")}
            className="h-9 px-3 text-[13px] font-semibold rounded-lg outline-none transition"
            style={{ border: "1.5px solid #dce8f5", background: "#fff", color: "#64748b", fontFamily: "Tajawal" }}
          >
            <option value="active">النشطون</option>
            {isAdmin && <option value="archived">المؤرشفون</option>}
            {isAdmin && <option value="all">الكل</option>}
          </select>
          {/* Gender filter */}
          <select
            value={gender} onChange={(e) => setGender(e.target.value)}
            className="h-9 px-3 text-[13px] font-semibold rounded-lg outline-none transition"
            style={{ border: "1.5px solid #dce8f5", background: "#fff", color: "#64748b", fontFamily: "Tajawal" }}
          >
            <option value="">الجنسان</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
          {/* Doctor filter */}
          {doctors.length > 0 && (
            <select
              value={doctorId} onChange={(e) => setDoctorId(e.target.value)}
              className="h-9 px-3 text-[13px] font-semibold rounded-lg outline-none transition"
              style={{ border: "1.5px solid #dce8f5", background: "#fff", color: "#64748b", fontFamily: "Tajawal" }}
            >
              <option value="">كل الأطباء</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}
        </div>
      </div>

      {/* Split-Pane Core Workspace Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        {/* Right side: Patient Datagrid List (2/3 width) */}
        <div
          className="lg:col-span-2 rounded-xl overflow-hidden"
          style={{
            background: "#fff",
            boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
            border: "1px solid #e8f0f9",
          }}
        >
          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <thead>
                <tr style={{ background: "#f7fafd", borderBottom: "2px solid #e8f0f9" }}>
                  {["رقم الملف", "اسم المريض", "العمر", "الجنس", "الطبيب المعالج", "آخر زيارة", "الحالة", ""].map(h => (
                    <th key={h} className="px-4 py-2.5 text-start text-xs font-bold whitespace-nowrap" style={{ color: "#64748b" }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  Array.from({ length: 5 }).map((_, i) => (
                    <tr key={i}>
                      {Array.from({ length: 8 }).map((_, j) => (
                        <td key={j} className="px-4 py-3">
                          <div className="h-4 rounded animate-pulse w-20" style={{ background: "#f1f5f9" }} />
                        </td>
                      ))}
                    </tr>
                  ))
                ) : loadError ? (
                  <tr>
                    <td colSpan={8} className="text-center py-12">
                      <div className="flex flex-col items-center gap-3">
                        <AlertCircle className="w-10 h-10" style={{ color: "#ef4444" }} />
                        <p className="text-sm font-semibold" style={{ color: "#ef4444" }}>تعذر تحميل بيانات المرضى حالياً</p>
                        <button
                          onClick={fetchPatients}
                          className="flex items-center gap-1.5 px-4 py-2 text-sm font-semibold rounded-lg transition"
                          style={{ border: "1.5px solid #dce8f5", background: "#fff", color: "#3d7ab5" }}
                          onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                          onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
                        >
                          <RefreshCw className="w-3.5 h-3.5" />
                          إعادة المحاولة
                        </button>
                      </div>
                    </td>
                  </tr>
                ) : !data?.data.length ? (
                  <tr>
                    <td colSpan={8} className="text-center py-12" style={{ color: "#94a3b8" }}>
                      {search ? "لا توجد نتائج مطابقة" : status === "archived" ? "لا يوجد مرضى مؤرشفون" : "لا يوجد مرضى بعد"}
                    </td>
                  </tr>
                ) : (
                  data.data.map((p) => (
                    <tr
                      key={p.id}
                      onContextMenu={(e) => handleContextMenu(e, p)}
                      className="transition cursor-pointer"
                      style={{
                        borderBottom: "1px solid #f1f5f9",
                        opacity: p.isActive ? 1 : 0.6,
                        background: selectedId === p.id ? "#3d7ab512" : "transparent",
                      }}
                      onClick={() => setSelectedId(p.id)}
                      onDoubleClick={() => router.push(`/patients/${p.id}`)}
                      onMouseEnter={(e) => {
                        if (selectedId !== p.id) {
                          e.currentTarget.style.background = "#f7fafd";
                        }
                      }}
                      onMouseLeave={(e) => {
                        if (selectedId !== p.id) {
                          e.currentTarget.style.background = "transparent";
                        }
                      }}
                    >
                      <td className="px-4 py-3 font-bold" style={{ color: "#3d7ab5" }}>
                        {p.patientNumber}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2.5">
                          <div
                            className="w-[30px] h-[30px] rounded-full flex items-center justify-center text-[13px] font-extrabold flex-shrink-0"
                            style={{ background: "#3d7ab518", color: "#3d7ab5" }}
                          >
                            {p.fullName.charAt(0)}
                          </div>
                          <span className="font-semibold" style={{ color: "#0d2137" }}>{p.fullName}</span>
                        </div>
                      </td>
                      <td className="px-4 py-3" style={{ color: "#64748b" }}>{p.age ?? "—"}</td>
                      <td className="px-4 py-3" style={{ color: "#64748b" }}>{GENDER_LABELS[p.gender ?? ""] ?? "—"}</td>
                      <td className="px-4 py-3" style={{ color: "#64748b", fontSize: 12 }}>{p.primaryDoctorName ?? "—"}</td>
                      <td className="px-4 py-3" style={{ color: "#94a3b8", fontSize: 12 }}>
                        {p.lastVisitDate ? new Date(p.lastVisitDate).toLocaleDateString("ar-YE") : "—"}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className="text-[12px] px-2.5 py-0.5 rounded-full font-semibold"
                          style={{
                            color: p.isActive ? "#22c55e" : "#94a3b8",
                            background: p.isActive ? "#22c55e18" : "#94a3b818",
                          }}
                        >
                          {p.isActive ? "نشط" : "مؤرشف"}
                        </span>
                      </td>
                      <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                        <div className="flex items-center gap-1.5">
                          <button
                            onClick={() => router.push(`/patients/${p.id}`)}
                            className="px-2 py-1 rounded-md text-[11px] font-semibold transition"
                            style={{ border: "1px solid #dce8f5", background: "#fff", color: "#3d7ab5" }}
                          >
                            عرض
                          </button>
                          {/* ⋮ More Actions Dropdown */}
                          <div className="relative" ref={rowMenuId === p.id ? rowMenuRef : null}>
                            <button
                              onClick={() => setRowMenuId(rowMenuId === p.id ? null : p.id)}
                              className="p-1 rounded-lg transition"
                              style={{ color: "#94a3b8" }}
                            >
                              <MoreVertical className="w-4 h-4" />
                            </button>
                            {rowMenuId === p.id && (
                              <div
                                className="absolute left-0 top-8 z-30 bg-white rounded-xl py-1 min-w-44 text-sm"
                                dir="rtl"
                                style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
                              >
                                <button
                                  onClick={() => { setRowMenuId(null); router.push(`/patients/${p.id}`); }}
                                  className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                  style={{ color: "#0d2137" }}
                                  onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                >
                                  <Eye className="w-4 h-4" style={{ color: "#64748b" }} /> عرض الملف
                                </button>
                                {p.isActive && (
                                  <button
                                    onClick={() => { setRowMenuId(null); router.push(`/patients/${p.id}/edit`); }}
                                    className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <Pencil className="w-4 h-4" style={{ color: "#64748b" }} /> تعديل البيانات
                                  </button>
                                )}
                                {p.isActive && (
                                  <button
                                    onClick={() => { setRowMenuId(null); router.push(`/appointments/new?patientId=${p.id}`); }}
                                    className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <CalendarPlus className="w-4 h-4" style={{ color: "#64748b" }} /> موعد جديد
                                  </button>
                                )}
                                {p.phone && p.isActive && (
                                  <a
                                    href={`https://wa.me/${formatPhoneForWhatsApp(p.phone)}`}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onClick={() => setRowMenuId(null)}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <MessageCircle className="w-4 h-4" style={{ color: "#22c55e" }} /> واتساب
                                  </a>
                                )}
                                {p.phone && p.isActive && (
                                  <a
                                    href={`tel:${normalizePhone(p.phone)}`}
                                    className="flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onClick={() => setRowMenuId(null)}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <Phone className="w-4 h-4" style={{ color: "#3d7ab5" }} /> اتصال هاتفي
                                  </a>
                                )}
                                {p.phone && p.isActive && (
                                  <button
                                    onClick={() => { navigator.clipboard.writeText(p.phone ?? "").catch(() => {}); setRowMenuId(null); toast.success("تم نسخ رقم الهاتف"); }}
                                    className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <Copy className="w-4 h-4" style={{ color: "#64748b" }} /> نسخ الرقم
                                  </button>
                                )}
                                {p.isActive && (
                                  <button
                                    onClick={() => { setRowMenuId(null); router.push(`/patients/${p.id}/print/summary`); }}
                                    className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                    style={{ color: "#0d2137" }}
                                    onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                                    onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                  >
                                    <Printer className="w-4 h-4" style={{ color: "#64748b" }} /> طباعة ملف المريض
                                  </button>
                                )}
                                {isAdmin && (
                                  <>
                                    <div className="h-px my-1" style={{ background: "#f1f5f9" }} />
                                    {p.isActive ? (
                                      <button
                                        onClick={() => { setRowMenuId(null); setConfirm({ open: true, patient: p, action: "archive" }); }}
                                        className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                        style={{ color: "#ef4444" }}
                                        onMouseEnter={(e) => (e.currentTarget.style.background = "#fef2f2")}
                                        onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                      >
                                        <Archive className="w-4 h-4" /> أرشفة المريض
                                      </button>
                                    ) : (
                                      <button
                                        onClick={() => { setRowMenuId(null); setConfirm({ open: true, patient: p, action: "restore" }); }}
                                        className="w-full flex items-center gap-2 px-3 py-2 transition text-start"
                                        style={{ color: "#22c55e" }}
                                        onMouseEnter={(e) => (e.currentTarget.style.background = "#f0fdf4")}
                                        onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                                      >
                                        <RotateCcw className="w-4 h-4" /> استعادة المريض
                                      </button>
                                    )}
                                  </>
                                )}
                              </div>
                            )}
                          </div>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between px-5 py-3" style={{ borderTop: "1px solid #f1f5f9" }}>
              <span className="text-xs" style={{ color: "#94a3b8" }}>
                عرض {(page - 1) * 20 + 1}–{Math.min(page * 20, data.totalCount)} من {data.totalCount}
              </span>
              <div className="flex items-center gap-1.5">
                {[...Array(Math.min(data.totalPages, 3))].map((_, n) => (
                  <button
                    key={n + 1}
                    onClick={() => setPage(n + 1)}
                    className="w-7 h-7 rounded-md text-xs font-semibold transition"
                    style={{
                      border: "1px solid #dce8f5",
                      background: page === n + 1 ? "#3d7ab5" : "#fff",
                      color: page === n + 1 ? "#fff" : "#64748b",
                    }}
                  >
                    {n + 1}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Left side: Selected Patient Details Card (1/3 width) */}
        <div
          className="rounded-xl overflow-hidden flex flex-col transition-all duration-300"
          style={{
            background: "#fff",
            boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
            border: "1px solid #e8f0f9",
            minHeight: "450px",
          }}
        >
          <div className="px-5 py-4 border-b border-[#e8f0f9] bg-[#f7fafd] flex items-center justify-between">
            <h3 className="font-bold text-[#0d2137] text-sm">تفاصيل المريض المختار</h3>
            {selectedId && (
              <span className="text-[11px] font-semibold bg-[#3d7ab515] text-[#3d7ab5] px-2 py-0.5 rounded-full">
                ملف رقم {selectedProfile?.patientNumber || "..."}
              </span>
            )}
          </div>

          {loadingDetails ? (
            <div className="flex-1 p-6 flex flex-col justify-center items-center space-y-4 animate-pulse">
              <div className="w-16 h-16 bg-slate-100 rounded-full" />
              <div className="h-4 bg-slate-100 rounded w-1/2" />
              <div className="h-3 bg-slate-100 rounded w-1/3" />
              <div className="w-full space-y-3 pt-6">
                <div className="h-8 bg-slate-100 rounded" />
                <div className="h-8 bg-slate-100 rounded" />
                <div className="h-8 bg-slate-100 rounded" />
              </div>
            </div>
          ) : !selectedId ? (
            <div className="flex-1 p-6 flex flex-col justify-center items-center text-center space-y-3 text-slate-400">
              <div className="w-14 h-14 bg-slate-50 border border-dashed border-slate-200 rounded-full flex items-center justify-center">
                <User className="w-7 h-7 text-slate-300" />
              </div>
              <p className="text-sm font-semibold">لم يتم تحديد مريض</p>
              <p className="text-xs">اختر مريضاً من القائمة لعرض تفاصيل ملفه السري والعمليات السريعة.</p>
            </div>
          ) : selectedProfile ? (
            <div className="p-5 flex-1 flex flex-col justify-between space-y-4 overflow-y-auto">
              <div className="space-y-4">
                <div className="flex items-center gap-3">
                  <div
                    className="w-14 h-14 bg-gradient-to-br from-sky-500 to-indigo-600 rounded-2xl flex items-center justify-center font-black text-white text-lg shadow-md"
                  >
                    {(selectedProfile.firstName[0] ?? "") + (selectedProfile.lastName[0] ?? "")}
                  </div>
                  <div>
                    <h4 className="font-extrabold text-base text-[#0d2137]">
                      {`${selectedProfile.firstName} ${selectedProfile.middleName ?? ""} ${selectedProfile.lastName}`.replace(/\s+/g, " ").trim()}
                    </h4>
                    <p className="text-xs text-slate-400 mt-1">
                      تاريخ التسجيل: {new Date(selectedProfile.createdAt).toLocaleDateString("ar-YE")}
                    </p>
                  </div>
                </div>

                {/* Outstanding Balance (Finance authorized roles only) */}
                {!isDoctor && selectedSummary && (
                  <div
                    className="group relative bg-gradient-to-br from-orange-50 to-amber-50 border border-orange-200 rounded-xl p-3 text-center shadow-sm hover:shadow-md transition-all duration-300 cursor-pointer"
                    onClick={() => router.push(`/patients/${selectedProfile.id}?tab=finance`)}
                  >
                    <span className="block text-[10px] text-orange-600 font-bold uppercase mb-1 tracking-wider">
                      <Coins className="inline w-3.5 h-3.5 ml-1" /> الرصيد المستحق
                    </span>
                    <span className="text-xl font-black text-orange-700" dir="ltr">
                      {(selectedSummary.totalOutstanding ?? 0).toLocaleString()} ر.ي
                    </span>
                  </div>
                )}

                {/* Medical Alerts Banner (Accountant blocked) */}
                {!isAccountant && (selectedProfile.medicalHistory?.chronicDiseases || selectedProfile.medicalHistory?.drugAllergies) && (
                  <div
                    className="bg-gradient-to-br from-red-50 to-rose-50 border border-red-100 p-3 rounded-xl hover:shadow-md transition-all duration-300 cursor-pointer"
                    onClick={() => router.push(`/patients/${selectedProfile.id}?tab=medical`)}
                  >
                    <span className="block text-[10px] text-red-500 font-bold mb-1.5 tracking-wider">
                      <AlertCircle className="inline w-3.5 h-3.5 ml-1" /> تنبيهات طبية هامة
                    </span>
                    <div className="flex flex-wrap gap-1">
                      {selectedProfile.medicalHistory.chronicDiseases && (
                        <span className="bg-red-500 text-white text-[11px] font-bold px-2 py-0.5 rounded-md">
                          {selectedProfile.medicalHistory.chronicDiseases}
                        </span>
                      )}
                      {selectedProfile.medicalHistory.drugAllergies && (
                        <span className="bg-amber-500 text-white text-[11px] font-bold px-2 py-0.5 rounded-md">
                          حساسية: {selectedProfile.medicalHistory.drugAllergies}
                        </span>
                      )}
                    </div>
                  </div>
                )}

                {/* Demographics & Contact details */}
                <div className="bg-[#f8fafc] border border-slate-100 rounded-xl p-3.5 space-y-2.5 text-xs text-slate-600">
                  <div className="flex justify-between items-center">
                    <span className="font-semibold text-slate-400">الجنس / العمر:</span>
                    <span className="font-bold text-[#0d2137]">
                      {GENDER_LABELS[selectedProfile.gender ?? ""] ?? "—"} {selectedProfile.age ? `· ${selectedProfile.age} سنة` : ""}
                    </span>
                  </div>
                  {selectedProfile.phone && (
                    <div className="flex justify-between items-center">
                      <span className="font-semibold text-slate-400">الهاتف:</span>
                      <span className="font-bold font-mono text-[#0d2137] flex items-center gap-1.5">
                        {selectedProfile.phone}
                        <a
                          href={`https://wa.me/${formatPhoneForWhatsApp(selectedProfile.phone)}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="w-5 h-5 rounded flex items-center justify-center hover:bg-green-50 text-green-500"
                          title="واتساب"
                        >
                          <MessageCircle className="w-3.5 h-3.5" />
                        </a>
                      </span>
                    </div>
                  )}
                  {selectedProfile.whatsApp && (
                    <div className="flex justify-between items-center">
                      <span className="font-semibold text-slate-400">الواتساب:</span>
                      <span className="font-bold font-mono text-[#0d2137] flex items-center gap-1.5">
                        {selectedProfile.whatsApp}
                        <a
                          href={`https://wa.me/${formatPhoneForWhatsApp(selectedProfile.whatsApp)}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="w-5 h-5 rounded flex items-center justify-center hover:bg-green-50 text-green-500"
                        >
                          <MessageCircle className="w-3.5 h-3.5" />
                        </a>
                      </span>
                    </div>
                  )}
                  <div className="flex justify-between items-center">
                    <span className="font-semibold text-slate-400">الطبيب المعالج:</span>
                    <span className="font-bold text-[#0d2137]">{selectedProfile.primaryDoctorName || "—"}</span>
                  </div>
                  {selectedProfile.address && (
                    <div className="flex justify-between items-start">
                      <span className="font-semibold text-slate-400 flex-shrink-0">العنوان:</span>
                      <span className="font-bold text-slate-700 text-end">{selectedProfile.address}</span>
                    </div>
                  )}
                  {selectedProfile.medicalHistory?.notes && !isAccountant && (
                    <div className="border-t border-slate-200/60 pt-2 mt-1">
                      <span className="font-semibold text-slate-400 block mb-1">ملاحظات طبية:</span>
                      <p className="text-slate-600 bg-white p-2 rounded border border-slate-100 italic">{selectedProfile.medicalHistory.notes}</p>
                    </div>
                  )}
                </div>
              </div>

              {/* Quick Actions Footer */}
              <div className="space-y-2 pt-2 border-t border-slate-100">
                <button
                  onClick={() => router.push(`/patients/${selectedProfile.id}`)}
                  className="w-full flex items-center justify-center gap-1.5 py-2 text-sm font-semibold rounded-lg text-white transition duration-200"
                  style={{ background: "#3d7ab5" }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#2d5e8e")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "#3d7ab5")}
                >
                  <Eye className="w-4 h-4" />
                  عرض الملف الكامل
                </button>

                <div className="grid grid-cols-2 gap-2">
                  <button
                    onClick={() => router.push(`/appointments/new?patientId=${selectedProfile.id}`)}
                    className="flex items-center justify-center gap-1.5 py-2 text-xs font-semibold rounded-lg border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 transition duration-200"
                  >
                    <CalendarPlus className="w-3.5 h-3.5 text-slate-400" />
                    موعد جديد
                  </button>

                  <button
                    onClick={() => router.push(`/daily-operations?search=${encodeURIComponent(selectedFullName)}`)}
                    className="flex items-center justify-center gap-1.5 py-2 text-xs font-semibold rounded-lg border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 transition duration-200"
                  >
                    <Activity className="w-3.5 h-3.5 text-slate-400" />
                    التشغيل اليومي
                  </button>
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </div>

      {/* Right-click Context Menu */}
      <PatientContextMenu
        patient={ctxMenu?.patient ?? null}
        position={ctxMenu?.position ?? null}
        isAdmin={isAdmin}
        onClose={() => setCtxMenu(null)}
        onOpen={(id) => router.push(`/patients/${id}`)}
        onEdit={(id) => router.push(`/patients/${id}/edit`)}
        onNewAppointment={(id) => router.push(`/appointments/new?patientId=${id}`)}
        onMessage={(id) => router.push(`/messages?patientId=${id}`)}
        onArchive={(patient) => setConfirm({ open: true, patient, action: "archive" })}
        onRestore={(patient) => setConfirm({ open: true, patient, action: "restore" })}
      />

      {/* Confirm Dialog */}
      <ConfirmDialog
        open={confirm.open}
        title={confirm.action === "archive" ? "أرشفة المريض" : "استعادة المريض"}
        message={
          confirm.action === "archive"
            ? `هل تريد أرشفة المريض "${confirm.patient?.fullName}"؟ لن يظهر في القوائم العادية لكن يمكن استعادته لاحقاً.`
            : `هل تريد استعادة المريض "${confirm.patient?.fullName}" وإعادة تفعيله؟`
        }
        confirmLabel={confirm.action === "archive" ? "أرشفة" : "استعادة"}
        variant={confirm.action === "archive" ? "danger" : "warning"}
        onConfirm={handleConfirm}
        onCancel={() => setConfirm({ open: false, patient: null, action: "archive" })}
      />
    </div>
  );
}
