"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent } from "react";
import {
  Crop,
  Star,
  Trash2,
} from "lucide-react";
import { cn, formatArabicDate } from "@/lib/utils";
import {
  useOrthoPhotos,
  useRecordsChecklist,
  useUpdateOrthoPhoto,
} from "@/hooks/useOrtho";
import { toast } from "@/stores/toastStore";
import type { OrthoPhoto } from "@/types/ortho";
import {
  ORTHO_PHOTO_CATEGORY_LABELS,
  ORTHO_PHOTO_SUBTYPES,
  TREATMENT_PHASE_LABELS,
  orthoSubtypeLabel,
} from "@/types/ortho";
import { ImagePreviewModal } from "@/components/shared/ImagePreviewModal";
import { OrthoImagePreparationDialog } from "@/components/ortho/OrthoImagePreparationDialog";
import api from "@/lib/api";
import { Field, EmptyState, SaveButton } from "./_shared";
import {
  inputCls,
  EMPTY_PHOTO_FORM,
  PHASE_BADGE_CLS,
  PHOTO_TYPE_LABELS,
} from "../_lib/types";

/**
 * Photos tab — the upload form + gallery + image-preparation dialog portion of
 * the original `RecordsPanel`. Split out (FE-20) from the records checklist;
 * the two are rendered together by the page shell for the `records` tab.
 *
 * Note: this tab also calls `useRecordsChecklist` purely to refetch the
 * checklist after image preparation (the dialog's `onSaved` may toggle
 * checklist items). React Query dedupes the request, so this is identical to
 * the original behavior.
 */
