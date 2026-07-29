import { useEffect, useState, useCallback } from "react";
import {
  FileText,
  RefreshCw,
  XCircle,
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Loader2,
  Filter,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import {
  type SmsMessageDto,
  type PagedResult,
  STATUS_MAP,
  STATUS_FILTER_OPTIONS,
  formatFullDate,
} from "./types";

// ─── Messages Tab ─────────────────────────────────────────────────────────────

export function MessagesTab() {
  const [messages, setMessages] = useState<SmsMessageDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const fetchMessages = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (statusFilter) params.set("status", statusFilter);
      const { data } = await api.get<PagedResult<SmsMessageDto>>(
        `/api/sms/messages?${params.toString()}`
      );
      setMessages(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, statusFilter]);

  useEffect(() => {
    fetchMessages();
  }, [fetchMessages]);

  const handleRetry = async (id: string) => {
    setRetryingId(id);
    try {
      await api.post(`/api/sms/messages/${id}/retry`);
      toast.success("تم إعادة إرسال الرسالة بنجاح");
      fetchMessages();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إعادة إرسال الرسالة"));
    } finally {
      setRetryingId(null);
    }
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل الرسائل</p>
        <button
          onClick={fetchMessages}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-gray-400" />
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
            className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          >
            {STATUS_FILTER_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
        <span className="text-xs text-gray-500 mr-auto">
          {totalCount} رسالة
        </span>
        <button
          onClick={fetchMessages}
          className="p-2 rounded-lg text-gray-400 hover:text-clinic-blue hover:bg-blue-50 transition"
          title="تحديث"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Messages Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="h-14 bg-gray-100 rounded-lg" />
            ))}
          </div>
        ) : messages.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد رسائل</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {[
                    "المريض",
                    "رقم الهاتف",
                    "المحتوى",
                    "القالب",
                    "الحالة",
                    "عدد الأجزاء",
                    "التاريخ",
                    "",
                  ].map((h) => (
                    <th
                      key={h}
                      className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {messages.map((msg) => {
                  const statusInfo =
                    STATUS_MAP[msg.status] || STATUS_MAP.pending;
                  const StatusIcon = statusInfo.icon;
                  return (
                    <tr key={msg.id} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                        {msg.patientName || "—"}
                      </td>
                      <td
                        className="px-4 py-3 text-gray-500 font-mono text-xs"
                        dir="ltr"
                      >
                        {msg.phoneNumber}
                      </td>
                      <td className="px-4 py-3 text-gray-600 max-w-[220px]">
                        <p className="truncate">{msg.messageContent}</p>
                        {msg.errorMessage && (
                          <p className="text-xs text-red-500 mt-0.5 flex items-center gap-1">
                            <AlertTriangle className="w-3 h-3 flex-shrink-0" />
                            {msg.errorMessage}
                          </p>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full font-medium">
                          {msg.templateType || "—"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1",
                            statusInfo.color
                          )}
                        >
                          <StatusIcon className="w-2.5 h-2.5" />
                          {statusInfo.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs text-center">
                        {msg.segmentCount}
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs whitespace-nowrap">
                        {formatFullDate(msg.createdAt)}
                      </td>
                      <td className="px-4 py-3">
                        {msg.status === "failed" && msg.retryCount < 3 && (
                          <button
                            onClick={() => handleRetry(msg.id)}
                            disabled={retryingId === msg.id}
                            className="p-1.5 rounded-lg text-blue-600 hover:bg-blue-50 transition disabled:opacity-60"
                            title="إعادة الإرسال"
                          >
                            {retryingId === msg.id ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                              <RefreshCw className="w-4 h-4" />
                            )}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-5 py-3 border-t border-gray-100 bg-gray-50">
            <p className="text-xs text-gray-500">
              صفحة {page} من {totalPages}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(Math.max(1, page - 1))}
                disabled={page <= 1}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white hover:text-clinic-blue disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <button
                onClick={() => setPage(Math.min(totalPages, page + 1))}
                disabled={page >= totalPages}
                className="p-1.5 rounded-lg text-gray-500 hover:bg-white hover:text-clinic-blue disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
