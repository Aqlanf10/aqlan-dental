"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { ScanLine, Plus, Trash2, X, Upload, FileText, Filter, Eye, Download } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { ImagePreviewModal } from "@/components/shared/ImagePreviewModal";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface RadiographItem {
  id: string;
  xrayType: string;
  fileUrl: string;
  fileName?: string;
  fileSize?: number;
  mimeType?: string;
  toothRelated?: string;
  notes?: string;
  xrayDate: string;
  doctorName?: string;
  isActive?: boolean;
  createdAt?: string;
}

// ─── X-Ray Types (Sprint 4 spec) ────────────────────────────────────────────────

const XRAY_TYPE_LABELS: Record<string, string> = {
  OPG: "بانوراما OPG",
  lateral_ceph: "سيفالومتري جانبي",
  PA_ceph: "سيفالومتري أمامي خلفي",
  periapical: "حول ذروية",
  bitewing: "Bitewing",
  CBCT: "CBCT",
  other: "أخرى",
};

const XRAY_TYPE_COLORS: Record<string, string> = {
  OPG: "bg-blue-100 text-blue-700",
  lateral_ceph: "bg-purple-100 text-purple-700",
  PA_ceph: "bg-indigo-100 text-indigo-700",
  periapical: "bg-cyan-100 text-cyan-700",
  bitewing: "bg-blue-100 text-blue-700",
  CBCT: "bg-orange-100 text-orange-700",
  other: "bg-gray-100 text-gray-700",
};

// Allowed types for radiographs
const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB

