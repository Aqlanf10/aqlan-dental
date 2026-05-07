"use client";

import { useEffect, useState } from "react";
import { ScanLine, Plus, Trash2, ExternalLink, FileText } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

interface RadiographItem {
  id: string;
  xrayType: string;
  fileUrl: string;
  toothRelated?: string;
  notes?: string;
  xrayDate: string;
  doctorName?: string;
}

const XRAY_TYPE_LABELS: Record<string, string> = {
  OPG: "OPG",
  lateral_ceph: "Lateral Ceph",
  bitewing: "Bitewing",
  periapical: "Periapical",
  PA_ceph: "PA Ceph",
  CBCT: "CBCT",
};

interface RadiographsTabProps {
  patientId: string;
}

export function RadiographsTab({ patientId }: RadiographsTabProps) {
  const [xrays, setXrays] = useState<RadiographItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Form state
  const [xrayFile, setXrayFile] = useState<File | null>(null);
  const [xrayType, setXrayType] = useState("OPG");
  const [xrayNotes, setXrayNotes] = useState("");
  const [addingXray, setAddingXray] = useState(false);

  const uploadFile = async (file: File): Promise<string> => {
    const token = localStorage.getItem("access_token") ?? "";
    const formData = new FormData();
    formData.append("file", file);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/api/uploads`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
      body: formData,
    });
    if (!res.ok) throw new Error("فشل رفع الملف");
    const data = await res.json() as { url: string };
    return data.url;
  };

  const fetchXrays = () => {
    setLoading(true);
    api.get<RadiographItem[]>(`/api/radiographs/${patientId}`)
      .then((r) => setXrays(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchXrays();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId]);

  const handleAddXray = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!xrayFile) return;
    setAddingXray(true);
    try {
      const fileUrl = await uploadFile(xrayFile);
      await api.post("/api/radiographs", {
        patientId,
        fileUrl,
        xrayType,
        notes: xrayNotes || undefined,
      });
      setXrayFile(null);
      setXrayNotes("");
      fetchXrays();
      toast.success("تم رفع ملف الأشعة بنجاح");
    } catch {
      toast.error("فشل رفع ملف الأشعة — تحقق من حجم الملف والاتصال");
    } finally {
      setAddingXray(false);
    }
  };

  const handleDeleteXray = async (id: string) => {
    try {
      await api.delete(`/api/radiographs/${id}`);
      setXrays((prev) => prev.filter((x) => x.id !== id));
      toast.success("تم حذف ملف الأشعة");
    } catch {
      toast.error("فشل حذف ملف الأشعة — حاول مجدداً");
    }
  };

  return (
    <div className="space-y-4" dir="rtl">
      {/* Add xray form */}
      <form onSubmit={handleAddXray} className="bg-[#f7fafd] rounded-lg p-4 border border-[#e8f0f9] space-y-3">
        <p className="text-xs font-semibold text-[#64748b] mb-1">إضافة أشعة</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="sm:col-span-2">
            <label className="text-xs text-[#64748b] block mb-1">ملف الأشعة *</label>
            <label className={cn(
              "flex items-center gap-3 w-full border-2 border-dashed rounded-lg px-4 py-3 cursor-pointer transition",
              xrayFile ? "border-clinic-blue bg-clinic-blue-50" : "border-[#e8f0f9] hover:border-[#c8d8e8]"
            )}>
              <input type="file" accept="image/*,.pdf,.dcm" className="sr-only" onChange={(e) => setXrayFile(e.target.files?.[0] ?? null)} />
              <div className="w-12 h-12 rounded-lg bg-[#f1f5f9] flex items-center justify-center flex-shrink-0">
                <ScanLine className="w-5 h-5 text-[#94a3b8]" />
              </div>
              <div className="min-w-0">
                <p className="text-sm font-medium text-[#0d2137]">
                  {xrayFile ? xrayFile.name : "اختر ملف أشعة أو اسحب وأفلت"}
                </p>
                <p className="text-xs text-[#94a3b8]">JPG، PNG، PDF، DICOM — حتى 10 ميجابايت</p>
              </div>
            </label>
          </div>
          <div>
            <label className="text-xs text-[#64748b] block mb-1">نوع الأشعة</label>
            <select
              value={xrayType}
              onChange={(e) => setXrayType(e.target.value)}
              className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue bg-white"
            >
              <option value="OPG">OPG</option>
              <option value="lateral_ceph">Lateral Ceph</option>
              <option value="bitewing">Bitewing</option>
              <option value="periapical">Periapical</option>
              <option value="PA_ceph">PA Ceph</option>
              <option value="CBCT">CBCT</option>
            </select>
          </div>
          <div className="sm:col-span-2">
            <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
            <input
              type="text"
              value={xrayNotes}
              onChange={(e) => setXrayNotes(e.target.value)}
              placeholder="ملاحظات اختيارية..."
              className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue"
            />
          </div>
        </div>
        <button
          type="submit"
          disabled={addingXray || !xrayFile}
          className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition disabled:opacity-50"
        >
          <Plus className="w-3.5 h-3.5" />
          {addingXray ? "جارٍ الإضافة..." : "إضافة أشعة"}
        </button>
      </form>

      {/* Xrays list */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-14 bg-[#f1f5f9] rounded-lg" />
          ))}
        </div>
      ) : xrays.length === 0 ? (
        <div className="text-center py-8 text-[#94a3b8]">
          <ScanLine className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-sm">لا توجد أشعة مسجّلة</p>
        </div>
      ) : (
        <div className="space-y-2">
          {xrays.map((xray) => (
            <div key={xray.id} className="flex items-center gap-3 bg-white border border-[#e8f0f9] rounded-lg p-3 hover:border-[#e8f0f9] transition">
              <div className="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center flex-shrink-0">
                <FileText className="w-5 h-5 text-purple-400" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap mb-0.5">
                  <span className="text-xs font-semibold text-[#0d2137]">
                    {XRAY_TYPE_LABELS[xray.xrayType] ?? xray.xrayType}
                  </span>
                  {xray.toothRelated && (
                    <span className="text-xs text-[#64748b]">سن: {xray.toothRelated}</span>
                  )}
                  {xray.doctorName && (
                    <span className="text-xs text-[#64748b]">{xray.doctorName}</span>
                  )}
                </div>
                <a
                  href={xray.fileUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs text-clinic-blue hover:underline truncate flex items-center gap-1"
                >
                  <ExternalLink className="w-3 h-3 flex-shrink-0" />
                  <span className="truncate">{xray.fileUrl}</span>
                </a>
                <p className="text-xs text-[#94a3b8] mt-0.5">{xray.xrayDate}</p>
                {xray.notes && <p className="text-xs text-[#64748b] mt-0.5 line-clamp-1">{xray.notes}</p>}
              </div>
              <button
                onClick={() => handleDeleteXray(xray.id)}
                className="flex-shrink-0 p-1.5 rounded-lg text-[#94a3b8] hover:text-red-500 hover:bg-red-50 transition"
                title="حذف"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
