"use client";
import { useState, useEffect, useCallback, useRef } from "react";
import Link from "next/link";
import { Search, UserPlus, ChevronRight, ChevronLeft, Eye, Pencil, Download, Archive, RotateCcw, MoreVertical, Calendar, Wallet, Activity, MessageCircle, Phone, Printer } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, GENDER_LABELS, formatPhoneForWhatsApp } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";

interface Doctor { id: string; name: string; }

// ─── قائمة إجراءات المريض ──────────────────────────────────────────────────
interface ContextMenuState {
  patientId: string;
  patientName: string;
  phone: string | null;
  isActive: boolean;
  x: number;
  y: number;
}

function PatientContextMenu({
  menu,
  onClose,
  userRole,
}: {
  menu: ContextMenuState;
  onClose: () => void;
  userRole?: string;
}) {
  const menuRef = useRef<HTMLDivElement>(null);
  const [adjustedPos, setAdjustedPos] = useState({ x: menu.x, y: menu.y });

  // Adjust position to stay within viewport
  useEffect(() => {
    if (!menuRef.current) return;
    const rect = menuRef.current.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    let x = menu.x;
    let y = menu.y;

    // RTL: menu opens to the left of cursor
    if (x - 220 < 0) x = 10;
    if (x > vw - 230) x = vw - 230;
    if (y + rect.height > vh - 10) y = vh - rect.height - 10;
    if (y < 10) y = 10;

    setAdjustedPos({ x, y });
  }, [menu.x, menu.y]);

  // Close on click outside
  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const handleScroll = () => onClose();
    document.addEventListener("mousedown", handleClick);
    document.addEventListener("scroll", handleScroll, true);
    return () => {
      document.removeEventListener("mousedown", handleClick);
      document.removeEventListener("scroll", handleScroll, true);
    };
  }, [onClose]);

  const isAdmin = userRole === "Admin";
  const isAccountant = userRole === "Accountant";
  const isOrthodontist = userRole === "Orthodontist";
  const canFinance = isAdmin || isAccountant;
  const canOrtho = isAdmin || isOrthodontist;

  const items = [
    { icon: Eye, label: "فتح الملف", href: `/patients/${menu.patientId}`, show: true },
    { icon: Pencil, label: "تعديل", href: `/patients/${menu.patientId}/edit`, show: menu.isActive },
    { icon: Calendar, label: "إنشاء موعد", href: `/appointments/new?patientId=${menu.patientId}&patientName=${encodeURIComponent(menu.patientName)}`, show: menu.isActive },
    { icon: Wallet, label: "إضافة دفعة", href: `/finance/payments?patientId=${menu.patientId}`, show: canFinance && menu.isActive },
    { icon: Activity, label: "الحالة التقويمية", href: `/ortho/new?patientId=${menu.patientId}&patientName=${encodeURIComponent(menu.patientName)}`, show: canOrtho && menu.isActive },
    { icon: MessageCircle, label: "رسالة داخلية", href: `/messages?patientId=${menu.patientId}`, show: true },
    { icon: Phone, label: "واتساب", href: menu.phone ? `https://wa.me/${formatPhoneForWhatsApp(menu.phone)}` : "#", show: !!menu.phone, external: true },
    { icon: Printer, label: "طباعة ملخص", href: `/patients/${menu.patientId}?print=1`, show: true },
    { icon: Archive, label: "أرشفة المريض", action: "archive" as const, show: isAdmin && menu.isActive, danger: true },
    { icon: RotateCcw, label: "استعادة المريض", action: "restore" as const, show: isAdmin && !menu.isActive, success: true },
  ].filter((item) => item.show);

  return (
    <div
      ref={menuRef}
      className="fixed z-50 bg-white rounded-xl border border-gray-200 shadow-xl py-1.5 min-w-[200px] animate-in fade-in-0 zoom-in-95 duration-100"
      style={{ left: adjustedPos.x, top: adjustedPos.y }}
    >
      <div className="px-3 py-2 border-b border-gray-100">
        <p className="text-sm font-bold text-gray-900 truncate">{menu.patientName}</p>
      </div>
      {items.map((item, i) => {
        const Icon = item.icon;
        if ("href" in item && item.href) {
          return (
            <a
              key={i}
              href={item.href}
              target={("external" in item && item.external) ? "_blank" : undefined}
              rel={("external" in item && item.external) ? "noopener noreferrer" : undefined}
              className={cn(
                "flex items-center gap-2.5 px-3 py-2 text-sm transition-colors",
                item.danger ? "text-orange-600 hover:bg-orange-50" :
                item.success ? "text-green-600 hover:bg-green-50" :
                "text-gray-700 hover:bg-gray-50"
              )}
              onClick={onClose}
            >
              <Icon className="w-4 h-4 flex-shrink-0" />
              {item.label}
            </a>
          );
        }
        if ("action" in item) {
          return (
            <button
              key={i}
              className={cn(
                "w-full flex items-center gap-2.5 px-3 py-2 text-sm transition-colors text-start",
                item.danger ? "text-orange-600 hover:bg-orange-50" :
                item.success ? "text-green-600 hover:bg-green-50" :
                "text-gray-700 hover:bg-gray-50"
              )}
              onClick={() => {
                onClose();
                if (item.action === "archive") {
                  document.dispatchEvent(new CustomEvent("patient-action", { detail: { action: "archive", id: menu.patientId, name: menu.patientName } }));
                } else if (item.action === "restore") {
                  document.dispatchEvent(new CustomEvent("patient-action", { detail: { action: "restore", id: menu.patientId, name: menu.patientName } }));
                }
              }}
            >
              <Icon className="w-4 h-4 flex-shrink-0" />
              {item.label}
            </button>
          );
        }
        return null;
      })}
    </div>
  );
}

