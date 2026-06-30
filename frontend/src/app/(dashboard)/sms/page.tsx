"use client";
import { useState } from "react";
import { MessageSquare, Zap } from "lucide-react";
import { cn } from "@/lib/utils";
import { type Tab, TABS } from "./_components/types";
import { DashboardTab } from "./_components/DashboardTab";
import { MessagesTab } from "./_components/MessagesTab";
import { TemplatesTab } from "./_components/TemplatesTab";
import { SettingsTab } from "./_components/SettingsTab";
import { QuickSendModal } from "./_components/QuickSendModal";

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function SmsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("dashboard");
  const [showQuickSend, setShowQuickSend] = useState(false);

  return (
    <div className="space-y-5 max-w-6xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <MessageSquare className="w-6 h-6 text-clinic-blue" />
            إدارة الرسائل القصيرة
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">إرسال وتتبع الرسائل القصيرة للمرضى</p>
        </div>
        <button
          onClick={() => setShowQuickSend(true)}
          className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition shadow-sm"
        >
          <Zap className="w-4 h-4" />
          إرسال سريع
        </button>
      </div>

      {/* Tabs */}
      <div className="inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1">
        {TABS.map((tab) => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={cn(
                "flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md transition",
                activeTab === tab.key
                  ? "bg-white text-clinic-blue shadow-sm"
                  : "text-gray-600 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      {activeTab === "dashboard" && <DashboardTab />}
      {activeTab === "messages" && <MessagesTab />}
      {activeTab === "templates" && <TemplatesTab />}
      {activeTab === "settings" && <SettingsTab />}

      {/* Quick Send Modal */}
      {showQuickSend && (
        <QuickSendModal onClose={() => setShowQuickSend(false)} />
      )}
    </div>
  );
}