function formatFileSize(bytes?: number | null): string {
  if (!bytes) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// ─── Component ──────────────────────────────────────────────────────────────────

interface RadiographsTabProps {
  patientId: string;
}

export function RadiographsTab({ patientId }: RadiographsTabProps) {
  const [xrays, setXrays] = useState<RadiographItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showAddModal, setShowAddModal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [filterType, setFilterType] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // Form state
  const [xrayFile, setXrayFile] = useState<File | null>(null);
  const [xrayType, setXrayType] = useState("OPG");
  const [xrayNotes, setXrayNotes] = useState("");
  const [xrayDate, setXrayDate] = useState(() => new Date().toISOString().split("T")[0]);
  const [xrayPreview, setXrayPreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Image preview state
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewUrl, setPreviewUrl] = useState("");
  const [previewFileName, setPreviewFileName] = useState<string | undefined>();
  const [previewMimeType, setPreviewMimeType] = useState<string | undefined>();

  const fetchXrays = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterType) params.set("xrayType", filterType);
    const qs = params.toString();
    api.get<RadiographItem[]>(`/api/radiographs/${patientId}${qs ? `?${qs}` : ""}`)
      .then((r) => setXrays(r.data))
      .catch(() => setError("فشل تحميل الأشعة"))
      .finally(() => setLoading(false));
  }, [patientId, filterType]);

  useEffect(() => {
    fetchXrays();
  }, [fetchXrays]);

  const openAddModal = () => {
    setXrayFile(null);
    setXrayType("OPG");
    setXrayNotes("");
    setXrayDate(new Date().toISOString().split("T")[0]);
    setXrayPreview(null);
    setShowAddModal(true);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;

    if (f) {
      // Validate file type
      const ext = "." + f.name.split(".").pop()?.toLowerCase();
      if (!ALLOWED_EXTENSIONS.includes(ext)) {
        toast.error("نوع الملف غير مدعوم. المسموح: JPG, PNG, WebP, PDF");
        return;
      }
      if (f.size > MAX_FILE_SIZE) {
        toast.error("حجم الملف يتجاوز 10 ميجابايت");
        return;
      }
      // Preview for images
      if (f.type.startsWith("image/")) {
        const reader = new FileReader();
        reader.onload = (ev) => setXrayPreview(ev.target?.result as string);
        reader.readAsDataURL(f);
      } else {
        setXrayPreview(null);
      }
    }
    setXrayFile(f);
  };

  const handleAddXray = async () => {
    if (!xrayFile) {
      toast.error("يجب اختيار ملف أشعة أولاً");
      return;
    }
    setSaving(true);
    setUploading(true);
    try {
      // Upload file
      const formData = new FormData();
      formData.append("file", xrayFile);
      const uploadRes = await api.post<{ url: string; originalName: string; size: number; contentType: string }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setUploading(false);

      // Create radiograph record
      await api.post("/api/radiographs", {
        patientId,
        fileUrl: uploadRes.data.url,
        fileName: uploadRes.data.originalName,
        fileSize: uploadRes.data.size,
        mimeType: uploadRes.data.contentType,
        xrayType,
        notes: xrayNotes || undefined,
        xrayDate: xrayDate || undefined,
      });

      toast.success("تم رفع الأشعة بنجاح");
      setShowAddModal(false);
      fetchXrays();
    } catch {
      toast.error("فشل رفع الأشعة — تحقق من حجم الملف والاتصال");
    } finally {
      setSaving(false);
      setUploading(false);
    }
  };

  const handleDeleteXray = async (id: string) => {
    try {
      await api.delete(`/api/radiographs/${id}`);
      toast.success("تم حذف الأشعة");
      setDeleteConfirm(null);
      fetchXrays();
    } catch {
      toast.error("فشل حذف الأشعة — حاول مجدداً");
    }
  };

  const openPreview = (xray: RadiographItem) => {
    if (!xray.fileUrl) return;
    setPreviewUrl(xray.fileUrl);
    setPreviewFileName(xray.fileName ?? XRAY_TYPE_LABELS[xray.xrayType] ?? xray.xrayType);
    setPreviewMimeType(xray.mimeType);
    setPreviewOpen(true);
  };

  const isImageFile = (mimeType?: string | null) => !mimeType || mimeType.startsWith("image/");

  // Stats
  const activeXrays = xrays;
  const typeCount = new Set(activeXrays.map(x => x.xrayType).filter(Boolean)).size;

  return (
    <div className="space-y-4" dir="rtl">
      {/* Stats row */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-purple-50">
          <ScanLine className="w-4 h-4 flex-shrink-0 text-purple-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">الأشعة</p>
            <p className="text-sm font-bold text-purple-500">{activeXrays.length}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-[#3d7ab510]">
          <Filter className="w-4 h-4 flex-shrink-0 text-[#3d7ab5]" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">الأنواع</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{typeCount}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-green-50">
          <ScanLine className="w-4 h-4 flex-shrink-0 text-green-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">آخر رفع</p>
            <p className="text-sm font-bold text-green-500">{activeXrays[0]?.xrayDate ? formatArabicDate(activeXrays[0].xrayDate) : "—"}</p>
          </div>
        </div>
      </div>

      {/* Header with filters and add button */}
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h3 className="text-sm font-bold text-[#0d2137]">الأشعة السنية</h3>
        <div className="flex items-center gap-2 flex-wrap">
          {/* Type filter */}
          <select
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
            className="text-xs border border-[#e8f0f9] rounded-lg px-2 py-1.5 focus:outline-none focus:border-[#3d7ab5] bg-white"
          >
            <option value="">كل الأنواع</option>
            {Object.entries(XRAY_TYPE_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
          {/* Add button */}
          <button
            onClick={openAddModal}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            إضافة أشعة
          </button>
        </div>
      </div>

      {/* Loading */}
      {loading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="aspect-square bg-[#f1f5f9] rounded-xl" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-8 text-red-500 text-sm">{error}</div>
      ) : activeXrays.length === 0 ? (
        <EmptyState
          icon={ScanLine}
          title="لا توجد أشعة"
          description="لم يتم رفع أي أشعة لهذا المريض"
          actionLabel="رفع أشعة"
          actionOnClick={openAddModal}
          actionVariant="secondary"
        />
      ) : (
        /* Radiograph Gallery Grid */
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
          {activeXrays.map((xray) => {
            const isImage = isImageFile(xray.mimeType);
            return (
              <div
                key={xray.id}
                className="group relative bg-white border border-[#e8f0f9] rounded-xl overflow-hidden hover:shadow-md transition"
              >
                {/* Thumbnail area */}
                <div
                  className="aspect-video bg-[#f1f5f9] cursor-pointer overflow-hidden flex items-center justify-center"
                  onClick={() => openPreview(xray)}
                >
                  {isImage && xray.fileUrl ? (
                    <img
                      src={xray.fileUrl}
                      alt={XRAY_TYPE_LABELS[xray.xrayType] ?? xray.xrayType}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-200"
                    />
                  ) : (
                    <div className="flex flex-col items-center gap-2 text-[#94a3b8]">
                      <FileText className="w-8 h-8 opacity-40" />
                      <span className="text-xs">PDF</span>
                    </div>
                  )}
                </div>

                {/* Info */}
                <div className="p-3 space-y-1.5">
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", XRAY_TYPE_COLORS[xray.xrayType] ?? "bg-gray-100 text-gray-600")}>
                      {XRAY_TYPE_LABELS[xray.xrayType] ?? xray.xrayType}
                    </span>
                    {xray.toothRelated && (
                      <span className="text-xs bg-[#f1f5f9] text-[#64748b] px-1.5 py-0.5 rounded">
                        سن: {xray.toothRelated}
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-3 text-[10px] text-[#94a3b8]">
                    <span>{formatArabicDate(xray.xrayDate)}</span>
                    {xray.doctorName && <span>{xray.doctorName}</span>}
                    {xray.fileSize != null && xray.fileSize > 0 && <span>{formatFileSize(xray.fileSize)}</span>}
                  </div>
                  {xray.notes && (
                    <p className="text-[11px] text-[#64748b] truncate">{xray.notes}</p>
                  )}
                </div>

                {/* Hover action buttons */}
                <div className="absolute top-2 left-2 flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                  {isImage ? (
                    <button
                      onClick={() => openPreview(xray)}
                      className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                      title="عرض بحجم كامل"
                    >
                      <Eye className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    </button>
                  ) : (
                    <a
                      href={xray.fileUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                      title="فتح PDF"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <Download className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    </a>
                  )}
                  {deleteConfirm === xray.id ? (
                    <div className="flex gap-1">
                      <button
                        onClick={() => handleDeleteXray(xray.id)}
                        className="w-7 h-7 rounded-full bg-red-500 flex items-center justify-center shadow-sm"
                        title="تأكيد"
                      >
                        <Trash2 className="w-3.5 h-3.5 text-white" />
                      </button>
                      <button
                        onClick={() => setDeleteConfirm(null)}
                        className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                        title="إلغاء"
                      >
                        <X className="w-3.5 h-3.5 text-[#64748b]" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setDeleteConfirm(xray.id)}
                      className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                      title="حذف"
                    >
                      <Trash2 className="w-3.5 h-3.5 text-red-400" />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ─── Image/PDF Preview Modal ────────────────────────────────────────── */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={previewUrl}
        fileName={previewFileName}
        mimeType={previewMimeType}
      />

      {/* ─── Add Radiograph Modal ──────────────────────────────────────────────── */}
      {showAddModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowAddModal(false)}>
          <div
            className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 space-y-4" dir="rtl">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">إضافة أشعة سنية</h3>
                <button onClick={() => setShowAddModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="space-y-3">
                {/* File upload */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملف الأشعة *</label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".jpg,.jpeg,.png,.webp,.pdf"
                    onChange={handleFileChange}
                    className="hidden"
                  />
                  {xrayPreview ? (
                    <div className="relative">
                      <img
                        src={xrayPreview}
                        alt="preview"
                        className="w-full h-48 object-cover rounded-lg border border-[#e8f0f9]"
                      />
                      <button
                        onClick={() => { setXrayFile(null); setXrayPreview(null); }}
                        className="absolute top-2 left-2 w-7 h-7 rounded-full bg-black/50 hover:bg-black/70 flex items-center justify-center text-white transition"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  ) : xrayFile ? (
                    <div className="flex items-center gap-2 p-2.5 bg-[#f8fafc] border border-[#e8f0f9] rounded-lg">
                      <FileText className="w-4 h-4 text-[#3d7ab5]" />
                      <span className="text-sm text-[#0d2137] truncate flex-1" dir="ltr">{xrayFile.name}</span>
                      <button
                        onClick={() => { setXrayFile(null); setXrayPreview(null); }}
                        className="text-[#94a3b8] hover:text-red-500"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => fileInputRef.current?.click()}
                      disabled={uploading}
                      className={cn(
                        "w-full flex items-center justify-center gap-2 p-6 border-2 border-dashed border-[#e8f0f9] rounded-lg text-sm transition",
                        uploading ? "opacity-60 cursor-not-allowed" : "hover:border-[#3d7ab5] hover:bg-[#f8fafc]"
                      )}
                    >
                      <Upload className="w-4 h-4 text-[#94a3b8]" />
                      {uploading ? "جاري الرفع..." : "اختر ملف (JPG, PNG, WebP, PDF — حتى 10 ميجابايت)"}
                    </button>
                  )}
                </div>

                {/* X-Ray Type + Date */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">نوع الأشعة *</label>
                    <select
                      value={xrayType}
                      onChange={(e) => setXrayType(e.target.value)}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      {Object.entries(XRAY_TYPE_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التاريخ</label>
                    <input
                      type="date"
                      value={xrayDate}
                      onChange={(e) => setXrayDate(e.target.value)}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                </div>

                {/* Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
                  <textarea
                    value={xrayNotes}
                    onChange={(e) => setXrayNotes(e.target.value)}
                    placeholder="ملاحظات اختيارية..."
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={() => setShowAddModal(false)}
                  className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  onClick={handleAddXray}
                  disabled={saving || !xrayFile}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving || !xrayFile ? "bg-[#3d7ab5]/60 cursor-not-allowed" : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                  )}
                >
                  {saving ? (uploading ? "جاري الرفع..." : "جاري الحفظ...") : "إضافة أشعة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