function exportCsv(patients: PatientListItem[]) {
  const headers = ["رقم المريض", "الاسم", "الجنس", "العمر", "الهاتف", "الطبيب", "تاريخ التسجيل"];
  const rows = patients.map((p) => [
    p.patientNumber,
    p.fullName,
    GENDER_LABELS[p.gender ?? ""] ?? "",
    String(p.age ?? ""),
    p.phone ?? "",
    p.primaryDoctorName ?? "",
    new Date(p.createdAt).toLocaleDateString("ar-YE"),
  ]);
  const csv = [headers, ...rows].map((r) => r.map((c) => `"${c}"`).join(",")).join("\n");
  const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = "patients.csv"; a.click();
  URL.revokeObjectURL(url);
}

export function PatientTable() {
  const [data, setData] = useState<PaginatedResponse<PatientListItem> | null>(null);
  const [search, setSearch] = useState("");
  const [gender, setGender] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>("active");
  const { user } = useAuthStore();
  const [confirmAction, setConfirmAction] = useState<{ type: "archive" | "restore"; id: string; name: string } | null>(null);
  const [contextMenu, setContextMenu] = useState<ContextMenuState | null>(null);

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  // Listen for archive/restore actions from context menu
  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent).detail;
      if (detail.action === "archive") {
        handleArchive(detail.id, detail.name);
      } else if (detail.action === "restore") {
        handleRestore(detail.id, detail.name);
      }
    };
    document.addEventListener("patient-action", handler);
    return () => document.removeEventListener("patient-action", handler);
  }, []);

  const fetchPatients = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (search)   params.set("search", search);
      if (gender)   params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      if (statusFilter === "archived") params.set("status", "archived");
      else if (statusFilter === "all") params.set("status", "all");
      // "active" is the default — no status param needed
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params}`
      );
      setData(res);
    } catch {}
    setLoading(false);
  }, [page, search, gender, doctorId, statusFilter]);

  useEffect(() => {
    const timer = setTimeout(fetchPatients, 300);
    return () => clearTimeout(timer);
  }, [fetchPatients]);

  useEffect(() => { setPage(1); }, [search, gender, doctorId, statusFilter]);

  const handleExport = async () => {
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "1000" });
      if (search)   params.set("search", search);
      if (gender)   params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      if (statusFilter === "archived") params.set("status", "archived");
      else if (statusFilter === "all") params.set("status", "all");
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(`/api/patients?${params}`);
      exportCsv(res.data);
    } catch {}
  };

  const handleArchive = (id: string, name: string) => {
    setConfirmAction({ type: "archive", id, name });
  };

  const handleRestore = (id: string, name: string) => {
    setConfirmAction({ type: "restore", id, name });
  };

  const executeConfirmAction = async () => {
    if (!confirmAction) return;
    try {
      if (confirmAction.type === "archive") {
        await api.delete(`/api/patients/${confirmAction.id}`);
        toast.success(`تم أرشفة المريض ${confirmAction.name} بنجاح`);
      } else {
        await api.post(`/api/patients/${confirmAction.id}/restore`);
        toast.success(`تم استعادة المريض ${confirmAction.name} بنجاح`);
      }
      fetchPatients();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "حدث خطأ أثناء العملية");
    } finally {
      setConfirmAction(null);
    }
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
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="البحث بالاسم أو رقم المريض أو الهاتف..."
              className="w-full h-9 pe-9 ps-4 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            />
          </div>
          {/* Gender filter */}
          <select
            value={gender}
            onChange={(e) => setGender(e.target.value)}
            className="h-9 px-3 text-sm rounded-lg border border-gray-300 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          >
            <option value="">الجنسان</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
          {/* Doctor filter */}
          {doctors.length > 0 && (
            <select
              value={doctorId}
              onChange={(e) => setDoctorId(e.target.value)}
              className="h-9 px-3 text-sm rounded-lg border border-gray-300 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            >
              <option value="">كل الأطباء</option>
              {doctors.map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
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

      {/* Status Filter Tabs */}
      <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-1 w-fit">
        {[
          { key: "active", label: "النشطون" },
          { key: "archived", label: "المؤرشفون" },
          { key: "all", label: "الكل" },
        ].map(({ key, label }) => (
          <button
            key={key}
            onClick={() => { setStatusFilter(key); setPage(1); }}
            className={cn(
              "px-4 py-1.5 text-sm font-medium rounded-md transition",
              statusFilter === key
                ? "bg-white text-gray-900 shadow-sm"
                : "text-gray-500 hover:text-gray-700"
            )}
          >
            {label}
          </button>
        ))}
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
                    {search ? "لا توجد نتائج مطابقة" : "لا يوجد مرضى بعد"}
                  </td>
                </tr>
              ) : (
                data.data.map((p) => (
                  <tr
                    key={p.id}
                    className={cn("hover:bg-gray-50 transition-colors cursor-pointer", !p.isActive && "bg-orange-50/30")}
                    onContextMenu={(e) => {
                      e.preventDefault();
                      setContextMenu({
                        patientId: p.id,
                        patientName: p.fullName,
                        phone: p.phone ?? null,
                        isActive: p.isActive,
                        x: e.clientX,
                        y: e.clientY,
                      });
                    }}
                  >
                    <td className="px-4 py-3">
                      <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded text-gray-700">
                        {p.patientNumber}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-semibold text-gray-900">{p.fullName}</td>
                    <td className="px-4 py-3 text-gray-600">
                      {GENDER_LABELS[p.gender ?? ""] ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600">{p.age ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600 font-mono text-xs">{p.phone ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{p.primaryDoctorName ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {new Date(p.createdAt).toLocaleDateString("ar-YE")}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          "text-xs px-2 py-0.5 rounded-full font-medium",
                          p.isActive
                            ? "bg-green-100 text-green-700"
                            : "bg-orange-100 text-orange-600"
                        )}
                      >
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
                        {user?.role === "Admin" && p.isActive && (
                          <button
                            onClick={() => handleArchive(p.id, p.fullName)}
                            className="p-1.5 text-gray-400 hover:text-orange-500 hover:bg-orange-50 rounded-lg transition"
                            title="أرشفة المريض"
                          >
                            <Archive className="w-4 h-4" />
                          </button>
                        )}
                        {user?.role === "Admin" && !p.isActive && (
                          <button
                            onClick={() => handleRestore(p.id, p.fullName)}
                            className="p-1.5 text-gray-400 hover:text-green-500 hover:bg-green-50 rounded-lg transition"
                            title="استعادة المريض"
                          >
                            <RotateCcw className="w-4 h-4" />
                          </button>
                        )}
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            const rect = e.currentTarget.getBoundingClientRect();
                            setContextMenu({
                              patientId: p.id,
                              patientName: p.fullName,
                              phone: p.phone ?? null,
                              isActive: p.isActive,
                              x: rect.left,
                              y: rect.bottom + 4,
                            });
                          }}
                          className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition"
                          title="إجراءات"
                        >
                          <MoreVertical className="w-4 h-4" />
                        </button>
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
              <span className="text-xs px-2 text-gray-600">
                {page} / {data.totalPages}
              </span>
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

      {/* Patient Context Menu */}
      {contextMenu && (
        <PatientContextMenu
          menu={contextMenu}
          onClose={() => setContextMenu(null)}
          userRole={user?.role}
        />
      )}

      {/* Confirmation Dialog */}
      {confirmAction && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl p-6 space-y-4">
            <div className="text-center">
              <div className={cn(
                "w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-3",
                confirmAction.type === "archive" ? "bg-orange-100" : "bg-green-100"
              )}>
                {confirmAction.type === "archive"
                  ? <Archive className="w-7 h-7 text-orange-600" />
                  : <RotateCcw className="w-7 h-7 text-green-600" />
                }
              </div>
              <h3 className="text-lg font-bold text-gray-900">
                {confirmAction.type === "archive" ? "أرشفة المريض" : "استعادة المريض"}
              </h3>
              <p className="text-sm text-gray-500 mt-2">
                {confirmAction.type === "archive"
                  ? `هل أنت متأكد من أرشفة المريض "${confirmAction.name}"؟ لن يظهر في قائمة المرضى النشطين، ويمكن استعادته لاحقًا.`
                  : `هل أنت متأكد من استعادة المريض "${confirmAction.name}"؟ سيظهر مجدداً في قائمة المرضى النشطين.`
                }
              </p>
            </div>
            <div className="flex gap-3">
              <button
                onClick={() => setConfirmAction(null)}
                className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
              <button
                onClick={executeConfirmAction}
                className={cn(
                  "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                  confirmAction.type === "archive"
                    ? "bg-orange-500 hover:bg-orange-600"
                    : "bg-green-500 hover:bg-green-600"
                )}
              >
                {confirmAction.type === "archive" ? "أرشفة" : "استعادة"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
