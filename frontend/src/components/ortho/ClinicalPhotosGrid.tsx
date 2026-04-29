"use client";

import { useState, useRef, useCallback } from "react";
import { Upload, X, User, Smile, ScanLine } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { ClinicalPhoto } from "@/types/ortho";

const PHOTO_SLOTS = [
  { key: "frontal_face", label: "الوجه الأمامي", icon: User, category: "extraoral", photoType: "frontal_relaxed" },
  { key: "profile_face", label: "الوجه الجانبي", icon: User, category: "extraoral", photoType: "profile_right" },
  { key: "smiling_face", label: "الوجه الابتسامة", icon: Smile, category: "extraoral", photoType: "frontal_smile" },
  { key: "frontal_intraoral", label: "الفم الأمامي", icon: ScanLine, category: "intraoral", photoType: "anterior" },
  { key: "right_intraoral", label: "الفم الأيمن", icon: ScanLine, category: "intraoral", photoType: "right_lateral" },
  { key: "left_intraoral", label: "الفم الأيسر", icon: ScanLine, category: "intraoral", photoType: "left_lateral" },
  { key: "upper_occlusal", label: "الفك العلوي", icon: ScanLine, category: "intraoral", photoType: "upper_occlusal" },
  { key: "lower_occlusal", label: "الفك السفلي", icon: ScanLine, category: "intraoral", photoType: "lower_occlusal" },
  { key: "lateral_view", label: "الجانب الأيمن/الأيسر", icon: ScanLine, category: "intraoral", photoType: "right_lateral" },
];

interface Props {
  patientId: string;
  orthoCaseId: string;
  existingPhotos: ClinicalPhoto[];
  onPhotosChange: () => void;
}

