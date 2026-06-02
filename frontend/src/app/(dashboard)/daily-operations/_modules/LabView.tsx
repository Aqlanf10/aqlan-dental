"use client";

import { useState } from "react";
import { Search, Activity, CheckCircle2, Clock, Calendar, Check, Send, AlertTriangle, FileSpreadsheet } from "lucide-react";
import { NAVY, BLUE, ORANGE, fmtRial, fmtDate } from "../_lib/constants";
import { toast } from "@/stores/toastStore";

interface LabCase {
  id: string;
  patientName: string;
  patientNumber: string;
  doctorName: string;
  labName: string;
  restorationType: string;
  shade: string;
  status: "Sent" | "Received" | "Delivered";
  sentDate: string;
  expectedDate: string;
  cost: number;
  notes?: string;
}

const MOCK_LAB_CASES: LabCase[] = [
  {
    id: "1",
    patientName: "أحمد محمد العولقي",
    patientNumber: "P-10023",
    doctorName: "د. عقلان الكاملي",
    labName: "مختبر النخبة لتركيبات الأسنان",
    restorationType: "تاج زركونيا (Zirconia Crown)",
    shade: "A2",
    status: "Sent",
    sentDate: "2026-06-01",
    expectedDate: "2026-06-05",
    cost: 45000,
    notes: "يرجى التركيز على حواف التاج ونقاط التلامس"
  },
  {
    id: "2",
    patientName: "سارة عبد الله الحبيشي",
    patientNumber: "P-10094",
    doctorName: "د. إيمان الخليدي",
    labName: "مختبر الجزيرة الحديث",
    restorationType: "فينير إيماكس (E-Max Veneers x6)",
    shade: "BL4",
    status: "Received",
    sentDate: "2026-05-25",
    expectedDate: "2026-06-02",
    cost: 180000,
    notes: "تبييض هوليوود سمايل طبيعي"
  },
  {
    id: "3",
    patientName: "صالح علي الصبري",
    patientNumber: "P-10112",
    doctorName: "د. عقلان الكاملي",
    labName: "مختبر النخبة لتركيبات الأسنان",
    restorationType: "جسر زركونيا 3 وحدات (3-Unit Bridge)",
    shade: "A3",
    status: "Delivered",
    sentDate: "2026-05-20",
    expectedDate: "2026-05-26",
    cost: 120000,
    notes: "تم القياس والتسليم بنجاح للمريض"
  },
  {
    id: "4",
    patientName: "نادية عبد الكريم الريمي",
    patientNumber: "P-10255",
    doctorName: "د. طارق الحيمي",
    labName: "مختبر المستقبل الرقمي",
    restorationType: "طقم أسنان أكريليكي جزئي (Partial Acrylic Denture)",
    shade: "A3.5",
    status: "Sent",
    sentDate: "2026-05-30",
    expectedDate: "2026-06-04",
    cost: 60000,
    notes: "مريض لديه حساسية لبعض المواد"
  }
];