export function OrthoPhotosTab({ caseId }: { caseId: string }) {
  const { data: photos = [] as OrthoPhoto[], refetch: refetchPhotos } =
    useOrthoPhotos(caseId);
  const { refetch: refetchChecklist } = useRecordsChecklist(caseId);
  const updatePhoto = useUpdateOrthoPhoto(caseId);
  const [form, setForm] = useState({ ...EMPTY_PHOTO_FORM });
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const [preparingPhoto, setPreparingPhoto] = useState<OrthoPhoto | null>(null);
  const [phaseFilter, setPhaseFilter] = useState<string>("all");
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Gallery filters (client-side — instant, no extra requests)
  const filteredPhotos = useMemo(() => {
    return photos.filter((p) => {
      if (phaseFilter !== "all" && p.treatmentPhase !== phaseFilter) return false;
      if (categoryFilter !== "all" && p.category !== categoryFilter) return false;
      return true;
    });
  }, [photos, phaseFilter, categoryFilter]);

  // إعادة ضبط مؤشر المعاينة عند تغيير التصفية حتى لا يخرج عن النطاق
  useEffect(() => {
    setPreviewIndex(0);
  }, [phaseFilter, categoryFilter]);

  /** الحقول الاختيارية الجديدة — تُرسل فقط عند تعبئتها (الرفع السريع القديم يبقى كما هو) */
  const tagPayload = () => ({
    category: form.category || undefined,
    subtype: form.subtype || undefined,
    treatmentPhase: form.treatmentPhase || undefined,
    isSelectedForReport: form.isSelectedForReport || undefined,
  });

  const toggleReportSelection = (p: OrthoPhoto) => {
    updatePhoto.mutate({
      photoId: p.id,
      data: { isSelectedForReport: !p.isSelectedForReport },
    });
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    // Validate
    const validExts = [".jpg", ".jpeg", ".png", ".webp"];
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!validExts.includes(ext)) {
      toast.error("صيغة الملف غير مدعومة. استخدم JPG أو PNG أو WebP");
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      toast.error("حجم الملف يتجاوز 10 ميجابايت");
      return;
    }
    setPhotoFile(file);
    // Generate preview
    const reader = new FileReader();
    reader.onload = (ev) => setPhotoPreview(ev.target?.result as string);
    reader.readAsDataURL(file);
  };

  const addPhotoFromUrl = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.photoUrl.trim()) return;
    setSaving(true);
    try {
      await (
        await import("@/services/orthoService")
      ).orthoService.addPhoto(caseId, {
        photoUrl: form.photoUrl,
        photoType: form.photoType,
        caption: form.caption,
        ...tagPayload(),
      });
      setForm({ ...EMPTY_PHOTO_FORM });
      await refetchPhotos();
      toast.success("تمت إضافة السجل");
    } catch {
      toast.error("فشل إضافة السجل");
    } finally {
      setSaving(false);
    }
  };

  const uploadAndAddPhoto = async (event: FormEvent) => {
    event.preventDefault();
    if (!photoFile) return;
    setUploading(true);
    try {
      // Step 1: Upload file via authenticated api client (sends to Railway backend with Bearer token)
      const formData = new FormData();
      formData.append("file", photoFile);
      const uploadRes = await api.post<{ url: string }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      const url = uploadRes.data.url;
      // Step 2: Add photo to ortho case
      await (
        await import("@/services/orthoService")
      ).orthoService.addPhoto(caseId, {
        photoUrl: url,
        photoType: form.photoType,
        caption: form.caption,
        ...tagPayload(),
      });
      setPhotoFile(null);
      setPhotoPreview(null);
      setForm((f) => ({ ...EMPTY_PHOTO_FORM, photoUrl: f.photoUrl }));
      if (fileInputRef.current) fileInputRef.current.value = "";
      await refetchPhotos();
      toast.success("تم رفع الصورة وإضافتها بنجاح");
    } catch {
      toast.error("فشل رفع الصورة");
    } finally {
      setUploading(false);
    }
  };

  const deletePhoto = async (photoId: string) => {
    try {
      await (
        await import("@/services/orthoService")
      ).orthoService.deletePhoto(caseId, photoId);
      setDeleteConfirm(null);
      await refetchPhotos();
      toast.success("تم حذف الصورة");
    } catch {
      toast.error("فشل حذف الصورة");
    }
  };

  const resolveImageUrl = (url: string) => {
    if (url.startsWith("http") || url.startsWith("data:")) return url;
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
    return apiBase ? `${apiBase}${url.startsWith("/") ? "" : "/"}${url}` : url;
  };

  return (
    <>
      {/* Photo upload + gallery */}
      <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
        <div className="space-y-4">
          {/* File Upload Form */}
          <form
            onSubmit={uploadAndAddPhoto}
            className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
          >
            <h2 className="font-semibold text-gray-900">رفع صورة</h2>
            <div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".jpg,.jpeg,.png,.webp"
                onChange={handleFileSelect}
                className="block w-full text-sm text-gray-500 file:ml-2 file:rounded-lg file:border-0 file:bg-[#3d7ab5] file:px-3 file:py-2 file:text-sm file:font-medium file:text-white hover:file:bg-[#1a3a5c] file:cursor-pointer"
              />
            </div>
            {photoPreview && (
              <div className="relative aspect-square w-full max-w-[200px] overflow-hidden rounded-lg border border-gray-200">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={photoPreview}
                  alt="معاينة"
                  className="h-full w-full object-cover"
                />
              </div>
            )}
            <Field label="النوع">
              <select
                className={inputCls}
                value={form.photoType}
                onChange={(e) =>
                  setForm((f) => ({ ...f, photoType: e.target.value }))
                }
              >
                <option value="Intraoral">داخل الفم</option>
                <option value="Extraoral">خارج الفم</option>
                <option value="Progress">متابعة</option>
                <option value="Radiograph">أشعة</option>
              </select>
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="فئة الصورة">
                <select
                  className={inputCls}
                  value={form.category}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      category: e.target.value,
                      subtype: "", // الأنواع الفرعية تتبع الفئة
                    }))
                  }
                >
                  <option value="">— غير محدد —</option>
                  {Object.entries(ORTHO_PHOTO_CATEGORY_LABELS).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="نوع فرعي">
                <select
                  className={inputCls}
                  value={form.subtype}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, subtype: e.target.value }))
                  }
                  disabled={
                    !form.category ||
                    (ORTHO_PHOTO_SUBTYPES[form.category] ?? []).length === 0
                  }
                >
                  <option value="">— غير محدد —</option>
                  {(ORTHO_PHOTO_SUBTYPES[form.category] ?? []).map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </Field>
            </div>
            <Field label="مرحلة العلاج">
              <select
                className={inputCls}
                value={form.treatmentPhase}
                onChange={(e) =>
                  setForm((f) => ({ ...f, treatmentPhase: e.target.value }))
                }
              >
                <option value="">— غير محدد —</option>
                {Object.entries(TREATMENT_PHASE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </Field>
            <label className="flex cursor-pointer items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={form.isSelectedForReport}
                onChange={(e) =>
                  setForm((f) => ({ ...f, isSelectedForReport: e.target.checked }))
                }
                className="h-4 w-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
              />
              إدراج في التقرير
            </label>
            <Field label="ملاحظة">
              <input
                className={inputCls}
                value={form.caption}
                onChange={(e) =>
                  setForm((f) => ({ ...f, caption: e.target.value }))
                }
                placeholder="وصف الصورة"
              />
            </Field>
            <SaveButton saving={uploading}>رفع وإضافة</SaveButton>
          </form>

          {/* URL paste fallback */}
          <details className="rounded-lg border border-gray-200 bg-white">
            <summary className="cursor-pointer px-5 py-3 text-sm font-medium text-gray-600 hover:text-gray-900">
              إضافة عبر رابط (متقدم)
            </summary>
            <form
              onSubmit={addPhotoFromUrl}
              className="space-y-3 border-t border-gray-100 p-5"
            >
              <Field label="رابط الصورة">
                <input
                  className={inputCls}
                  value={form.photoUrl}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, photoUrl: e.target.value }))
                  }
                  placeholder="https://..."
                  dir="ltr"
                />
              </Field>
              <SaveButton saving={saving}>إضافة</SaveButton>
            </form>
          </details>
        </div>

        {/* Photo Gallery */}
        <div className="space-y-3">
          {/* Filter pills */}
          {photos.length > 0 && (
            <div className="flex flex-wrap items-center gap-2">
              {[
                { value: "all", label: "الكل" },
                ...Object.entries(TREATMENT_PHASE_LABELS).map(([value, label]) => ({
                  value,
                  label,
                })),
              ].map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setPhaseFilter(opt.value)}
                  className={cn(
                    "rounded-full border px-3 py-1 text-xs font-medium transition",
                    phaseFilter === opt.value
                      ? "border-clinic-blue bg-clinic-blue text-white"
                      : "border-gray-200 bg-white text-gray-600 hover:border-clinic-blue/40"
                  )}
                >
                  {opt.label}
                </button>
              ))}
              <span className="mx-1 h-4 w-px bg-gray-200" />
              {[
                { value: "all", label: "كل الفئات" },
                ...Object.entries(ORTHO_PHOTO_CATEGORY_LABELS).map(
                  ([value, label]) => ({ value, label })
                ),
              ].map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setCategoryFilter(opt.value)}
                  className={cn(
                    "rounded-full border px-3 py-1 text-xs font-medium transition",
                    categoryFilter === opt.value
                      ? "border-clinic-blue bg-clinic-blue text-white"
                      : "border-gray-200 bg-white text-gray-600 hover:border-clinic-blue/40"
                  )}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          )}
          {photos.length === 0 ? (
            <EmptyState text="لا توجد صور أو سجلات مرتبطة بحالة التقويم." />
          ) : filteredPhotos.length === 0 ? (
            <EmptyState text="لا توجد صور مطابقة للتصفية المحددة." />
          ) : (
            <div className="grid gap-3 grid-cols-2 md:grid-cols-3">
              {filteredPhotos.map((p: OrthoPhoto, idx: number) => (
                <div
                  key={p.id}
                  className="group relative aspect-square overflow-hidden rounded-lg border border-gray-200 bg-gray-50 cursor-pointer"
                  onClick={() => {
                    setPreviewIndex(idx);
                    setPreviewOpen(true);
                  }}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={resolveImageUrl(p.photoUrl)}
                    alt={p.caption || PHOTO_TYPE_LABELS[p.photoType] || p.photoType}
                    className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                    onError={(e) => {
                      (e.target as HTMLImageElement).style.display = "none";
                    }}
                  />
                  {/* Overlay on hover */}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity">
                    <div className="absolute bottom-0 right-0 left-0 p-2">
                      <p className="text-xs font-medium text-white truncate">
                        {p.caption || PHOTO_TYPE_LABELS[p.photoType] || p.photoType}
                      </p>
                      {p.takenAt && (
                        <p className="text-[10px] text-white/70">
                          {formatArabicDate(p.takenAt)}
                        </p>
                      )}
                    </div>
                  </div>
                  {/* Phase + subtype/type badges */}
                  <div className="absolute top-2 right-2 flex max-w-[75%] flex-wrap justify-end gap-1">
                    {p.isPreparedForReport && (
                      <span className="rounded bg-emerald-600/90 px-1.5 py-0.5 text-[10px] font-bold text-white">
                        مجهزة
                      </span>
                    )}
                    {p.treatmentPhase && TREATMENT_PHASE_LABELS[p.treatmentPhase] && (
                      <span
                        className={cn(
                          "rounded px-1.5 py-0.5 text-[10px] font-bold text-white",
                          PHASE_BADGE_CLS[p.treatmentPhase] ?? "bg-black/50"
                        )}
                      >
                        {TREATMENT_PHASE_LABELS[p.treatmentPhase]}
                      </span>
                    )}
                    <span className="rounded bg-black/50 px-1.5 py-0.5 text-[10px] font-medium text-white">
                      {orthoSubtypeLabel(p.subtype) ||
                        (p.category && ORTHO_PHOTO_CATEGORY_LABELS[p.category]) ||
                        PHOTO_TYPE_LABELS[p.photoType] ||
                        p.photoType}
                    </span>
                  </div>
                  {/* Report selection toggle */}
                  <button
                    type="button"
                    title={
                      p.isSelectedForReport
                        ? "إزالة من التقرير"
                        : "إدراج في التقرير"
                    }
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleReportSelection(p);
                    }}
                    className={cn(
                      "absolute bottom-2 left-2 z-10 rounded-full p-1.5 transition",
                      p.isSelectedForReport
                        ? "bg-amber-400 text-white shadow"
                        : "bg-black/40 text-white opacity-0 group-hover:opacity-100 hover:bg-amber-400"
                    )}
                  >
                    <Star
                      className="h-3.5 w-3.5"
                      fill={p.isSelectedForReport ? "currentColor" : "none"}
                    />
                  </button>
                  <button
                    type="button"
                    title="تجهيز الصورة"
                    onClick={(e) => {
                      e.stopPropagation();
                      setPreparingPhoto(p);
                    }}
                    className="absolute bottom-2 right-2 z-10 rounded-full bg-black/45 p-1.5 text-white opacity-0 transition hover:bg-clinic-blue group-hover:opacity-100"
                  >
                    <Crop className="h-3.5 w-3.5" />
                  </button>
                  {/* Delete button */}
                  {deleteConfirm === p.id ? (
                    <div
                      className="absolute top-2 left-2 flex items-center gap-1"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <button
                        type="button"
                        onClick={() => deletePhoto(p.id)}
                        className="rounded bg-red-600 px-2 py-1 text-[10px] font-bold text-white"
                      >
                        تأكيد
                      </button>
                      <button
                        type="button"
                        onClick={() => setDeleteConfirm(null)}
                        className="rounded bg-gray-600 px-2 py-1 text-[10px] font-bold text-white"
                      >
                        إلغاء
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        setDeleteConfirm(p.id);
                      }}
                      className="absolute top-2 left-2 rounded bg-black/40 p-1 text-white opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-600"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Image Preview Modal — navigates within the filtered gallery */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={filteredPhotos[previewIndex]?.photoUrl ?? ""}
        fileName={
          filteredPhotos[previewIndex]?.caption ||
          filteredPhotos[previewIndex]?.photoType
        }
        items={filteredPhotos.map((p) => ({
          url: resolveImageUrl(p.photoUrl),
          fileName: p.caption || p.photoType,
        }))}
        currentIndex={previewIndex}
        onNavigate={setPreviewIndex}
      />
      <OrthoImagePreparationDialog
        caseId={caseId}
        photo={preparingPhoto}
        open={preparingPhoto !== null}
        onClose={() => setPreparingPhoto(null)}
        onSaved={() => {
          refetchPhotos();
          refetchChecklist();
        }}
      />
    </>
  );
}
