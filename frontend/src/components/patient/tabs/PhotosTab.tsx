"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { Image as ImageIcon, Plus, Trash2, X, Upload, Camera, Filter, Eye } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate, localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { ImagePreviewModal } from "@/components/shared/ImagePreviewModal";
import { resolveImageUrl } from "@/hooks/useClinicBranding";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface ClinicalPhotoItem {
  id: string;
  category: string;
  photoType?: string;
  fileUrl: string;
  thumbnailUrl?: string;
  stage?: string;
  notes?: string;
  fileSize?: number;
  photoDate: string;
  orthoCaseId?: string;
  isActive?: boolean;
  createdAt?: string;
}

// ─── Photo Categories (Sprint 4 spec) ───────────────────────────────────────────

const PHOTO_CATEGORIES: Record<string, string> = {
  extraoral: "خارج فموية",
  intraoral: "داخل فموية",
  smile: "ابتسامة",
  frontal: "أمامية",
  profile: "جانبية",
  occlusal_upper: "إطباقية علوية",
  occlusal_lower: "إطباقية سفلية",
  other: "أخرى",
};

const CATEGORY_COLORS: Record<string, string> = {
  extraoral: "bg-blue-100 text-blue-700",
  intraoral: "bg-cyan-100 text-cyan-700",
  smile: "bg-yellow-100 text-yellow-700",
  frontal: "bg-green-100 text-green-700",
  profile: "bg-purple-100 text-purple-700",
  occlusal_upper: "bg-orange-100 text-orange-700",
  occlusal_lower: "bg-pink-100 text-pink-700",
  other: "bg-gray-100 text-gray-700",
};

const STAGE_LABELS: Record<string, string> = {
  initial: "أولية",
  progress: "متابعة",
  final: "نهائية",
};

const STAGE_COLORS: Record<string, string> = {
  initial: "bg-blue-100 text-blue-700",
  progress: "bg-yellow-100 text-yellow-700",
  final: "bg-green-100 text-green-700",
};

// Allowed image types
const ALLOWED_IMAGE_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB

// ─── Component ──────────────────────────────────────────────────────────────────

interface PhotosTabProps {
  patientId: string;
  orthoCaseId?: string;
}

