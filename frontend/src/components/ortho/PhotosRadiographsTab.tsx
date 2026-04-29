"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { X, Camera, ScanLine } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { ClinicalPhotosGrid } from "./ClinicalPhotosGrid";
import type { ClinicalPhoto } from "@/types/ortho";

interface Radiograph {
  id: string;
  xrayDate: string;
  xrayType: string;
  fileUrl: string;
  toothRelated?: string;
  notes?: string;
}

const XRAY_TYPES = [
  { value: "OPG", label: "OPG — بانوراما" },
  { value: "lateral_ceph", label: "سيفالومتري جانبي" },
  { value: "PA_ceph", label: "سيفالومتري خلفي أمامي" },
  { value: "periapical", label: "حوطي" },
  { value: "bitewing", label: "عضاض" },
  { value: "CBCT", label: "CBCT" },
];

interface Props {
  patientId: string;
  orthoCaseId: string;
}

export function PhotosRadiographsTab({ patientId, orthoCaseId }: Props) {
  const [photos, setPhotos] = useState<ClinicalPhoto[]>([]);
  const [radiographs, setRadiographs] = useState<Radiograph[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeSubTab, setActiveSubTab] = useState<"photos" | "xray">("photos");
  const [uploading, setUploading] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  const xrayFileRef = useRef<HTMLInputElement>(null);

  const [xrayForm, setXrayForm] = useState({
    xrayType: "OPG",
    toothRelated: "",
    notes: "",
  });

  const fetchData = useCallback(() => {
    Promise.all([
      api.get<ClinicalPhoto[]>(`/api/clinical-photos/${patientId}`).catch(() => ({ data: [] })),
      api.get<Radiograph[]>(`/api/radiographs/${patientId}`).catch(() => ({ data: [] })),
    ]).then(([photosRes, xrayRes]) => {
      setPhotos(photosRes.data ?? []);
      setRadiographs(xrayRes.data ?? []);
    }).finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleXrayUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    try {
      // Step 1: Upload file
      const formData = new FormData();
      formData.append("file", file);
      formData.append("subDirectory", "radiographs");

      const { data: fileData } = await api.post<{ url: string }>(
        "/api/files/upload",
        formData,
        { headers: { "Content-Type": "multipart/form-data" } }
      );

      // Step 2: Create radiograph record
      const radiographFormData = new FormData();
      radiographFormData.append("patientId", patientId);
      radiographFormData.append("fileUrl", fileData.url);
      if (xrayForm.xrayType) radiographFormData.append("xrayType", xrayForm.xrayType);
      if (xrayForm.toothRelated) radiographFormData.append("toothRelated", xrayForm.toothRelated);
      if (xrayForm.notes) radiographFormData.append("notes", xrayForm.notes);

      const { data: newXray } = await api.post<Radiograph>("/api/radiographs/upload", radiographFormData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setRadiographs((prev) => [newXray, ...prev]);
      setXrayForm((f) => ({ ...f, toothRelated: "", notes: "" }));
    } catch {
    } finally {
      setUploading(false);
      if (xrayFileRef.current) xrayFileRef.current.value = "";
    }
  };

  const handleDeleteXray = async (id: string) => {
    try {
      await api.delete(`/api/radiographs/${id}`);
      setRadiographs((prev) => prev.filter((r) => r.id !== id));
    } catch {}
  };

  if (loading) return <div className="animate-pulse h-64 bg-gray-100 rounded-xl" />;

  return (
    <div className="space-y-5">
      {/* Sub-tabs */}
      <div className="flex items-center gap-2 border-b border-gray-200 pb-3">
        <button
          onClick={() => setActiveSubTab("photos")}
          className={cn(
            "flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition",
            activeSubTab === "photos"
              ? "bg-clinic-teal text-white"
              : "text-gray-600 hover:bg-gray-100"
          )}
        >
          <Camera className="w-4 h-4" />
          الصور السريرية ({photos.length})
        </button>
        <button
          onClick={() => setActiveSubTab("xray")}
          className={cn(
            "flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition",
            activeSubTab === "xray"
              ? "bg-clinic-teal text-white"
              : "text-gray-600 hover:bg-gray-100"
          )}
        >
          <ScanLine className="w-4 h-4" />
          الأشعة ({radiographs.length})
        </button>
      </div>

      {/* Photos Tab — ClinicalPhotosGrid */}
      {activeSubTab === "photos" && (
        <ClinicalPhotosGrid
          patientId={patientId}
          orthoCaseId={orthoCaseId}
          existingPhotos={photos}
          onPhotosChange={fetchData}
        />
      )}

      {/* X-ray Tab */}
      {activeSubTab === "xray" && (
        <div className="space-y-4">
          {/* Upload form */}
          <div className="bg-gray-50 rounded-xl border border-gray-200 p-4 space-y-3">
            <h4 className="text-sm font-semibold text-gray-700">رفع أشعة</h4>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <select
                className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
                value={xrayForm.xrayType}
                onChange={(e) => setXrayForm((f) => ({ ...f, xrayType: e.target.value }))}
              >
                {XRAY_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
              <input
                className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
                placeholder="السن المرتبط (مثل: 36)"
                value={xrayForm.toothRelated}
                onChange={(e) => setXrayForm((f) => ({ ...f, toothRelated: e.target.value }))}
              />
              <input
                ref={xrayFileRef}
                type="file"
                accept="image/*,.dicom"
                onChange={handleXrayUpload}
                disabled={uploading}
                className="text-sm"
              />
            </div>
            {uploading && <span className="text-xs text-gray-500">جاري الرفع...</span>}
          </div>

          {/* X-ray List */}
          {radiographs.length === 0 ? (
            <div className="text-center py-16 text-gray-400">
              <ScanLine className="w-12 h-12 mx-auto mb-3 opacity-30" />
              <p className="text-sm">لا توجد أشعة مسجلة</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {radiographs.map((xray) => (
                <div
                  key={xray.id}
                  className="bg-white rounded-xl border border-gray-200 overflow-hidden cursor-pointer hover:shadow-md transition"
                  onClick={() => setPreviewUrl(xray.fileUrl)}
                >
                  <div className="flex items-start gap-3 p-4">
                    <div className="w-16 h-16 bg-gray-100 rounded-lg flex items-center justify-center flex-shrink-0 overflow-hidden">
                      {xray.fileUrl ? (
                        <img src={xray.fileUrl} alt={xray.xrayType} className="w-full h-full object-cover" />
                      ) : (
                        <ScanLine className="w-6 h-6 text-gray-300" />
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-gray-900">
                          {XRAY_TYPES.find((t) => t.value === xray.xrayType)?.label ?? xray.xrayType}
                        </span>
                        {xray.toothRelated && (
                          <span className="text-xs bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded">
                            سن {xray.toothRelated}
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-gray-400 mt-0.5">{xray.xrayDate}</p>
                      {xray.notes && <p className="text-xs text-gray-500 mt-1">{xray.notes}</p>}
                    </div>
                    <button
                      onClick={(e) => { e.stopPropagation(); handleDeleteXray(xray.id); }}
                      className="text-gray-300 hover:text-red-500 transition flex-shrink-0"
                    >
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

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
