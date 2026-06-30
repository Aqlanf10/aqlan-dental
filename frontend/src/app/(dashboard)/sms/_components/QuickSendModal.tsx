"use client";
import { useEffect, useState } from "react";
import { Send, X, Search, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { type SmsTemplateDto, inputCls } from "./types";

// ─── Quick Send Modal ─────────────────────────────────────────────────────────

export interface QuickSendModalProps {
  onClose: () => void;
}

export function QuickSendModal({ onClose }: QuickSendModalProps) {
  const [patientSearch, setPatientSearch] = useState("");
  const [patients, setPatients] = useState<
    { id: string; name: string; phone: string }[]
  >([]);
  const [selectedPatient, setSelectedPatient] = useState<{
    id: string;
    name: string;
    phone: string;
  } | null>(null);
  const [searching, setSearching] = useState(false);
  const [templates, setTemplates] = useState<SmsTemplateDto[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<string>("");
  const [message, setMessage] = useState("");
  const [sending, setSending] = useState(false);

  // Fetch templates for dropdown
  useEffect(() => {
    api
      .get<SmsTemplateDto[]>("/api/sms/templates")
      .then(({ data }) => setTemplates(data.filter((t) => t.isTemplateActive)))
      .catch(() => {});
  }, []);

  // Search patients
  useEffect(() => {
    if (!patientSearch.trim() || patientSearch.trim().length < 2) {
      setPatients([]);
      return;
    }
    const timer = setTimeout(async () => {
      setSearching(true);
      try {
        const { data } = await api.get<{
          data: { id: string; fullName: string; phone?: string }[];
          items?: { id: string; fullName: string; phone?: string }[];
        }>(`/api/patients?search=${encodeURIComponent(patientSearch.trim())}&pageSize=10`);
        // API returns { data: [...] } from PaginatedResponse, but doctor access returns { items: [...] }
        const list = data.data ?? data.items ?? [];
        setPatients(list.map(p => ({ id: p.id, name: p.fullName, phone: p.phone ?? "" })));
      } catch {
        setPatients([]);
      } finally {
        setSearching(false);
      }
    }, 350);
    return () => clearTimeout(timer);
  }, [patientSearch]);

  // When a template is selected, fill the message
  const handleTemplateSelect = (templateKey: string) => {
    setSelectedTemplate(templateKey);
    const tmpl = templates.find((t) => t.templateKey === templateKey);
    if (tmpl) {
      setMessage(tmpl.contentTemplate);
    }
  };

  const handleSend = async () => {
    if (!selectedPatient || !message.trim()) return;
    setSending(true);
    try {
      await api.post("/api/sms/send", {
        patientId: selectedPatient.id,
        messageContent: message.trim(),
        templateType: selectedTemplate || undefined,
      });
      toast.success(`تم إرسال الرسالة إلى ${selectedPatient.name}`);
      onClose();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إرسال الرسالة"));
    } finally {
      setSending(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
    >
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-5 max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-gray-900 flex items-center gap-2">
            <Send className="w-5 h-5 text-clinic-blue" />
            إرسال رسالة سريعة
          </h3>
          <button
            onClick={onClose}
            className="p-1 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Patient Search */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            المريض <span className="text-red-500">*</span>
          </label>
          {selectedPatient ? (
            <div className="flex items-center justify-between px-3 py-2 bg-clinic-blue/5 border border-clinic-blue/20 rounded-lg">
              <div>
                <p className="text-sm font-medium text-gray-900">
                  {selectedPatient.name}
                </p>
                <p
                  className="text-xs text-gray-500 font-mono"
                  dir="ltr"
                >
                  {selectedPatient.phone}
                </p>
              </div>
              <button
                onClick={() => {
                  setSelectedPatient(null);
                  setPatientSearch("");
                }}
                className="p-1 rounded-lg text-gray-400 hover:text-red-500 hover:bg-red-50 transition"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <div className="relative">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                type="text"
                value={patientSearch}
                onChange={(e) => setPatientSearch(e.target.value)}
                placeholder="ابحث عن مريض..."
                className={cn(inputCls, "pr-9")}
              />
              {searching && (
                <Loader2 className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-gray-400" />
              )}
              {/* Search Results */}
              {patients.length > 0 && !selectedPatient && (
                <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                  {patients.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => {
                        setSelectedPatient(p);
                        setPatientSearch("");
                        setPatients([]);
                      }}
                      className="w-full text-right px-3 py-2 hover:bg-gray-50 transition flex items-center justify-between"
                    >
                      <span className="text-sm font-medium text-gray-900">
                        {p.name}
                      </span>
                      <span
                        className="text-xs text-gray-400 font-mono"
                        dir="ltr"
                      >
                        {p.phone}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Template Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            القالب
          </label>
          <select
            value={selectedTemplate}
            onChange={(e) => handleTemplateSelect(e.target.value)}
            className={inputCls}
          >
            <option value="">بدون قالب</option>
            {templates.map((t) => (
              <option key={t.id} value={t.templateKey}>
                {t.nameAr}
              </option>
            ))}
          </select>
        </div>

        {/* Message Content */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            محتوى الرسالة <span className="text-red-500">*</span>
          </label>
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            rows={4}
            className={cn(
              "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none",
              message.length > 160 && "border-amber-300"
            )}
           
            placeholder="اكتب رسالتك هنا..."
          />
          <div className="flex items-center justify-between mt-1">
            <span
              className={cn(
                "text-xs",
                message.length > 160
                  ? "text-amber-600 font-medium"
                  : "text-gray-400"
              )}
            >
              {message.length}/160
              {message.length > 160 &&
                ` · ${Math.ceil(message.length / 153)} أجزاء`}
            </span>
            {message.length > 160 && (
              <span className="text-xs text-amber-600">
                سيتم تقسيم الرسالة
              </span>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-3 pt-2">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
          >
            إلغاء
          </button>
          <button
            onClick={handleSend}
            disabled={!selectedPatient || !message.trim() || sending}
            className="px-5 py-2 text-sm font-medium text-white bg-clinic-blue rounded-lg hover:opacity-90 disabled:opacity-60 transition flex items-center gap-2"
          >
            {sending ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Send className="w-4 h-4" />
            )}
            {sending ? "جارٍ الإرسال..." : "إرسال"}
          </button>
        </div>
      </div>
    </div>
  );
}