export function ClinicalPhotosGrid({ patientId, orthoCaseId, existingPhotos, onPhotosChange }: Props) {
  const [uploadingSlot, setUploadingSlot] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [dragOverSlot, setDragOverSlot] = useState<string | null>(null);
  const fileInputRefs = useRef<Record<string, HTMLInputElement | null>>({});

  const getPhotoForSlot = useCallback(
    (slotKey: string) => {
      const slot = PHOTO_SLOTS.find((s) => s.key === slotKey);
      if (!slot) return null;
      return existingPhotos.find(
        (p) => p.photoType === slot.photoType && p.category === slot.category
      ) ?? null;
    },
    [existingPhotos]
  );

  const handleUpload = async (slotKey: string, file: File) => {
    const slot = PHOTO_SLOTS.find((s) => s.key === slotKey);
    if (!slot) return;

    setUploadingSlot(slotKey);
    try {
      // Step 1: Upload file to /api/files/upload
      const formData = new FormData();
      formData.append("file", file);
      formData.append("subDirectory", "clinical-photos");

      const { data: fileData } = await api.post<{ url: string }>(
        "/api/files/upload",
        formData,
        { headers: { "Content-Type": "multipart/form-data" } }
      );

      // Step 2: Save photo record via the clinical-photos endpoint
      const existing = getPhotoForSlot(slotKey);
      if (existing) {
        // Update existing photo
        await api.put(`/api/clinical-photos/${existing.id}`, {
          fileUrl: fileData.url,
          category: slot.category,
          photoType: slot.photoType,
          stage: "initial",
        });
      } else {
        // Create new photo record
        await api.post("/api/clinical-photos", {
          patientId,
          orthoCaseId,
          fileUrl: fileData.url,
          category: slot.category,
          photoType: slot.photoType,
          stage: "initial",
          photoDate: new Date().toISOString().split("T")[0],
        });
      }
      onPhotosChange();
    } catch {
      // Silent fail
    } finally {
      setUploadingSlot(null);
      const input = fileInputRefs.current[slotKey];
      if (input) input.value = "";
    }
  };

  const handleDelete = async (slotKey: string) => {
    const photo = getPhotoForSlot(slotKey);
    if (!photo) return;

    try {
      await api.delete(`/api/clinical-photos/${photo.id}`);
      onPhotosChange();
    } catch {
      // Silent fail
    }
  };

  const handleFileChange = (slotKey: string, e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleUpload(slotKey, file);
  };

  const handleDrop = (slotKey: string, e: React.DragEvent) => {
    e.preventDefault();
    setDragOverSlot(null);
    const file = e.dataTransfer.files?.[0];
    if (file && file.type.startsWith("image/")) {
      handleUpload(slotKey, file);
    }
  };

  const handleDragOver = (slotKey: string, e: React.DragEvent) => {
    e.preventDefault();
    setDragOverSlot(slotKey);
  };

  const handleDragLeave = () => {
    setDragOverSlot(null);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-700">الصور السريرية التسعة</h3>
        <span className="text-xs text-gray-400">
          {existingPhotos.length}/9 مكتمل
        </span>
      </div>

      <div className="grid grid-cols-3 gap-3">
        {PHOTO_SLOTS.map((slot) => {
          const photo = getPhotoForSlot(slot.key);
          const IconComp = slot.icon;
          const isUploading = uploadingSlot === slot.key;
          const isDragOver = dragOverSlot === slot.key;
          const isFilled = !!photo?.fileUrl;

          return (
            <div key={slot.key} className="relative">
              <input
                ref={(el) => { fileInputRefs.current[slot.key] = el; }}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={(e) => handleFileChange(slot.key, e)}
              />

              <div
                className={cn(
                  "relative aspect-square rounded-xl border-2 border-dashed overflow-hidden transition cursor-pointer group",
                  isFilled
                    ? "border-transparent"
                    : isDragOver
                    ? "border-clinic-teal bg-teal-50"
                    : "border-gray-200 bg-gray-50 hover:border-gray-300 hover:bg-gray-100"
                )}
                onClick={() => {
                  if (!isUploading) fileInputRefs.current[slot.key]?.click();
                }}
                onDrop={(e) => handleDrop(slot.key, e)}
                onDragOver={(e) => handleDragOver(slot.key, e)}
                onDragLeave={handleDragLeave}
              >
                {isFilled && photo?.fileUrl ? (
                  <>
                    <img
                      src={photo.fileUrl}
                      alt={slot.label}
                      className="w-full h-full object-cover"
                      onClick={(e) => {
                        e.stopPropagation();
                        setPreviewUrl(photo.fileUrl);
                      }}
                    />
                    <div className="absolute inset-0 bg-black/0 group-hover:bg-black/30 transition" />
                  </>
                ) : (
                  <div className="flex flex-col items-center justify-center h-full gap-2 p-2">
                    <div className={cn(
                      "w-10 h-10 rounded-full flex items-center justify-center transition",
                      isDragOver ? "bg-clinic-teal/20" : "bg-gray-200"
                    )}>
                      <IconComp className={cn(
                        "w-5 h-5",
                        isDragOver ? "text-clinic-teal" : "text-gray-400"
                      )} />
                    </div>
                    <span className={cn(
                      "text-xs text-center leading-tight font-medium",
                      isDragOver ? "text-clinic-teal" : "text-gray-500"
                    )}>
                      {slot.label}
                    </span>
                    {!isFilled && (
                      <Upload className="w-3 h-3 text-gray-300" />
                    )}
                  </div>
                )}

                {/* Uploading overlay */}
                {isUploading && (
                  <div className="absolute inset-0 bg-white/80 flex items-center justify-center z-10">
                    <div className="flex flex-col items-center gap-2">
                      <div className="w-6 h-6 border-2 border-clinic-teal border-t-transparent rounded-full animate-spin" />
                      <span className="text-xs text-gray-600">جاري الرفع...</span>
                    </div>
                  </div>
                )}
              </div>

              {/* Label below the slot */}
              <p className="text-xs text-center text-gray-600 mt-1 font-medium truncate">
                {slot.label}
              </p>

              {/* Delete button */}
              {isFilled && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete(slot.key);
                  }}
                  className="absolute top-1 left-1 w-6 h-6 rounded-full bg-red-500 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition z-20 shadow-sm"
                  title="حذف الصورة"
                >
                  <X className="w-3 h-3" />
                </button>
              )}
            </div>
          );
        })}
      </div>

      {/* Image Preview Modal */}
      {previewUrl && (
        <div
          className="fixed inset-0 bg-black/70 z-50 flex items-center justify-center p-4"
          onClick={() => setPreviewUrl(null)}
        >
          <div className="relative max-w-4xl max-h-[90vh] bg-white rounded-xl overflow-hidden shadow-2xl">
            <button
              onClick={() => setPreviewUrl(null)}
              className="absolute top-3 left-3 w-8 h-8 bg-white/90 rounded-full flex items-center justify-center text-gray-700 hover:bg-white transition z-10"
            >
              <X className="w-4 h-4" />
            </button>
            <img src={previewUrl} alt="Preview" className="max-h-[90vh] w-auto" />
          </div>
        </div>
      )}
    </div>
  );
}
