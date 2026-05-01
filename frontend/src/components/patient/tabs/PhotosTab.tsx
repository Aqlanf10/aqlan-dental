"use client";

import { useEffect, useState } from "react";
import { Image as ImageIcon, Plus, Trash2, ExternalLink } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

interface ClinicalPhotoItem {
  id: string;
  category: string;
  photoType?: string;
  fileUrl: string;
  thumbnailUrl?: string;
  stage?: string;
  notes?: string;
  photoDate: string;
  orthoCaseId?: string;
}

const CATEGORY_LABELS: Record<string, string> = {
  intraoral: "داخل الفم",
  extraoral: "خارج الفم",
};

const STAGE_LABELS: Record<string, string> = {
  initial: "أولية",
  progress: "متابعة",
  final: "نهائية",
};

interface PhotosTabProps {
  patientId: string;
}

export function PhotosTab({ patientId }: PhotosTabProps) {
  const [photos, setPhotos] = useState<ClinicalPhotoItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Form state
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoCategory, setPhotoCategory] = useState("intraoral");
  const [photoType, setPhotoType] = useState("");
  const [photoStage, setPhotoStage] = useState("");
  const [photoNotes, setPhotoNotes] = useState("");
  const [addingPhoto, setAddingPhoto] = useState(false);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);

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

  const fetchPhotos = () => {
    setLoading(true);
    api.get<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}`)
      .then((r) => setPhotos(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchPhotos();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId]);

  const handlePhotoFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;
    setPhotoFile(f);
    if (f && f.type.startsWith("image/")) {
      const reader = new FileReader();
      reader.onload = (ev) => setPhotoPreview(ev.target?.result as string);
      reader.readAsDataURL(f);
    } else {
      setPhotoPreview(null);
    }
  };

  const handleAddPhoto = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!photoFile) return;
    setAddingPhoto(true);
    try {
      const fileUrl = await uploadFile(photoFile);
      await api.post("/api/clinical-photos", {
        patientId,
        fileUrl,
        category: photoCategory,
        photoType: photoType || undefined,
        stage: photoStage || undefined,
        notes: photoNotes || undefined,
      });
      setPhotoFile(null);
      setPhotoPreview(null);
      setPhotoType("");
      setPhotoStage("");
      setPhotoNotes("");
      fetchPhotos();
      toast.success("تم رفع الصورة بنجاح");
    } catch {
      toast.error("فشل رفع الصورة — تحقق من حجم الملف والاتصال");
    } finally {
      setAddingPhoto(false);
    }
  };

  const handleDeletePhoto = async (id: string) => {
    try {
      await api.delete(`/api/clinical-photos/${id}`);
      setPhotos((prev) => prev.filter((p) => p.id !== id));
      toast.success("تم حذف الصورة");
    } catch {
      toast.error("فشل حذف الصورة — حاول مجدداً");
    }
  };

  return (
    <div className="space-y-4" dir="rtl">
      {/* Add photo form */}
      <form onSubmit={handleAddPhoto} className="bg-gray-50 rounded-lg p-4 border border-gray-100 space-y-3">
        <p className="text-xs font-semibold text-gray-500 mb-1">إضافة صورة</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="sm:col-span-2">
            <label className="text-xs text-gray-500 block mb-1">الصورة *</label>
            <label className={cn(
              "flex items-center gap-3 w-full border-2 border-dashed rounded-lg px-4 py-3 cursor-pointer transition",
              photoFile ? "border-clinic-teal bg-teal-50" : "border-gray-200 hover:border-gray-300"
            )}>
              <input type="file" accept="image/*,.pdf" className="sr-only" onChange={handlePhotoFileChange} />
              {photoPreview ? (
                <img src={photoPreview} alt="preview" className="w-12 h-12 rounded-lg object-cover flex-shrink-0" />
              ) : (
                <div className="w-12 h-12 rounded-lg bg-gray-100 flex items-center justify-center flex-shrink-0">
                  <ImageIcon className="w-5 h-5 text-gray-400" />
                </div>
              )}
              <div className="min-w-0">
                <p className="text-sm font-medium text-gray-700">
                  {photoFile ? photoFile.name : "اختر صورة أو اسحب وأفلت"}
                </p>
                <p className="text-xs text-gray-400">JPG، PNG، WebP — حتى 10 ميجابايت</p>
              </div>
            </label>
          </div>
          <div>
            <label className="text-xs text-gray-500 block mb-1">الفئة</label>
            <select
              value={photoCategory}
              onChange={(e) => setPhotoCategory(e.target.value)}
              className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal bg-white"
            >
              <option value="intraoral">داخل الفم</option>
              <option value="extraoral">خارج الفم</option>
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-500 block mb-1">نوع الصورة</label>
            <input
              type="text"
              value={photoType}
              onChange={(e) => setPhotoType(e.target.value)}
              placeholder="مثال: frontal, lateral..."
              className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
            />
          </div>
          <div>
            <label className="text-xs text-gray-500 block mb-1">المرحلة</label>
            <select
              value={photoStage}
              onChange={(e) => setPhotoStage(e.target.value)}
              className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal bg-white"
            >
              <option value="">— غير محدد —</option>
              <option value="initial">أولية</option>
              <option value="progress">متابعة</option>
              <option value="final">نهائية</option>
            </select>
          </div>
          <div className="sm:col-span-2">
            <label className="text-xs text-gray-500 block mb-1">ملاحظات</label>
            <input
              type="text"
              value={photoNotes}
              onChange={(e) => setPhotoNotes(e.target.value)}
              placeholder="ملاحظات اختيارية..."
              className="w-full text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
            />
          </div>
        </div>
        <button
          type="submit"
          disabled={addingPhoto || !photoFile}
          className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition disabled:opacity-50"
        >
          <Plus className="w-3.5 h-3.5" />
          {addingPhoto ? "جارٍ الرفع..." : "إضافة صورة"}
        </button>
      </form>

      {/* Photos grid */}
      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-lg" />
          ))}
        </div>
      ) : photos.length === 0 ? (
        <div className="text-center py-8 text-gray-400">
          <ImageIcon className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-sm">لا توجد صور سريرية</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {photos.map((photo) => (
            <div key={photo.id} className="flex items-start gap-3 bg-white border border-gray-100 rounded-lg p-3 hover:border-gray-200 transition">
              <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center flex-shrink-0">
                <ImageIcon className="w-5 h-5 text-gray-400" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap mb-0.5">
                  <span className="text-xs font-semibold text-gray-700">
                    {CATEGORY_LABELS[photo.category] ?? photo.category}
                  </span>
                  {photo.photoType && (
                    <span className="text-xs text-gray-500">{photo.photoType}</span>
                  )}
                  {photo.stage && (
                    <span className="text-xs bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded">
                      {STAGE_LABELS[photo.stage] ?? photo.stage}
                    </span>
                  )}
                </div>
                <a
                  href={photo.fileUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs text-clinic-teal hover:underline truncate flex items-center gap-1"
                >
                  <ExternalLink className="w-3 h-3 flex-shrink-0" />
                  <span className="truncate">{photo.fileUrl}</span>
                </a>
                <p className="text-xs text-gray-400 mt-0.5">{photo.photoDate}</p>
                {photo.notes && <p className="text-xs text-gray-500 mt-0.5 line-clamp-1">{photo.notes}</p>}
              </div>
              <button
                onClick={() => handleDeletePhoto(photo.id)}
                className="flex-shrink-0 p-1.5 rounded-lg text-gray-400 hover:text-red-500 hover:bg-red-50 transition"
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
