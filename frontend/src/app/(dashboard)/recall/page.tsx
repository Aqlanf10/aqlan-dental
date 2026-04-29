"use client";

import { useState } from "react";
import { UserCheck, Calendar, Clock, Search, Send, Filter } from "lucide-react";
import api from "@/lib/api";
import { useQuery } from "@tanstack/react-query";

interface RecallEntry {
  id: string;
  patientId: string;
  patientName: string;
  patientPhone: string;
  lastVisitDate: string;
  nextDueDate: string;
  recallType: string;
  status: "due" | "overdue" | "scheduled" | "completed";
  daysOverdue?: number;
}

interface PatientRecord {
  id: string;
  firstName: string;
  lastName: string;
  phone?: string;
  lastVisitDate?: string;
}

export default function RecallPage() {
  const [statusFilter, setStatusFilter] = useState("all");
  const [searchTerm, setSearchTerm] = useState("");

  const { data: patientsData, isLoading } = useQuery({
    queryKey: ["patients-for-recall"],
    queryFn: async () => {
      const { data } = await api.get("/api/patients?pageSize=100");
      return data;
    },
    staleTime: 60_000,
  });

  const patients: PatientRecord[] = patientsData?.data ?? [];

  const recallEntries: RecallEntry[] = patients
    .filter((p) => p.lastVisitDate)
    .map((p) => {
      const lastVisit = new Date(p.lastVisitDate!);
      const nextDue = new Date(lastVisit);
      nextDue.setMonth(nextDue.getMonth() + 6);
      const now = new Date();
      const daysOverdue = Math.max(0, Math.floor((now.getTime() - nextDue.getTime()) / (1000 * 60 * 60 * 24)));

      let status: RecallEntry["status"] = "due";
      if (daysOverdue > 30) status = "overdue";
      else if (daysOverdue > 0) status = "due";
      else status = "scheduled";

      return {
        id: p.id,
        patientId: p.id,
        patientName: p.firstName + " " + p.lastName,
        patientPhone: p.phone || "—",
        lastVisitDate: p.lastVisitDate!,
        nextDueDate: nextDue.toISOString().split("T")[0],
        recallType: "checkup",
        status,
        daysOverdue: daysOverdue > 0 ? daysOverdue : undefined,
      };
    });

  const filteredEntries = recallEntries.filter(
    (r) =>
      (statusFilter === "all" || r.status === statusFilter) &&
      (r.patientName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.patientPhone?.includes(searchTerm))
  );

  const statusColors: Record<string, string> = {
    due: "bg-amber-100 text-amber-700",
    overdue: "bg-red-100 text-red-700",
    scheduled: "bg-blue-100 text-blue-700",
    completed: "bg-green-100 text-green-700",
  };

  const statusLabels: Record<string, string> = {
    due: "مستحق",
    overdue: "متأخر",
    scheduled: "مجدول",
    completed: "مكتمل",
  };

  const recallTypes: Record<string, string> = {
    checkup: "فحص دوري",
    ortho_followup: "متابعة تقويم",
    surgery_followup: "متابعة جراحة",
    cleaning: "تنظيف",
  };

  const stats = {
    total: recallEntries.length,
    overdue: recallEntries.filter((r) => r.status === "overdue").length,
    due: recallEntries.filter((r) => r.status === "due").length,
    scheduled: recallEntries.filter((r) => r.status === "scheduled").length,
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-extrabold text-[#0d2137]">نظام الاستدعاء</h2>
          <p className="text-sm text-gray-500 mt-1">إدارة استدعاء المرضى للمتابعات الدورية والفحوصات</p>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {[
          { label: "الإجمالي", value: stats.total, icon: UserCheck, color: "bg-[#f0f5fb] text-[#3d7ab5]" },
          { label: "متأخر", value: stats.overdue, icon: Clock, color: "bg-red-50 text-red-600" },
          { label: "مستحق", value: stats.due, icon: Calendar, color: "bg-amber-50 text-amber-600" },
          { label: "مجدول", value: stats.scheduled, icon: Filter, color: "bg-blue-50 text-blue-600" },
        ].map((stat) => (
          <div key={stat.label} className={`${stat.color} rounded-xl p-4`}>
            <div className="flex items-center gap-2 mb-2">
              <stat.icon className="w-5 h-5 opacity-70" />
              <span className="text-xs font-bold opacity-70">{stat.label}</span>
            </div>
            <p className="text-2xl font-extrabold">{stat.value}</p>
          </div>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[220px] max-w-sm">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 right-3 text-gray-400" />
          <input
            type="text"
            placeholder="بحث بالاسم أو الرقم..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-10 pr-10 pl-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5]"
          />
        </div>
        <div className="flex items-center gap-1.5">
          {["all", "overdue", "due", "scheduled", "completed"].map((s) => (
            <button
              key={s}
              onClick={() => setStatusFilter(s)}
              className={`px-3 py-1.5 text-xs font-bold rounded-lg transition-colors ${
                statusFilter === s ? "bg-[#3d7ab5] text-white" : "bg-[#f0f5fb] text-gray-600 hover:bg-[#dce8f5]"
              }`}
            >
              {s === "all" ? "الكل" : statusLabels[s]}
            </button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="animate-pulse h-24 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : filteredEntries.length === 0 ? (
        <div className="text-center py-20">
          <UserCheck className="w-16 h-16 mx-auto text-gray-200 mb-4" />
          <p className="text-gray-400 text-lg font-bold">لا يوجد مرضى للاستدعاء</p>
          <p className="text-gray-300 text-sm mt-1">سيظهر المرضى المستحقون للمتابعة هنا تلقائياً</p>
        </div>
      ) : (
        <div className="space-y-2">
          {filteredEntries.map((entry) => (
            <div key={entry.id} className="p-4 rounded-xl border border-[#e8f0f9] bg-white hover:shadow-md transition-all">
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-full bg-[#3d7ab5]/10 flex items-center justify-center flex-shrink-0">
                  <UserCheck className="w-5 h-5 text-[#3d7ab5]" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-[#0d2137]">{entry.patientName}</span>
                    <span className="text-xs text-gray-400">{entry.patientPhone}</span>
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${statusColors[entry.status]}`}>
                      {statusLabels[entry.status]}
                    </span>
                    {entry.daysOverdue && entry.daysOverdue > 0 && (
                      <span className="text-[10px] font-bold text-red-500">متأخر {entry.daysOverdue} يوم</span>
                    )}
                  </div>
                  <div className="flex items-center gap-4 text-xs text-gray-500 mt-1">
                    <span>آخر زيارة: {entry.lastVisitDate}</span>
                    <span>الاستدعاء: {entry.nextDueDate}</span>
                    <span className="bg-gray-100 px-2 py-0.5 rounded text-gray-600">{recallTypes[entry.recallType]}</span>
                  </div>
                </div>
                <div className="flex gap-2 flex-shrink-0">
                  <button className="px-3 py-1.5 bg-[#3d7ab5] text-white rounded-lg text-xs font-bold hover:bg-[#2d5e8e] transition-colors flex items-center gap-1">
                    <Send className="w-3 h-3" />تذكير
                  </button>
                  <button className="px-3 py-1.5 bg-green-100 text-green-700 rounded-lg text-xs font-bold hover:bg-green-200 transition-colors flex items-center gap-1">
                    <Calendar className="w-3 h-3" />موعد
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