export function PhotosTab({ patientId, orthoCaseId }: PhotosTabProps) {
  const [photos, setPhotos] = useState<ClinicalPhotoItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showAddModal, setShowAddModal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [filterCategory, setFilterCategory] = useState("");
  const [filterStage, setFilterStage] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // Form state
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [photoCategory, setPhotoCategory] = useState("intraoral");
  const [photoType, setPhotoType] = useState("");
  const [photoStage, setPhotoStage] = useState("");
  const [photoNotes, setPhotoNotes] = useState("");
  const [photoDate, setPhotoDate] = useState(() => localDateString());
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Image preview state
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);

  const fetchPhotos = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterCategory) params.set("category", filterCategory);
    if (filterStage) params.set("stage", filterStage);
    const qs = params.toString();
    api.get<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}${qs ? `?${qs}` : ""}${orthoCaseId ? `${qs ? "&" : "?"}orthoCaseId=${orthoCaseId}` : ""}`)
      .then((r) => setPhotos(r.data))
      .catch(() => setError("فشل تحميل الصور"))
      .finally(() => setLoading(false));
  }, [patientId, filterCategory, filterStage, orthoCaseId]);

  useEffect(() => {
    fetchPhotos();
  }, [fetchPhotos]);

  const openAddModal = () => {
    setPhotoFile(null);
    setPhotoPreview(null);
    setPhotoCategory("intraoral");
    setPhotoType("");
    setPhotoStage("");
    setPhotoNotes("");
    setPhotoDate(localDateString());
    setShowAddModal(true);
  };

  const handlePhotoFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;

    if (f) {
      // Validate file type
      const ext = "." + f.name.split(".").pop()?.toLowerCase();
      if (!ALLOWED_IMAGE_EXTENSIONS.includes(ext)) {
        toast.error("نوع الملف غير مدعوم. المسموح: JPG, PNG, WebP فقط");
        return;
      }
      if (f.size > MAX_FILE_SIZE) {
        toast.error("حجم الصورة يتجاوز 10 ميجابايت");
        return;
      }
      if (f.type.startsWith("image/")) {
        const reader = new FileReader();
        reader.onload = (ev) => setPhotoPreview(ev.target?.result as string);
        reader.readAsDataURL(f);
      } else {
        setPhotoPreview(null);
      }
    }
    setPhotoFile(f);
  };

  const handleAddPhoto = async () => {
    if (!photoFile) {
      toast.error("يجب اختيار صورة أولاً");
      return;
    }
    setSaving(true);
    setUploading(true);
    try {
      // Upload file first
      const formData = new FormData();
      formData.append("file", photoFile);
      const uploadRes = await api.post<{ url: string }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setUploading(false);

      // Then create photo record
      await api.post("/api/clinical-photos", {
        patientId,
        fileUrl: uploadRes.data.url,
        category: photoCategory,
        photoType: photoType || undefined,
        stage: photoStage || undefined,
        notes: photoNotes || undefined,
        photoDate: photoDate || undefined,
      });

      toast.success("تم رفع الصورة بنجاح");
      setShowAddModal(false);
      fetchPhotos();
    } catch {
      toast.error("فشل رفع الصورة — تحقق من حجم الملف والاتصال");
    } finally {
      setSaving(false);
      setUploading(false);
    }
  };

  const handleDeletePhoto = async (id: string) => {
    try {
      await api.delete(`/api/clinical-photos/${id}`);
      toast.success("تم حذف الصورة");
      setDeleteConfirm(null);
      fetchPhotos();
    } catch {
      toast.error("فشل حذف الصورة — حاول مجدداً");
    }
  };

  const openImagePreview = (index: number) => {
    setPreviewIndex(index);
    setPreviewOpen(true);
  };

  // Stats
  const activePhotos = photos;
  const categoryCount = new Set(activePhotos.map(p => p.category).filter(Boolean)).size;

  // Build preview items for the ImagePreviewModal
  const previewItems = activePhotos.map(p => ({
    url: resolveImageUrl(p.fileUrl),
    fileName: `${PHOTO_CATEGORIES[p.category] ?? p.category}${p.photoType ? ` - ${p.photoType}` : ""}`,
    mimeType: "image/jpeg",
  }));

  return (
    <div className="space-y-4">
      {/* Stats row */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-[#3d7ab510]">
          <Camera className="w-4 h-4 flex-shrink-0 text-[#3d7ab5]" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">الصور</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{activePhotos.length}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-purple-50">
          <Filter className="w-4 h-4 flex-shrink-0 text-purple-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">الفئات</p>
            <p className="text-sm font-bold text-purple-500">{categoryCount}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-green-50">
          <ImageIcon className="w-4 h-4 flex-shrink-0 text-green-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">آخر رفع</p>
            <p className="text-sm font-bold text-green-500">{activePhotos[0]?.photoDate ? formatArabicDate(activePhotos[0].photoDate) : "—"}</p>
          </div>
        </div>
      </div>

      {/* Header with filters and add button */}
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h3 className="text-sm font-bold text-[#0d2137]">الصور السريرية</h3>
        <div className="flex items-center gap-2 flex-wrap">
          {/* Category filter */}
          <select
            value={filterCategory}
            onChange={(e) => setFilterCategory(e.target.value)}
            className="text-xs border border-[#e8f0f9] rounded-lg px-2 py-1.5 focus:outline-none focus:border-[#3d7ab5] bg-white"
          >
            <option value="">كل الفئات</option>
            {Object.entries(PHOTO_CATEGORIES).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
          {/* Stage filter */}
          <select
            value={filterStage}
            onChange={(e) => setFilterStage(e.target.value)}
            className="text-xs border border-[#e8f0f9] rounded-lg px-2 py-1.5 focus:outline-none focus:border-[#3d7ab5] bg-white"
          >
            <option value="">كل المراحل</option>
            {Object.entries(STAGE_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
          {/* Add button */}
          <button
            onClick={openAddModal}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            إضافة صورة
          </button>
        </div>
      </div>

      {/* Loading */}
      {loading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3 animate-pulse">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="aspect-square bg-[#f1f5f9] rounded-xl" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-8 text-red-500 text-sm">{error}</div>
      ) : activePhotos.length === 0 ? (
        <EmptyState
          icon={ImageIcon}
          title="لا توجد صور"
          description="لم يتم رفع أي صور لهذا المريض"
          actionLabel="رفع صورة"
          actionOnClick={openAddModal}
          actionVariant="secondary"
        />
      ) : (
        /* Photo Gallery Grid */
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
          {activePhotos.map((photo, idx) => (
            <div
              key={photo.id}
              className="group relative bg-white border border-[#e8f0f9] rounded-xl overflow-hidden hover:shadow-md transition"
            >
              {/* Thumbnail */}
              <div
                className="aspect-square bg-[#f1f5f9] cursor-pointer overflow-hidden"
                onClick={() => openImagePreview(idx)}
              >
                {photo.thumbnailUrl || photo.fileUrl ? (
                  <>
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={resolveImageUrl(photo.thumbnailUrl || photo.fileUrl)}
                    alt={PHOTO_CATEGORIES[photo.category] ?? photo.category}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-200"
                  />
                  </>
                ) : (
                  <div className="w-full h-full flex items-center justify-center">
                    <ImageIcon className="w-8 h-8 text-[#94a3b8] opacity-40" />
                  </div>
                )}
              </div>

              {/* Info overlay */}
              <div className="p-2.5 space-y-1">
                <div className="flex items-center gap-1.5 flex-wrap">
                  {photo.category && (
                    <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full font-medium", CATEGORY_COLORS[photo.category] ?? "bg-gray-100 text-gray-600")}>
                      {PHOTO_CATEGORIES[photo.category] ?? photo.category}
                    </span>
                  )}
                  {photo.stage && (
                    <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full font-medium", STAGE_COLORS[photo.stage] ?? "bg-gray-100 text-gray-600")}>
                      {STAGE_LABELS[photo.stage] ?? photo.stage}
                    </span>
                  )}
                </div>
                {photo.photoType && (
                  <p className="text-[11px] text-[#64748b] truncate">{photo.photoType}</p>
                )}
                <p className="text-[10px] text-[#94a3b8]">{formatArabicDate(photo.photoDate)}</p>
                {photo.notes && (
                  <p className="text-[10px] text-[#94a3b8] truncate">{photo.notes}</p>
                )}
              </div>

              {/* Hover action buttons */}
              <div className="absolute top-2 left-2 flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <button
                  onClick={() => openImagePreview(idx)}
                  className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                  title="عرض بحجم كامل"
                >
                  <Eye className="w-3.5 h-3.5 text-[#3d7ab5]" />
                </button>
                {deleteConfirm === photo.id ? (
                  <div className="flex gap-1">
                    <button
                      onClick={() => handleDeletePhoto(photo.id)}
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
                    onClick={() => setDeleteConfirm(photo.id)}
                    className="w-7 h-7 rounded-full bg-white/90 hover:bg-white flex items-center justify-center shadow-sm"
                    title="حذف"
                  >
                    <Trash2 className="w-3.5 h-3.5 text-red-400" />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ─── Image Preview Modal ────────────────────────────────────────────── */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={previewItems[previewIndex]?.url ?? ""}
        fileName={previewItems[previewIndex]?.fileName}
        mimeType={previewItems[previewIndex]?.mimeType}
        items={previewItems}
        currentIndex={previewIndex}
        onNavigate={setPreviewIndex}
      />

      {/* ─── Add Photo Modal ──────────────────────────────────────────────────── */}
      {showAddModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowAddModal(false)}>
          <div
            className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">إضافة صورة سريرية</h3>
                <button onClick={() => setShowAddModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="space-y-3">
                {/* File upload */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الصورة *</label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".jpg,.jpeg,.png,.webp"
                    onChange={handlePhotoFileChange}
                    className="hidden"
                  />
                  {photoPreview ? (
                    <div className="relative">
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img
                        src={photoPreview}
                        alt="preview"
                        className="w-full h-48 object-cover rounded-lg border border-[#e8f0f9]"
                      />
                      <button
                        onClick={() => { setPhotoFile(null); setPhotoPreview(null); }}
                        className="absolute top-2 left-2 w-7 h-7 rounded-full bg-black/50 hover:bg-black/70 flex items-center justify-center text-white transition"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => fileInputRef.current?.click()}
                      className={cn(
                        "w-full flex items-center justify-center gap-2 p-6 border-2 border-dashed border-[#e8f0f9] rounded-lg text-sm transition",
                        uploading ? "opacity-60 cursor-not-allowed" : "hover:border-[#3d7ab5] hover:bg-[#f8fafc]"
                      )}
                    >
                      <Upload className="w-4 h-4 text-[#94a3b8]" />
                      {uploading ? "جاري الرفع..." : "اختر صورة (JPG, PNG, WebP — حتى 10 ميجابايت)"}
                    </button>
                  )}
                </div>

                {/* Category */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الفئة *</label>
                  <select
                    value={photoCategory}
                    onChange={(e) => setPhotoCategory(e.target.value)}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                  >
                    {Object.entries(PHOTO_CATEGORIES).map(([k, v]) => (
                      <option key={k} value={k}>{v}</option>
                    ))}
                  </select>
                </div>

                {/* Photo Type (free text) */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">نوع الصورة</label>
                  <input
                    type="text"
                    value={photoType}
                    onChange={(e) => setPhotoType(e.target.value)}
                    placeholder="مثال: أمامية مريحة، ابتسامة..."
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Stage + Date row */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">المرحلة</label>
                    <select
                      value={photoStage}
                      onChange={(e) => setPhotoStage(e.target.value)}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— غير محدد —</option>
                      {Object.entries(STAGE_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التاريخ</label>
                    <input
                      type="date"
                      value={photoDate}
                      onChange={(e) => setPhotoDate(e.target.value)}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                </div>

                {/* Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
                  <textarea
                    value={photoNotes}
                    onChange={(e) => setPhotoNotes(e.target.value)}
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
                  onClick={handleAddPhoto}
                  disabled={saving || !photoFile}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving || !photoFile ? "bg-[#3d7ab5]/60 cursor-not-allowed" : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                  )}
                >
                  {saving ? (uploading ? "جاري الرفع..." : "جاري الحفظ...") : "إضافة صورة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
