"use client";
import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { Search, UserPlus, ChevronRight, ChevronLeft, Eye, Pencil, Download, Filter } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, GENDER_LABELS, badgeColorClass } from "@/lib/utils";
import type { BadgeColor } from "@/lib/utils";

interface Doctor { id: string; name: string; }

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

function statusBadge(isActive: boolean) {
  const color: BadgeColor = isActive ? "green" : "gray";
  const label = isActive ? "نشط" : "محذوف";
  return <span className={badgeColorClass(color)}>{label}</span>;
}

export function PatientTable() {
  const [data, setData] = useState<PaginatedResponse<PatientListItem> | null>(null);
  const [search, setSearch] = useState("");
  const [gender, setGender] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  const fetchPatients = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (search)   params.set("search", search);
      if (gender)   params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params}`
      );
      setData(res);
    } catch { /* ignore */ }
    setLoading(false);
  }, [page, search, gender, doctorId]);

  useEffect(() => {
    const timer = setTimeout(fetchPatients, 300);
    return () => clearTimeout(timer);
  }, [fetchPatients]);

  useEffect(() => { setPage(1); }, [search, gender, doctorId]);

  const handleExport = async () => {
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "1000" });
      if (search)   params.set("search", search);
      if (gender)   params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(`/api/patients?${params}`);
      exportCsv(res.data);
    } catch { /* ignore */ }
  };

  return (
    <div className="space-y-4" dir="rtl">
      {/* Toolbar Card */}
      <div
        className="rounded-xl border p-5"
        style={{
          backgroundColor: "#ffffff",
          borderColor: "#e8f0f9",
          boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
        }}
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          {/* Search */}
          <div className="relative flex-1 min-w-64">
            <Search
              className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3"
              style={{ color: "#94a3b8" }}
            />
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="البحث بالاسم أو رقم المريض أو الهاتف..."
              className="aqlan-input pe-9 ps-4 h-10"
              style={{ borderRadius: "0.5rem" }}
            />
          </div>

          {/* Filter toggle */}
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={cn(
              "flex items-center gap-1.5 px-3 py-2 text-sm rounded-lg transition-colors",
              showFilters
                ? "text-white"
                : "btn-ghost"
            )}
            style={showFilters ? { backgroundColor: "#3d7ab5", color: "#ffffff" } : undefined}
          >
            <Filter className="w-4 h-4" />
            تصفية
          </button>

          {/* Export */}
          <button
            onClick={handleExport}
            className="btn-ghost flex items-center gap-1.5 px-3 py-2 text-sm rounded-lg"
          >
            <Download className="w-4 h-4" />
            تصدير
          </button>

          {/* Add Patient Button */}
          <Link
            href="/patients/new"
            className="btn-blue flex items-center gap-2"
          >
            <UserPlus className="w-4 h-4" />
            إضافة مريض
          </Link>
        </div>

        {/* Filter controls (collapsible) */}
        {showFilters && (
          <div className="flex flex-wrap items-center gap-3 mt-4 pt-4" style={{ borderTop: "1px solid #f1f5f9" }}>
            {/* Gender filter */}
            <select
              value={gender}
              onChange={(e) => setGender(e.target.value)}
              className="aqlan-select h-10 min-w-[140px]"
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
                className="aqlan-select h-10 min-w-[160px]"
              >
                <option value="">كل الأطباء</option>
                {doctors.map((d) => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            )}
          </div>
        )}
      </div>

      {/* Table Card */}
      <div
        className="rounded-xl border overflow-hidden"
        style={{
          backgroundColor: "#ffffff",
          borderColor: "#e8f0f9",
          boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
        }}
      >
        <div className="overflow-x-auto">
          <table className="aqlan-table">
            <thead>
              <tr>
                <th>الاسم</th>
                <th>الهاتف</th>
                <th>العمر</th>
                <th>الجنس</th>
                <th>آخر زيارة</th>
                <th>الحالة</th>
                <th>إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 7 }).map((_, j) => (
                      <td key={j}>
                        <div
                          className="h-4 rounded animate-pulse w-20"
                          style={{ backgroundColor: "#f0f5fb" }}
                        />
                      </td>
                    ))}
                  </tr>
                ))
              ) : !data?.data.length ? (
                <tr>
                  <td colSpan={7} className="text-center py-12" style={{ color: "#94a3b8" }}>
                    {search ? "لا توجد نتائج مطابقة" : "لا يوجد مرضى بعد"}
                  </td>
                </tr>
              ) : (
                data.data.map((p) => (
                  <tr key={p.id}>
                    <td>
                      <div className="flex items-center gap-3">
                        {/* Avatar circle */}
                        <div
                          className="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold text-white flex-shrink-0"
                          style={{ backgroundColor: "#3d7ab5" }}
                        >
                          {p.fullName.charAt(0)}
                        </div>
                        <div>
                          <div className="font-semibold" style={{ color: "#0d2137" }}>
                            {p.fullName}
                          </div>
                          <div className="text-xs font-mono" style={{ color: "#94a3b8" }}>
                            {p.patientNumber}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className="font-mono text-xs" style={{ color: "#64748b" }}>
                        {p.phone ?? "—"}
                      </span>
                    </td>
                    <td style={{ color: "#64748b" }}>{p.age ?? "—"}</td>
                    <td>
                      <span className={badgeColorClass(p.gender === "Male" ? "blue" : "purple")}>
                        {GENDER_LABELS[p.gender ?? ""] ?? "—"}
                      </span>
                    </td>
                    <td>
                      <span className="text-xs" style={{ color: "#64748b" }}>
                        {new Date(p.createdAt).toLocaleDateString("ar-YE")}
                      </span>
                    </td>
                    <td>{statusBadge(p.isActive)}</td>
                    <td>
                      <div className="flex items-center gap-1">
                        <Link
                          href={`/patients/${p.id}`}
                          className="p-2 rounded-lg transition-colors"
                          style={{ color: "#94a3b8" }}
                          onMouseEnter={(e) => {
                            e.currentTarget.style.color = "#3d7ab5";
                            e.currentTarget.style.backgroundColor = "#3d7ab518";
                          }}
                          onMouseLeave={(e) => {
                            e.currentTarget.style.color = "#94a3b8";
                            e.currentTarget.style.backgroundColor = "transparent";
                          }}
                          title="عرض الملف"
                        >
                          <Eye className="w-4 h-4" />
                        </Link>
                        <Link
                          href={`/patients/${p.id}/edit`}
                          className="p-2 rounded-lg transition-colors"
                          style={{ color: "#94a3b8" }}
                          onMouseEnter={(e) => {
                            e.currentTarget.style.color = "#f5922e";
                            e.currentTarget.style.backgroundColor = "#f5922e18";
                          }}
                          onMouseLeave={(e) => {
                            e.currentTarget.style.color = "#94a3b8";
                            e.currentTarget.style.backgroundColor = "transparent";
                          }}
                          title="تعديل"
                        >
                          <Pencil className="w-4 h-4" />
                        </Link>
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
          <div
            className="flex items-center justify-between px-4 py-3"
            style={{
              borderTop: "1px solid #e8f0f9",
              backgroundColor: "#f7fafd",
            }}
          >
            <p className="text-xs" style={{ color: "#64748b" }}>
              عرض {(page - 1) * 20 + 1}–{Math.min(page * 20, data.totalCount)} من {data.totalCount}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPreviousPage}
                className="p-1.5 rounded-lg transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                style={{ color: "#64748b" }}
                onMouseEnter={(e) => { if (data.hasPreviousPage) e.currentTarget.style.backgroundColor = "#eef3f9"; }}
                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <span className="text-xs px-2" style={{ color: "#0d2137" }}>
                {page} / {data.totalPages}
              </span>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNextPage}
                className="p-1.5 rounded-lg transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                style={{ color: "#64748b" }}
                onMouseEnter={(e) => { if (data.hasNextPage) e.currentTarget.style.backgroundColor = "#eef3f9"; }}
                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
