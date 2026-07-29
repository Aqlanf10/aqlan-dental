import { useEffect, useState, useCallback } from "react";
import {
  MessageSquare,
  RefreshCw,
  XCircle,
  Pencil,
  Loader2,
  Save,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import {
  type SmsTemplateDto,
  CATEGORY_COLORS,
  CATEGORY_LABELS,
} from "./types";

// ─── Templates Tab ────────────────────────────────────────────────────────────

export function TemplatesTab() {
  const [templates, setTemplates] = useState<SmsTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsTemplateDto[]>("/api/sms/templates");
      setTemplates(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

  const handleEdit = (t: SmsTemplateDto) => {
    setEditingId(t.id);
    setEditContent(t.contentTemplate);
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setEditContent("");
  };

  const handleSave = async (t: SmsTemplateDto) => {
    setSaving(true);
    try {
      const { data } = await api.put<SmsTemplateDto>(
        `/api/sms/templates/${t.id}`,
        { contentTemplate: editContent, isTemplateActive: t.isTemplateActive }
      );
      setTemplates((prev) =>
        prev.map((tmpl) => (tmpl.id === data.id ? data : tmpl))
      );
      setEditingId(null);
      setEditContent("");
      toast.success("تم تحديث القالب بنجاح");
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل تحديث القالب"));
    } finally {
      setSaving(false);
    }
  };

  /** Highlight {{placeholder}} patterns in template content */
  const renderTemplateContent = (content: string) => {
    const parts = content.split(/({{[^}]+}})/g);
    return parts.map((part, i) =>
      /{{[^}]+}}/.test(part) ? (
        <span
          key={i}
          className="bg-clinic-blue/10 text-clinic-blue font-semibold px-1 rounded"
        >
          {part}
        </span>
      ) : (
        <span key={i}>{part}</span>
      )
    );
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل القوالب</p>
        <button
          onClick={fetchTemplates}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-40 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">{templates.length} قالب</p>
        <button
          onClick={fetchTemplates}
          className="p-2 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
          title="تحديث"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {templates.length === 0 ? (
        <div className="text-center py-16 text-gray-400 bg-white rounded-xl border border-gray-200">
          <MessageSquare className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد قوالب</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {templates.map((t) => {
            const isEditing = editingId === t.id;
            const currentContent = isEditing ? editContent : t.contentTemplate;
            const charCount = currentContent.length;
            const maxLen = t.maxLength || 160;
            const isOverLimit = charCount > maxLen;

            return (
              <div
                key={t.id}
                className={cn(
                  "bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-3 transition",
                  isEditing && "ring-2 ring-clinic-blue/30 border-clinic-blue/30"
                )}
              >
                {/* Header */}
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-gray-900 truncate">
                      {t.nameAr}
                    </p>
                    <p className="text-[10px] text-gray-400 font-mono mt-0.5">
                      {t.templateKey}
                    </p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span
                      className={cn(
                        "text-[10px] px-2 py-0.5 rounded-full font-medium",
                        CATEGORY_COLORS[t.category] || CATEGORY_COLORS.general
                      )}
                    >
                      {CATEGORY_LABELS[t.category] || t.category}
                    </span>
                    {!isEditing && (
                      <button
                        onClick={() => handleEdit(t)}
                        className="p-1.5 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
                        title="تعديل"
                      >
                        <Pencil className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                {/* Content */}
                {isEditing ? (
                  <div className="space-y-2">
                    <textarea
                      value={editContent}
                      onChange={(e) => setEditContent(e.target.value)}
                      rows={4}
                      className={cn(
                        "w-full px-3 py-2 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none font-sans",
                        isOverLimit
                          ? "border-red-300 bg-red-50/30"
                          : "border-gray-300 bg-white"
                      )}
                     
                    />
                    <div className="flex items-center justify-between">
                      <span
                        className={cn(
                          "text-xs",
                          isOverLimit ? "text-red-600 font-bold" : "text-gray-400"
                        )}
                      >
                        {charCount}/{maxLen}
                      </span>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={handleCancelEdit}
                          className="px-3 py-1 text-xs rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
                        >
                          إلغاء
                        </button>
                        <button
                          onClick={() => handleSave(t)}
                          disabled={saving}
                          className="px-3 py-1 text-xs rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition flex items-center gap-1"
                        >
                          {saving ? (
                            <Loader2 className="w-3 h-3 animate-spin" />
                          ) : (
                            <Save className="w-3 h-3" />
                          )}
                          حفظ
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="text-xs text-gray-600 bg-gray-50 rounded-lg p-3 whitespace-pre-wrap font-sans leading-relaxed">
                      {renderTemplateContent(t.contentTemplate)}
                    </div>
                    <div className="flex items-center justify-between">
                      <span
                        className={cn(
                          "text-[10px]",
                          charCount > maxLen
                            ? "text-red-500 font-semibold"
                            : "text-gray-400"
                        )}
                      >
                        {charCount}/{maxLen}
                      </span>
                      <span
                        className={cn(
                          "text-[10px] px-2 py-0.5 rounded-full font-medium",
                          t.isTemplateActive
                            ? "bg-green-100 text-green-700"
                            : "bg-gray-100 text-gray-500"
                        )}
                      >
                        {t.isTemplateActive ? "مفعّل" : "معطّل"}
                      </span>
                    </div>
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
