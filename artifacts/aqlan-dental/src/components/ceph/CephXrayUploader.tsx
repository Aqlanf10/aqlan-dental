
import { useCallback, useRef, useState } from "react";
import {
  UploadCloud,
  Link2,
  Loader2,
  CheckCircle2,
  X,
  ImageIcon,
  AlertTriangle,
} from "lucide-react";
import api from "@/lib/api";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { cn } from "@/lib/utils";

const MAX_SIZE = 10 * 1024 * 1024; // 10 MB
const ALLOWED_EXT = ["jpg", "jpeg", "png", "webp"];
const ALLOWED_MIME = [
  "image/jpeg",
  "image/png",
  "image/webp",
];

interface UploadResponse {
  url: string;
  fileName: string;
  originalName: string;
  size: number;
  contentType: string;
}

type Method = "device" | "url";
type Status = "idle" | "uploading" | "done" | "error";

interface Props {
  /** The current stored value (relative or absolute url) that will be submitted. */
  value: string;
  /** Called whenever the stored xrayFileUrl value changes (relative url or absolute link). */
  onChange: (url: string) => void;
  /** Called while an upload is in flight so the parent can disable submit. */
  onUploadingChange?: (uploading: boolean) => void;
}

function extractErr(error: unknown, fallback: string): string {
  const r = error as {
    response?: { data?: { message?: string; errors?: Record<string, string[]> } };
  };
  const message = r.response?.data?.message;
  if (message) return message;
  const errors = r.response?.data?.errors;
  if (errors) {
    const first = Object.values(errors).flat()[0];
    if (first) return first;
  }
  return fallback;
}

