"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import Link from "next/link";
import { FolderOpen, Plus, Pencil, Trash2, X, FileText, Upload, Eye, Filter, Download, CheckCircle, Printer, Pill, FileSignature, ArrowRightLeft, FlaskConical } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { ImagePreviewModal } from "@/components/shared/ImagePreviewModal";
import { resolveImageUrl } from "@/hooks/useClinicBranding";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface DocumentDto {
  id: string;
  patientId: string;
  documentType?: string;
  title?: string;
  fileUrl?: string;
  fileName?: string;
  fileSize?: number;
  mimeType?: string;
  notes?: string;
  uploadedBy?: string;
  signed: boolean;
  signedAt?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface DocForm {
  title: string;
  documentType: string;
  fileUrl: string;
  fileName: string;
  fileSize: number;
  mimeType: string;
  notes: string;
}

const EMPTY_FORM: DocForm = {
  title: "",
  documentType: "",
  fileUrl: "",
  fileName: "",
  fileSize: 0,
  mimeType: "",
  notes: "",
};

const DOCUMENT_TYPES: Record<string, string> = {
  Consent: "موافقة",
  Report: "تقرير",
  Prescription: "وصفة",
  Contract: "عقد",
  Referral: "إحالة",
  Lab: "مختبر",
  Other: "أخرى",
};

const TYPE_COLORS: Record<string, string> = {
  Consent: "bg-green-100 text-green-700",
  Report: "bg-blue-100 text-blue-700",
  Prescription: "bg-red-100 text-red-700",
  Contract: "bg-purple-100 text-purple-700",
  Referral: "bg-yellow-100 text-yellow-700",
  Lab: "bg-cyan-100 text-cyan-700",
  Other: "bg-gray-100 text-gray-700",
};

const DOC_TYPE_ICONS: Record<string, LucideIcon> = {
  Consent: FileSignature,
  Report: FileText,
  Prescription: Pill,
  Contract: FileSignature,
  Referral: ArrowRightLeft,
  Lab: FlaskConical,
  Other: FolderOpen,
};

function formatFileSize(bytes?: number | null): string {
  if (!bytes) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// Allowed file types for documents
const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB

// ─── Component ──────────────────────────────────────────────────────────────────

interface DocumentsTabProps {
  patientId: string;
}

export function DocumentsTab({ patientId }: DocumentsTabProps) {
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [editingDoc, setEditingDoc] = useState<DocumentDto | null>(null);
  const [form, setForm] = useState<DocForm>({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [filterType, setFilterType] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Preview state
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewUrl, setPreviewUrl] = useState("");
  const [previewFileName, setPreviewFileName] = useState<string | undefined>();
  const [previewMimeType, setPreviewMimeType] = useState<string | undefined>();

  const fetchDocuments = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams({ patientId });
    if (filterType) params.set("documentType", filterType);
    api.get<{ data: DocumentDto[] }>(`/api/documents?${params}`)
      .then((r) => setDocuments(r.data.data ?? []))
      .catch(() => setError("فشل تحميل المستندات"))
      .finally(() => setLoading(false));
  }, [patientId, filterType]);

  useEffect(() => {
    fetchDocuments();
  }, [fetchDocuments]);

  const openAddModal = () => {
    setEditingDoc(null);
    setForm({ ...EMPTY_FORM });
    setShowModal(true);
  };

  const openEditModal = (doc: DocumentDto) => {
    setEditingDoc(doc);
    setForm({
      title: doc.title ?? "",
      documentType: doc.documentType ?? "",
      fileUrl: doc.fileUrl ?? "",
      fileName: doc.fileName ?? "",
      fileSize: doc.fileSize ?? 0,
      mimeType: doc.mimeType ?? "",
      notes: doc.notes ?? "",
    });
    setShowModal(true);
  };

  const openPreview = (doc: DocumentDto) => {
    if (!doc.fileUrl) return;
    setPreviewUrl(resolveImageUrl(doc.fileUrl));
    setPreviewFileName(doc.fileName);
    setPreviewMimeType(doc.mimeType);
    setPreviewOpen(true);
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate file type
    const ext = "." + file.name.split(".").pop()?.toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      toast.error("نوع الملف غير مدعوم. المسموح: JPG, PNG, WebP, PDF");
      return;
    }

    if (file.size > MAX_FILE_SIZE) {
      toast.error("حجم الملف يتجاوز 10 ميجابايت");
      return;
    }

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const { data } = await api.post<{ url: string; fileName: string; originalName: string; size: number; contentType: string }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setForm(prev => ({
        ...prev,
        fileUrl: data.url,
        fileName: data.originalName,
        fileSize: data.size,
        mimeType: data.contentType,
      }));
      toast.success("تم رفع الملف بنجاح");
    } catch {
      toast.error("فشل رفع الملف");
    } finally {
      setUploading(false);
    }
  };

  const handleSave = async () => {
    if (!form.title.trim()) {
      toast.error("عنوان المستند مطلوب");
      return;
    }
    if (!editingDoc && !form.fileUrl) {
      toast.error("يجب رفع ملف أولاً");
      return;
    }
    setSaving(true);
    try {
      if (editingDoc) {
        await api.put(`/api/documents/${editingDoc.id}`, {
          title: form.title,
          documentType: form.documentType || null,
          notes: form.notes || null,
        });
        toast.success("تم تحديث المستند بنجاح");
      } else {
        await api.post("/api/documents", {
          patientId,
          title: form.title,
          documentType: form.documentType || null,
          fileUrl: form.fileUrl || null,
          fileName: form.fileName || null,
          fileSize: form.fileSize || null,
          mimeType: form.mimeType || null,
          notes: form.notes || null,
        });
        toast.success("تم إضافة المستند بنجاح");
      }
      setShowModal(false);
      fetchDocuments();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ المستند");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await api.delete(`/api/documents/${id}`);
      toast.success("تم حذف المستند بنجاح");
      setDeleteConfirm(null);
      fetchDocuments();
    } catch {
      toast.error("فشل حذف المستند");
    }
  };

  const isImageFile = (mimeType?: string | null) => mimeType?.startsWith("image/") ?? false;

  // ─── Stats ──────────────────────────────────────────────────────────────────

  const activeDocs = documents.filter(d => d.isActive);
  const signedCount = activeDocs.filter(d => d.signed).length;

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4" dir="rtl">
      {/* Stats row */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-[#3d7ab510]">
          <FolderOpen className="w-4 h-4 flex-shrink-0 text-[#3d7ab5]" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">المستندات</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{activeDocs.length}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-green-50">
          <CheckCircle className="w-4 h-4 flex-shrink-0 text-green-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">موقّعة</p>
            <p className="text-sm font-bold text-green-500">{signedCount}</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2.5 flex items-center gap-2.5 bg-purple-50">
          <FileText className="w-4 h-4 flex-shrink-0 text-purple-500" />
          <div className="min-w-0">
            <p className="text-xs truncate text-[#94a3b8]">الأنواع</p>
            <p className="text-sm font-bold text-purple-500">{new Set(activeDocs.map(d => d.documentType).filter(Boolean)).size}</p>
          </div>
        </div>
      </div>

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h3 className="text-sm font-bold text-[#0d2137]">المستندات</h3>
        <div className="flex items-center gap-2">
          {/* Filter */}
          <div className="flex items-center gap-1.5">
            <Filter className="w-3.5 h-3.5 text-[#64748b]" />
            <select
              value={filterType}
              onChange={(e) => setFilterType(e.target.value)}
              className="text-xs border border-[#e8f0f9] rounded-lg px-2 py-1.5 focus:outline-none focus:border-[#3d7ab5] bg-white"
            >
              <option value="">الكل</option>
              {Object.entries(DOCUMENT_TYPES).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </select>
          </div>
          {/* Print media list */}
          <Link
            href={`/patients/${patientId}/print/media`}
            className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
          >
            <Printer className="w-3 h-3" />
            طباعة قائمة المرفقات
          </Link>
          {/* Add button */}
          <button
            onClick={openAddModal}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            إضافة مستند
          </button>
        </div>
      </div>

      {/* Loading */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-16 bg-[#f1f5f9] rounded-xl" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-8 text-red-500 text-sm">{error}</div>
      ) : activeDocs.length === 0 ? (
        <EmptyState
          icon={FolderOpen}
          title="لا توجد مستندات"
          description="لم يتم رفع أي مستندات لهذا المريض"
          actionLabel="رفع مستند"
          actionOnClick={openAddModal}
          actionVariant="secondary"
        />
      ) : (
        <div className="space-y-2">
          {activeDocs.map((doc) => {
            const isImage = isImageFile(doc.mimeType);
            const TypeIcon = DOC_TYPE_ICONS[doc.documentType ?? ""] ?? FolderOpen;
            return (
              <div
                key={doc.id}
                className="flex items-center justify-between p-3 bg-white border border-[#e8f0f9] rounded-xl hover:border-[#dce8f5] transition"
              >
                <div className="flex items-center gap-3 min-w-0">
                  {/* Thumbnail or icon */}
                  <div
                    className={cn("w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0 overflow-hidden cursor-pointer", isImage ? "bg-transparent" : "bg-[#3d7ab510]")}
                    onClick={() => isImage && openPreview(doc)}
                  >
                    {isImage && doc.fileUrl ? (
                      <>
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img src={resolveImageUrl(doc.fileUrl)} alt="" className="w-full h-full object-cover" />
                      </>
                    ) : (
                      <TypeIcon className="w-5 h-5 text-[#3d7ab5]" />
                    )}
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-medium text-[#0d2137] truncate max-w-[200px]">
                        {doc.title ?? "بدون عنوان"}
                      </span>
                      {doc.documentType && (
                        <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", TYPE_COLORS[doc.documentType] ?? "bg-gray-100 text-gray-600")}>
                          {DOCUMENT_TYPES[doc.documentType] ?? doc.documentType}
                        </span>
                      )}
                      {doc.signed && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium flex items-center gap-1">
                          <CheckCircle className="w-3 h-3" />
                          موقّع
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-3 mt-0.5">
                      {doc.fileName && (
                        <span className="text-xs text-[#94a3b8] truncate max-w-[150px]" dir="ltr">{doc.fileName}</span>
                      )}
                      {doc.fileSize != null && doc.fileSize > 0 && (
                        <span className="text-xs text-[#94a3b8]">{formatFileSize(doc.fileSize)}</span>
                      )}
                      <span className="text-xs text-[#94a3b8]">{formatArabicDate(doc.createdAt)}</span>
                    </div>
                    {doc.notes && (
                      <p className="text-xs text-[#94a3b8] mt-0.5 truncate max-w-[300px]">{doc.notes}</p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-1.5 flex-shrink-0">
                  {doc.fileUrl && (
                    isImage ? (
                      <button
                        onClick={() => openPreview(doc)}
                        className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                      >
                        <Eye className="w-3 h-3" />
                        عرض
                      </button>
                    ) : (
                      <a
                        href={resolveImageUrl(doc.fileUrl)}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                      >
                        <Download className="w-3 h-3" />
                        فتح
                      </a>
                    )
                  )}
                  <button
                    onClick={() => openEditModal(doc)}
                    className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg border border-[#e8f0f9] text-[#64748b] hover:bg-[#f8fafc] transition"
                  >
                    <Pencil className="w-3 h-3" />
                  </button>
                  {deleteConfirm === doc.id ? (
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleDelete(doc.id)}
                        className="px-2 py-1 text-xs font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 transition"
                      >
                        تأكيد
                      </button>
                      <button
                        onClick={() => setDeleteConfirm(null)}
                        className="px-2 py-1 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
                      >
                        إلغاء
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setDeleteConfirm(doc.id)}
                      className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg text-red-500 hover:bg-red-50 transition"
                    >
                      <Trash2 className="w-3 h-3" />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ─── Image Preview Modal ────────────────────────────────────────────── */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={previewUrl}
        fileName={previewFileName}
        mimeType={previewMimeType}
      />

      {/* ─── Add/Edit Modal ──────────────────────────────────────────────────── */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowModal(false)}>
          <div
            className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 space-y-4" dir="rtl">
              {/* Modal header */}
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">
                  {editingDoc ? "تعديل المستند" : "إضافة مستند"}
                </h3>
                <button onClick={() => setShowModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form */}
              <div className="space-y-3">
                {/* Title */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">عنوان المستند *</label>
                  <input
                    type="text"
                    value={form.title}
                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                    placeholder="مثل: موافقة علاج تقويم"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Document Type */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">نوع المستند</label>
                  <select
                    value={form.documentType}
                    onChange={(e) => setForm({ ...form, documentType: e.target.value })}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                  >
                    <option value="">— اختر —</option>
                    {Object.entries(DOCUMENT_TYPES).map(([k, v]) => (
                      <option key={k} value={k}>{v}</option>
                    ))}
                  </select>
                </div>

                {/* File Upload (only for new documents) */}
                {!editingDoc && (
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الملف *</label>
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept=".jpg,.jpeg,.png,.webp,.pdf"
                      onChange={handleFileUpload}
                      className="hidden"
                    />
                    {form.fileUrl ? (
                      <div className="flex items-center gap-2 p-2.5 bg-[#f8fafc] border border-[#e8f0f9] rounded-lg">
                        <FileText className="w-4 h-4 text-[#3d7ab5]" />
                        <span className="text-sm text-[#0d2137] truncate flex-1" dir="ltr">{form.fileName}</span>
                        <span className="text-xs text-[#94a3b8]">{formatFileSize(form.fileSize)}</span>
                        <button
                          onClick={() => setForm(prev => ({ ...prev, fileUrl: "", fileName: "", fileSize: 0, mimeType: "" }))}
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
                          "w-full flex items-center justify-center gap-2 p-4 border-2 border-dashed border-[#e8f0f9] rounded-lg text-sm transition",
                          uploading ? "opacity-60 cursor-not-allowed" : "hover:border-[#3d7ab5] hover:bg-[#f8fafc]"
                        )}
                      >
                        <Upload className="w-4 h-4 text-[#94a3b8]" />
                        {uploading ? "جاري الرفع..." : "اختر ملف (JPG, PNG, WebP, PDF — حتى 10 ميجابايت)"}
                      </button>
                    )}
                  </div>
                )}

                {/* Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
                  <textarea
                    value={form.notes}
                    onChange={(e) => setForm({ ...form, notes: e.target.value })}
                    placeholder="ملاحظات إضافية عن المستند"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving ? "bg-[#3d7ab5]/60 cursor-not-allowed" : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                  )}
                >
                  {saving ? "جاري الحفظ..." : editingDoc ? "تحديث" : "إضافة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
