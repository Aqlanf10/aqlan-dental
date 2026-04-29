"use client";
import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { Search, UserPlus, ChevronRight, ChevronLeft, Eye, Pencil, Download } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, GENDER_LABELS } from "@/lib/utils";

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

export function PatientTable() {
  const [data, setData] = useState<PaginatedResponse<PatientListItem> | null>(null);
  const [search, setSearch] = useState("");
  const [gender, setGender] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

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
    } catch {}
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
    } catch {}
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
                  <tr key={p.id} className="hover:bg-gray-50 transition-colors">
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
                            : "bg-gray-100 text-gray-500"
                        )}
                      >
                        {p.isActive ? "نشط" : "محذوف"}
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
                        <Link
                          href={`/patients/${p.id}/edit`}
                          className="p-1.5 text-gray-400 hover:text-clinic-gold hover:bg-clinic-gold-light rounded-lg transition"
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
    </div>
  );
}