export default function CephXrayUploader({ value, onChange, onUploadingChange }: Props) {
  const [method, setMethod] = useState<Method>("device");
  const [status, setStatus] = useState<Status>("idle");
  const [error, setError] = useState("");
  const [dragActive, setDragActive] = useState(false);
  const [localPreview, setLocalPreview] = useState(""); // object URL or remote url
  const [previewBroken, setPreviewBroken] = useState(false);
  const [fileLabel, setFileLabel] = useState("");
  const [uploadedFileName, setUploadedFileName] = useState("");
  const [urlDraft, setUrlDraft] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const setUploading = useCallback(
    (u: boolean) => {
      setStatus(u ? "uploading" : "done");
      onUploadingChange?.(u);
    },
    [onUploadingChange]
  );

  const deleteTemporaryUpload = useCallback(async (fileName: string) => {
    if (!fileName) return;
    try {
      await api.delete(`/api/uploads/${encodeURIComponent(fileName)}`);
    } catch {
      // Best effort only. The upload may already be attached or removed.
    }
  }, []);

  const reset = useCallback(async () => {
    if (localPreview.startsWith("blob:")) URL.revokeObjectURL(localPreview);
    await deleteTemporaryUpload(uploadedFileName);
    setLocalPreview("");
    setPreviewBroken(false);
    setFileLabel("");
    setUploadedFileName("");
    setUrlDraft("");
    setStatus("idle");
    setError("");
    onChange("");
    onUploadingChange?.(false);
  }, [
    deleteTemporaryUpload,
    localPreview,
    onChange,
    onUploadingChange,
    uploadedFileName,
  ]);

  const validate = (file: File): string | null => {
    const ext = file.name.split(".").pop()?.toLowerCase() ?? "";
    if (file.size > MAX_SIZE) return "حجم الملف يتجاوز 10 ميجابايت";
    if (!ALLOWED_EXT.includes(ext)) {
      return "نوع الملف غير مدعوم للتحليل. استخدم JPG أو PNG أو WEBP";
    }
    if (file.type && !ALLOWED_MIME.includes(file.type)) {
      return "نوع الملف غير مدعوم للتحليل. استخدم JPG أو PNG أو WEBP";
    }
    return null;
  };

  const handleFile = useCallback(
    async (file: File) => {
      setError("");
      const v = validate(file);
      if (v) {
        setStatus("error");
        setError(v);
        return;
      }

      await deleteTemporaryUpload(uploadedFileName);

      // Immediate local preview
      if (localPreview.startsWith("blob:")) URL.revokeObjectURL(localPreview);
      const objectUrl = URL.createObjectURL(file);
      setLocalPreview(objectUrl);
      setPreviewBroken(false);
      setFileLabel(file.name);

      // Background upload
      setUploading(true);
      try {
        const formData = new FormData();
        formData.append("file", file);
        const { data } = await api.post<UploadResponse>("/api/uploads", formData, {
          headers: { "Content-Type": "multipart/form-data" },
        });
        onChange(data.url); // store the returned relative url
        setUploadedFileName(data.fileName);
        setStatus("done");
        onUploadingChange?.(false);
      } catch (err) {
        setStatus("error");
        setError(extractErr(err, "تعذر رفع الصورة، حاول مرة أخرى"));
        onChange("");
        onUploadingChange?.(false);
      }
    },
    [
      deleteTemporaryUpload,
      localPreview,
      onChange,
      onUploadingChange,
      setUploading,
      uploadedFileName,
    ]
  );

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragActive(false);
      const file = e.dataTransfer.files?.[0];
      if (file) handleFile(file);
    },
    [handleFile]
  );

  const previewUrl = localPreview || resolveImageUrl(value);

  const applyRemoteUrl = useCallback(async () => {
    const candidate = urlDraft.trim();
    let parsed: URL;
    try {
      parsed = new URL(candidate);
      if (!["http:", "https:"].includes(parsed.protocol)) throw new Error();
    } catch {
      setStatus("error");
      setError("أدخل رابط صورة صحيح يبدأ بـ http أو https");
      return;
    }

    setError("");
    setPreviewBroken(false);
    setStatus("uploading");
    onUploadingChange?.(true);

    try {
      await deleteTemporaryUpload(uploadedFileName);
      const { data } = await api.post<UploadResponse & { sourceUrl: string }>(
        "/api/uploads/import-image",
        { url: parsed.toString() }
      );
      setUploadedFileName(data.fileName);
      setFileLabel(data.originalName || parsed.hostname);
      setLocalPreview(resolveImageUrl(data.url));
      onChange(data.url);
      setStatus("done");
    } catch (err) {
      onChange("");
      setStatus("error");
      setError(extractErr(err, "تعذر استيراد الصورة من الرابط"));
    } finally {
      onUploadingChange?.(false);
    }
  }, [
    deleteTemporaryUpload,
    onChange,
    onUploadingChange,
    uploadedFileName,
    urlDraft,
  ]);

  return (
    <div className="space-y-4">
      {/* Method tabs */}
      <div className="inline-flex rounded-lg bg-gray-100 p-1 text-sm">
        <button
          type="button"
          onClick={() => {
            if (method !== "device") void reset();
            setMethod("device");
          }}
          className={cn(
            "flex items-center gap-1.5 px-3 py-1.5 rounded-md font-medium transition",
            method === "device"
              ? "bg-white text-clinic-navy shadow-sm"
              : "text-gray-500 hover:text-gray-700"
          )}
        >
          <UploadCloud className="w-4 h-4" />
          رفع من الجهاز
        </button>
        <button
          type="button"
          onClick={() => {
            if (method !== "url") void reset();
            setMethod("url");
          }}
          className={cn(
            "flex items-center gap-1.5 px-3 py-1.5 rounded-md font-medium transition",
            method === "url"
              ? "bg-white text-clinic-navy shadow-sm"
              : "text-gray-500 hover:text-gray-700"
          )}
        >
          <Link2 className="w-4 h-4" />
          رابط مباشر / PACS
        </button>
      </div>

      {/* Preview area */}
      {previewUrl && !previewBroken ? (
        <div className="relative rounded-xl overflow-hidden border border-gray-200 bg-gray-900/5 group">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={previewUrl}
            alt="معاينة صورة الأشعة"
            onError={() => setPreviewBroken(true)}
            className="w-full max-h-[460px] object-contain bg-[repeating-conic-gradient(#f3f4f6_0%_25%,#ffffff_0%_50%)] bg-[length:20px_20px]"
          />
          {/* Status badge */}
          <div className="absolute top-3 start-3 flex items-center gap-1.5">
            {status === "uploading" && (
              <span className="flex items-center gap-1.5 rounded-full bg-white/90 px-2.5 py-1 text-xs font-medium text-clinic-blue shadow">
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                جارٍ الرفع...
              </span>
            )}
            {status === "done" && value && (
              <span className="flex items-center gap-1.5 rounded-full bg-white/90 px-2.5 py-1 text-xs font-medium text-emerald-600 shadow">
                <CheckCircle2 className="w-3.5 h-3.5" />
                تم الرفع
              </span>
            )}
          </div>
          {/* Remove */}
          <button
            type="button"
            onClick={() => void reset()}
            className="absolute top-3 end-3 flex items-center gap-1 rounded-full bg-white/90 px-2.5 py-1 text-xs font-medium text-gray-600 shadow hover:bg-white hover:text-red-600 transition"
          >
            <X className="w-3.5 h-3.5" />
            إزالة
          </button>
          {fileLabel && (
            <div className="absolute bottom-0 inset-x-0 bg-gradient-to-t from-black/60 to-transparent px-3 py-2 text-xs text-white truncate">
              {fileLabel}
            </div>
          )}
        </div>
      ) : previewBroken ? (
        <div className="rounded-xl border border-red-200 bg-red-50 p-6 text-center space-y-3">
          <AlertTriangle className="w-8 h-8 text-red-400 mx-auto" />
          <p className="text-sm text-red-700">
            {method === "url"
              ? "تعذر تحميل الصورة من الرابط"
              : "تعذر عرض معاينة الصورة في المتصفح"}
          </p>
          <button
            type="button"
            onClick={() => void reset()}
            className="text-xs font-medium text-clinic-blue hover:underline"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : method === "device" ? (
        // Dropzone
        <div
          role="button"
          tabIndex={0}
          onClick={() => inputRef.current?.click()}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") inputRef.current?.click();
          }}
          onDragOver={(e) => {
            e.preventDefault();
            setDragActive(true);
          }}
          onDragLeave={() => setDragActive(false)}
          onDrop={onDrop}
          className={cn(
            "flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-14 text-center cursor-pointer transition",
            dragActive
              ? "border-clinic-blue bg-clinic-blue-50"
              : "border-gray-300 bg-gray-50 hover:border-clinic-blue hover:bg-clinic-blue-50/50"
          )}
        >
          <div
            className={cn(
              "flex h-14 w-14 items-center justify-center rounded-full transition",
              dragActive ? "bg-clinic-blue text-white" : "bg-white text-clinic-blue shadow-sm"
            )}
          >
            <UploadCloud className="w-7 h-7" />
          </div>
          <div>
            <p className="text-sm font-semibold text-gray-700">
              اسحب صورة الأشعة هنا أو اضغط للاختيار من الجهاز
            </p>
            <p className="text-xs text-gray-400 mt-1">الحد الأقصى لحجم الملف 10 ميجابايت</p>
          </div>
          <input
            ref={inputRef}
            type="file"
            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) handleFile(file);
              e.target.value = "";
            }}
          />
        </div>
      ) : (
        // URL input method
        <div className="rounded-xl border border-gray-200 bg-gray-50 p-5 space-y-3">
          <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
            <Link2 className="w-4 h-4 text-clinic-blue" />
            رابط مباشر للصورة أو خادم PACS
          </label>
          <div className="flex gap-2">
            <input
              type="url"
              dir="ltr"
              value={urlDraft}
              onChange={(e) => setUrlDraft(e.target.value)}
              placeholder="https://..."
              className="flex-1 px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
            />
            <button
              type="button"
              onClick={() => void applyRemoteUrl()}
              disabled={status === "uploading" || !urlDraft.trim()}
              className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-navy text-white hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition whitespace-nowrap"
            >
              <ImageIcon className="w-4 h-4" />
              استيراد
            </button>
          </div>
        </div>
      )}

      {/* Inline error (validation / upload) */}
      {error && (
        <div className="flex items-start gap-2 rounded-lg bg-red-50 border border-red-200 px-3 py-2 text-sm text-red-700">
          <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Formats + notes */}
      <div className="space-y-1.5 text-xs">
        <p className="text-gray-500">الصيغ المدعومة: JPG, JPEG, PNG, WEBP</p>
        <p className="text-gray-400 leading-relaxed">
          صيغ DICOM وTIFF وPDF غير مناسبة للرسم التفاعلي حاليًا — حوّلها إلى JPG أو PNG قبل الرفع.
        </p>
        <p className="text-gray-400">
          عند استخدام رابط، تُنسخ الصورة بأمان إلى ملفات المركز حتى تبقى قابلة للرسم والقياس.
        </p>
      </div>
    </div>
  );
}
