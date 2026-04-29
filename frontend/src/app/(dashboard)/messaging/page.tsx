"use client";

import { useState } from "react";
import { Bell, Send, Search, User, Clock, CheckCheck } from "lucide-react";
import api from "@/lib/api";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

interface Message {
  id: string;
  senderId: string;
  senderName: string;
  recipientId: string;
  recipientName: string;
  subject: string;
  body: string;
  isRead: boolean;
  createdAt: string;
  category: string;
}

export default function MessagingPage() {
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [composeOpen, setComposeOpen] = useState(false);
  const [selectedMessage, setSelectedMessage] = useState<Message | null>(null);
  const [composeForm, setComposeForm] = useState({
    recipientId: "",
    subject: "",
    body: "",
    category: "general",
  });

  const queryClient = useQueryClient();

  const { data: messagesData, isLoading } = useQuery({
    queryKey: ["messages", selectedCategory],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (selectedCategory !== "all") params.set("category", selectedCategory);
      const { data } = await api.get(`/api/notifications?${params.toString()}`);
      return data as { notifications: Message[]; unreadCount: number };
    },
    staleTime: 15_000,
  });

  const markReadMutation = useMutation({
    mutationFn: async (id: string) => {
      await api.put(`/api/notifications/${id}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["messages"] });
    },
  });

  const messages = messagesData?.notifications ?? [];
  const filteredMessages = messages.filter(
    (m) =>
      m.subject?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      m.body?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      m.senderName?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const categories = [
    { value: "all", label: "الكل" },
    { value: "general", label: "عام" },
    { value: "surgery", label: "الجراحة" },
    { value: "ortho", label: "التقويم" },
    { value: "appointment", label: "المواعيد" },
    { value: "prescription", label: "الوصفات" },
  ];

  const getCategoryColor = (cat: string) => {
    const colors: Record<string, string> = {
      general: "bg-gray-100 text-gray-700",
      surgery: "bg-red-100 text-red-700",
      ortho: "bg-blue-100 text-blue-700",
      appointment: "bg-amber-100 text-amber-700",
      prescription: "bg-green-100 text-green-700",
    };
    return colors[cat] ?? "bg-gray-100 text-gray-700";
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-extrabold text-[#0d2137]">الرسائل</h2>
          <p className="text-sm text-gray-500 mt-1">
            إدارة الرسائل والإشعارات الداخلية
          </p>
        </div>
        <button
          onClick={() => setComposeOpen(true)}
          className="flex items-center gap-2 px-5 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors shadow-sm"
        >
          <Send className="w-4 h-4" />
          رسالة جديدة
        </button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[220px] max-w-sm">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 right-3 text-gray-400" />
          <input
            type="text"
            placeholder="بحث في الرسائل..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-10 pr-10 pl-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5]"
          />
        </div>
        <div className="flex items-center gap-1.5">
          {categories.map((cat) => (
            <button
              key={cat.value}
              onClick={() => setSelectedCategory(cat.value)}
              className={`px-3 py-1.5 text-xs font-bold rounded-lg transition-colors ${
                selectedCategory === cat.value
                  ? "bg-[#3d7ab5] text-white"
                  : "bg-[#f0f5fb] text-gray-600 hover:bg-[#dce8f5]"
              }`}
            >
              {cat.label}
            </button>
          ))}
        </div>
      </div>

      {/* Messages List */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="animate-pulse h-20 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : filteredMessages.length === 0 ? (
        <div className="text-center py-20">
          <Bell className="w-16 h-16 mx-auto text-gray-200 mb-4" />
          <p className="text-gray-400 text-lg font-bold">لا توجد رسائل</p>
          <p className="text-gray-300 text-sm mt-1">ستظهر الرسائل الجديدة هنا</p>
        </div>
      ) : (
        <div className="space-y-2">
          {filteredMessages.map((msg) => (
            <div
              key={msg.id}
              onClick={() => {
                setSelectedMessage(msg);
                if (!msg.isRead) markReadMutation.mutate(msg.id);
              }}
              className={`p-4 rounded-xl border cursor-pointer transition-all hover:shadow-md ${
                msg.isRead
                  ? "bg-white border-[#e8f0f9]"
                  : "bg-[#f0f5fb] border-[#3d7ab5]/20 border-r-4 border-r-[#3d7ab5]"
              }`}
            >
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-full bg-[#3d7ab5]/10 flex items-center justify-center flex-shrink-0">
                  <User className="w-5 h-5 text-[#3d7ab5]" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-[#0d2137]">
                      {msg.senderName || "النظام"}
                    </span>
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${getCategoryColor(msg.category)}`}>
                      {msg.category}
                    </span>
                    {msg.isRead && <CheckCheck className="w-3.5 h-3.5 text-[#3d7ab5] ms-auto" />}
                  </div>
                  <p className="text-sm font-semibold text-[#0d2137] truncate">
                    {msg.subject || "إشعار"}
                  </p>
                  <p className="text-xs text-gray-500 truncate mt-0.5">
                    {msg.body}
                  </p>
                  <div className="flex items-center gap-1 mt-1.5">
                    <Clock className="w-3 h-3 text-gray-300" />
                    <span className="text-[11px] text-gray-400">{msg.createdAt}</span>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Message Detail Modal */}
      {selectedMessage && (
        <div
          className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4"
          onClick={() => setSelectedMessage(null)}
        >
          <div
            className="bg-white rounded-2xl shadow-2xl max-w-lg w-full p-6"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-lg font-extrabold text-[#0d2137] mb-2">
              {selectedMessage.subject || "إشعار"}
            </h3>
            <p className="text-sm text-gray-400 mb-4">
              من: {selectedMessage.senderName || "النظام"} — {selectedMessage.createdAt}
            </p>
            <div className="bg-[#f7fafd] rounded-xl p-4 text-sm text-gray-700 leading-relaxed">
              {selectedMessage.body}
            </div>
            <button
              onClick={() => setSelectedMessage(null)}
              className="mt-4 w-full py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors"
            >
              إغلاق
            </button>
          </div>
        </div>
      )}

      {/* Compose Modal */}
      {composeOpen && (
        <div
          className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4"
          onClick={() => setComposeOpen(false)}
        >
          <div
            className="bg-white rounded-2xl shadow-2xl max-w-lg w-full p-6"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-lg font-extrabold text-[#0d2137] mb-4">
              رسالة جديدة
            </h3>
            <div className="space-y-3">
              <input
                type="text"
                placeholder="الموضوع"
                value={composeForm.subject}
                onChange={(e) =>
                  setComposeForm((f) => ({ ...f, subject: e.target.value }))
                }
                className="w-full h-10 px-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30"
              />
              <select
                value={composeForm.category}
                onChange={(e) =>
                  setComposeForm((f) => ({ ...f, category: e.target.value }))
                }
                className="w-full h-10 px-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30"
              >
                {categories.filter((c) => c.value !== "all").map((cat) => (
                  <option key={cat.value} value={cat.value}>
                    {cat.label}
                  </option>
                ))}
              </select>
              <textarea
                placeholder="نص الرسالة..."
                rows={5}
                value={composeForm.body}
                onChange={(e) =>
                  setComposeForm((f) => ({ ...f, body: e.target.value }))
                }
                className="w-full px-4 py-3 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 resize-none"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => setComposeOpen(false)}
                  className="flex-1 py-2.5 bg-gray-100 text-gray-600 rounded-xl text-sm font-bold hover:bg-gray-200 transition-colors"
                >
                  إلغاء
                </button>
                <button
                  onClick={() => {
                    setComposeOpen(false);
                    setComposeForm({ recipientId: "", subject: "", body: "", category: "general" });
                  }}
                  className="flex-1 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors"
                >
                  إرسال
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