export default function LabView() {
  const [cases, setCases] = useState<LabCase[]>(MOCK_LAB_CASES);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("All");

  const handleUpdateStatus = (id: string, newStatus: "Sent" | "Received" | "Delivered") => {
    setCases(prev => prev.map(c => {
      if (c.id === id) {
        toast.success(`تم تحديث حالة المعمل بنجاح`);
        return { ...c, status: newStatus };
      }
      return c;
    }));
  };

  const filteredCases = cases.filter(c => {
    const matchesSearch = c.patientName.includes(search) || c.doctorName.includes(search) || c.restorationType.includes(search);
    const matchesStatus = statusFilter === "All" || c.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const getStatusBadge = (status: "Sent" | "Received" | "Delivered") => {
    switch (status) {
      case "Sent":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200">
            <Clock className="w-3 h-3" /> تم الإرسال للمعمل
          </span>
        );
      case "Received":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">
            <CheckCircle2 className="w-3 h-3" /> جاهز للتسليم بالعيادة
          </span>
        );
      case "Delivered":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
            <Check className="w-3 h-3" /> تم التسليم للمريض
          </span>
        );
    }
  };

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 space-y-4">
      {/* Top Filter Bar */}
      <div className="flex items-center justify-between gap-3 bg-white p-3 rounded-2xl border border-gray-100 shadow-sm flex-wrap">
        <div className="flex items-center gap-2 flex-1 max-w-md relative">
          <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder="البحث باسم المريض، الطبيب، أو نوع التركيبة..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full text-xs rounded-xl border border-gray-200 pl-10 pr-9 py-2 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20"
          />
        </div>

        <div className="flex items-center gap-2">
          <select
            value={statusFilter}
            onChange={e => setStatusFilter(e.target.value)}
            className="text-xs font-semibold rounded-xl px-3 py-2 border border-gray-200 outline-none"
            style={{ color: NAVY }}
          >
            <option value="All">📊 كل حالات المعمل</option>
            <option value="Sent">⌛ المرسلة للمعمل</option>
            <option value="Received">📦 الجاهزة للتسليم</option>
            <option value="Delivered">✅ المسلمة للمرضى</option>
          </select>

          <button className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-bold bg-gray-50 border border-gray-200 hover:bg-gray-100 text-gray-700">
            <FileSpreadsheet className="w-3.5 h-3.5" /> تصدير
          </button>
        </div>
      </div>

      {/* Grid List */}
      <div className="flex-1 overflow-auto bg-white rounded-2xl border border-gray-100 shadow-sm">
        {filteredCases.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-gray-400">
            <Activity className="w-12 h-12 mb-3 opacity-20" />
            <p className="text-sm font-bold">لا توجد حالات معمل مطابقة</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-right" style={{ borderCollapse: "separate", borderSpacing: 0 }}>
              <thead>
                <tr className="text-[11px] font-bold text-gray-400 uppercase tracking-wider bg-gray-50/50">
                  <th className="py-3 px-4 border-b">المريض</th>
                  <th className="py-3 px-4 border-b">التركيبة والظل</th>
                  <th className="py-3 px-4 border-b">الطبيب والمعمل</th>
                  <th className="py-3 px-4 border-b">التواريخ</th>
                  <th className="py-3 px-4 border-b">التكلفة</th>
                  <th className="py-3 px-4 border-b">الحالة</th>
                  <th className="py-3 px-4 border-b text-left">إجراءات</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredCases.map(c => (
                  <tr key={c.id} className="hover:bg-gray-50/30 text-xs">
                    <td className="py-3.5 px-4 font-bold" style={{ color: NAVY }}>
                      <div>{c.patientName}</div>
                      <div className="text-[10px] text-gray-400 font-medium font-sans mt-0.5">{c.patientNumber}</div>
                    </td>
                    <td className="py-3.5 px-4 font-semibold text-gray-700">
                      <div>{c.restorationType}</div>
                      <div className="text-[10px] text-purple-600 font-bold bg-purple-50 px-1 py-0.5 rounded border border-purple-100 inline-block mt-1">
                        الظل / Shade: {c.shade}
                      </div>
                    </td>
                    <td className="py-3.5 px-4 text-gray-600">
                      <div className="font-semibold">{c.doctorName}</div>
                      <div className="text-[10px] text-gray-400 mt-0.5">{c.labName}</div>
                    </td>
                    <td className="py-3.5 px-4 text-gray-500 font-medium">
                      <div>ارسل: {fmtDate(c.sentDate)}</div>
                      <div className="text-[10px] text-orange-600 font-bold mt-0.5">متوقع: {fmtDate(c.expectedDate)}</div>
                    </td>
                    <td className="py-3.5 px-4 font-bold text-gray-900 font-sans">{fmtRial(c.cost)}</td>
                    <td className="py-3.5 px-4">{getStatusBadge(c.status)}</td>
                    <td className="py-3.5 px-4 text-left">
                      <div className="flex items-center justify-end gap-1.5">
                        {c.status === "Sent" && (
                          <button
                            onClick={() => handleUpdateStatus(c.id, "Received")}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-bold text-white bg-blue-600 hover:bg-blue-700 transition"
                          >
                            تأكيد الوصول
                          </button>
                        )}
                        {c.status === "Received" && (
                          <button
                            onClick={() => handleUpdateStatus(c.id, "Delivered")}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-bold text-white bg-emerald-600 hover:bg-emerald-700 transition"
                          >
                            تسليم للمريض
                          </button>
                        )}
                        {c.notes && (
                          <button
                            onClick={() => alert(`ملاحظة المعمل: ${c.notes}`)}
                            className="w-7 h-7 rounded-lg flex items-center justify-center border hover:bg-gray-100 text-gray-400"
                            title="عرض الملاحظات"
                          >
                            <AlertTriangle className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
