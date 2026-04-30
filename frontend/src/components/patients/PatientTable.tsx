"use client";
import { useState, useEffect, useCallback, useRef } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  Search, UserPlus, ChevronRight, ChevronLeft, Eye, Pencil,
  Download, MoreVertical, Archive, RotateCcw,
} from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, GENDER_LABELS } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import {
  PatientContextMenu,
  type ContextMenuPosition,
} from "@/components/patients/PatientContextMenu";

interface Doctor { id: string; name: string; }

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
  const isAdmin = user?.role?.toLowerCase() === "admin";

  const [data, setData] = useState<PaginatedResponse<PatientListItem> | null>(null);
  const [search, setSearch] = useState("");
  const [gender, setGender] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [status, setStatus] = useState<"active" | "archived" | "all">("active");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

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
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: "20", status });
      if (search) params.set("search", search);
      if (gender) params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params}`
      );
      setData(res);
    } catch { /* ignore */ }
    setLoading(false);
  }, [page, search, gender, doctorId, status]);

  useEffect(() => {
    const timer = setTimeout(fetchPatients, 300);
    return () => clearTimeout(timer);
  }, [fetchPatients]);

  useEffect(() => { setPage(1); }, [search, gender, doctorId, status]);

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
    } catch { /* ignore */ }
  };

  const doRestore = async (patient: PatientListItem) => {
    try {
      await api.put(`/api/patients/${patient.id}/restore`);
      await fetchPatients();
    } catch { /* ignore */ }
  };

  const handleConfirm = () => {
    if (!confirm.patient) return;
    if (confirm.action === "archive") doArchive(confirm.patient);
    else doRestore(confirm.patient);
    setConfirm({ open: false, patient: null, action: "archive" });
  };

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2 flex-1">
          {/* Search */}
          <div className="relative flex-1 min-w-48">
            <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
            <input
              type="search" value={search} onChange={(e) => setSearch(e.target.value)}
              placeholder="البحث بالاسم أو رقم المريض أو الهاتف..."
              className="w-full h-9 pe-9 ps-4 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            />
          </div>
          {/* Status filter */}
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as "active" | "archived" | "all")}
            className="h-9 px-3 text-sm rounded-lg border border-gray-300 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          >
            <option value="active">النشطون</option>
            {isAdmin && <option value="archived">المؤرشفون</option>}
            {isAdmin && <option value="all">الكل</option>}
          </select>
          {/* Gender filter */}
          <select
            value={gender} onChange={(e) => setGender(e.target.value)}
            className="h-9 px-3 text-sm rounded-lg border border-gray-300 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          >
            <option value="">الجنسان</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
          {/* Doctor filter */}
          {doctors.length > 0 && (
            <select
              value={doctorId} onChange={(e) => setDoctorId(e.target.value)}
              className="h-9 px-3 text-sm rounded-lg border border-gray-300 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            >
              <option value="">كل الأطباء</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}
          {/* Export */}
          <button
            onClick={handleExport}
            className="h-9 flex items-center gap-1.5 px-3 text-sm rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
            title="تصدير CSV"
          >
            <Download className="w-4 h-4" />
            تصدير
          </button>
        </div>
        <Link
          href="/patients/new"
          className="flex items-center gap-2 px-4 py-2 bg-clinic-teal text-white text-sm font-medium rounded-lg hover:opacity-90 transition"
        >
          <UserPlus className="w-4 h-4" />
          مريض جديد
        </Link>
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50 text-gray-500 text-xs font-semibold">
                <th className="px-4 py-3 text-start">رقم المريض</th>
                <th className="px-4 py-3 text-start">الاسم</th>
                <th className="px-4 py-3 text-start">الجنس</th>
                <th className="px-4 py-3 text-start">العمر</th>
                <th className="px-4 py-3 text-start">الهاتف</th>
                <th className="px-4 py-3 text-start">الطبيب</th>
                <th className="px-4 py-3 text-start">تاريخ التسجيل</th>
                <th className="px-4 py-3 text-start">الحالة</th>
                <th className="px-4 py-3 text-start">إجراءات</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 9 }).map((_, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="h-4 bg-gray-100 rounded animate-pulse w-20" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : !data?.data.length ? (
                <tr>
                  <td colSpan={9} className="text-center py-12 text-gray-400">
                    {search ? "لا توجد نتائج مطابقة" : status === "archived" ? "لا يوجد مرضى مؤرشفون" : "لا يوجد مرضى بعد"}
                  </td>
                </tr>
              ) : (
                data.data.map((p) => (
                  <tr
                    key={p.id}
                    onContextMenu={(e) => handleContextMenu(e, p)}
                    className={cn(
                      "hover:bg-gray-50 transition-colors cursor-context-menu select-none",
                      !p.isActive && "opacity-60"
                    )}
                  >
                    <td className="px-4 py-3">
                      <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded text-gray-700">
                        {p.patientNumber}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-semibold text-gray-900">{p.fullName}</td>
                    <td className="px-4 py-3 text-gray-600">{GENDER_LABELS[p.gender ?? ""] ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{p.age ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600 font-mono text-xs">{p.phone ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{p.primaryDoctorName ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {new Date(p.createdAt).toLocaleDateString("ar-YE")}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        p.isActive ? "bg-green-100 text-green-700" : "bg-red-100 text-red-600"
                      )}>
                        {p.isActive ? "نشط" : "مؤرشف"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <Link
                          href={`/patients/${p.id}`}
                          className="p-1.5 text-gray-400 hover:text-clinic-teal hover:bg-clinic-teal-light rounded-lg transition"
                          title="عرض الملف"
                        >
                          <Eye className="w-4 h-4" />
                        </Link>
                        {p.isActive && (
                          <Link
                            href={`/patients/${p.id}/edit`}
                            className="p-1.5 text-gray-400 hover:text-clinic-gold hover:bg-clinic-gold-light rounded-lg transition"
                            title="تعديل"
                          >
                            <Pencil className="w-4 h-4" />
                          </Link>
                        )}
                        {/* ⋮ More actions */}
                        <div className="relative" ref={rowMenuId === p.id ? rowMenuRef : null}>
                          <button
                            onClick={(e) => { e.stopPropagation(); setRowMenuId(rowMenuId === p.id ? null : p.id); }}
                            className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition"
                            title="المزيد"
                          >
                            <MoreVertical className="w-4 h-4" />
                          </button>
                          {rowMenuId === p.id && (
                            <div className="absolute left-0 top-8 z-30 bg-white rounded-xl shadow-xl border border-gray-200 py-1 min-w-44 text-sm" dir="rtl">
                              <Link
                                href={`/patients/${p.id}`}
                                className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 text-gray-700"
                                onClick={() => setRowMenuId(null)}
                              >
                                <Eye className="w-4 h-4 text-gray-400" /> عرض الملف
                              </Link>
                              {p.isActive && (
                                <Link
                                  href={`/patients/${p.id}/edit`}
                                  className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 text-gray-700"
                                  onClick={() => setRowMenuId(null)}
                                >
                                  <Pencil className="w-4 h-4 text-gray-400" /> تعديل البيانات
                                </Link>
                              )}
                              {p.isActive && (
                                <Link
                                  href={`/appointments/new?patientId=${p.id}`}
                                  className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 text-gray-700"
                                  onClick={() => setRowMenuId(null)}
                                >
                                  <Pencil className="w-4 h-4 text-gray-400" /> موعد جديد
                                </Link>
                              )}
                              {isAdmin && (
                                <>
                                  <div className="h-px bg-gray-100 my-1" />
                                  {p.isActive ? (
                                    <button
                                      onClick={() => { setRowMenuId(null); setConfirm({ open: true, patient: p, action: "archive" }); }}
                                      className="w-full flex items-center gap-2 px-3 py-2 hover:bg-red-50 text-red-600"
                                    >
                                      <Archive className="w-4 h-4" /> أرشفة المريض
                                    </button>
                                  ) : (
                                    <button
                                      onClick={() => { setRowMenuId(null); setConfirm({ open: true, patient: p, action: "restore" }); }}
                                      className="w-full flex items-center gap-2 px-3 py-2 hover:bg-green-50 text-green-700"
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

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50">
            <p className="text-xs text-gray-500">
              عرض {(page - 1) * 20 + 1}–{Math.min(page * 20, data.totalCount)} من {data.totalCount}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPreviousPage}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <span className="text-xs px-2 text-gray-600">{page} / {data.totalPages}</span>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNextPage}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right-click context menu */}
      <PatientContextMenu
        patient={ctxMenu?.patient ?? null}
        position={ctxMenu?.position ?? null}
        isAdmin={isAdmin}
        onClose={() => setCtxMenu(null)}
        onOpen={(id) => router.push(`/patients/${id}`)}
        onEdit={(id) => router.push(`/patients/${id}/edit`)}
        onNewAppointment={(id) => router.push(`/appointments/new?patientId=${id}`)}
        onArchive={(patient) => setConfirm({ open: true, patient, action: "archive" })}
        onRestore={(patient) => setConfirm({ open: true, patient, action: "restore" })}
      />

      {/* Confirm dialog */}
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
