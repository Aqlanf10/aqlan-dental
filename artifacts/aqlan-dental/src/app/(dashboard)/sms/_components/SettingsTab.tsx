import { useEffect, useState, useCallback } from "react";
import {
  RefreshCw,
  Clock,
  CheckCircle2,
  XCircle,
  Loader2,
  Save,
  Settings,
  Zap,
  Phone,
  Eye,
  EyeOff,
  ArrowUpDown,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { type SmsGatewaySettingsDto, inputCls } from "./types";

// ─── Settings Tab ─────────────────────────────────────────────────────────────

export function SettingsTab() {
  const [settings, setSettings] = useState<SmsGatewaySettingsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const [testLoading, setTestLoading] = useState(false);
  const [testResult, setTestResult] = useState<{
    connected: boolean;
    message: string;
  } | null>(null);

  const fetchSettings = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsGatewaySettingsDto>("/api/sms/settings");
      setSettings(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    setSaved(false);
    try {
      const payload: Record<string, unknown> = {
        enabled: settings.enabled,
        apiUrl: settings.apiUrl,
        gatewayMode: settings.gatewayMode || "local_android",
        senderName: settings.senderName,
        dailyLimit: settings.dailyLimit,
        sendAppointmentReminders: settings.sendAppointmentReminders,
        sendPaymentReminders: settings.sendPaymentReminders,
        reminderHours: settings.reminderHours,
      };
      if (apiKey) {
        payload.apiKey = apiKey;
      }
      const { data } = await api.put<SmsGatewaySettingsDto>(
        "/api/sms/settings",
        payload
      );
      setSettings(data);
      setApiKey("");
      setShowApiKey(false);
      setSaved(true);
      toast.success("تم حفظ الإعدادات بنجاح");
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل حفظ الإعدادات"));
    } finally {
      setSaving(false);
    }
  };

  const handleTestConnection = async () => {
    setTestLoading(true);
    setTestResult(null);
    try {
      const { data } = await api.post<{ connected: boolean; message: string }>(
        "/api/sms/test-connection"
      );
      setTestResult(data);
      if (data.connected) {
        toast.success(data.message || "تم الاتصال بنجاح");
      } else {
        toast.error(data.message || "فشل الاتصال");
      }
    } catch (err) {
      const msg = extractErrorMessage(err, "فشل اختبار الاتصال");
      setTestResult({ connected: false, message: msg });
      toast.error(msg);
    } finally {
      setTestLoading(false);
    }
  };

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل الإعدادات</p>
        <button
          onClick={fetchSettings}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading || !settings) {
    return (
      <div className="animate-pulse space-y-3 max-w-2xl">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="h-12 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-5">
      {/* SMS Enabled Toggle */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-5">
        <h3 className="font-bold text-gray-900 flex items-center gap-2">
          <Settings className="w-4 h-4 text-clinic-blue" />
          الإعدادات العامة
        </h3>

        {/* Toggle: SMS Enabled */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">تفعيل الرسائل القصيرة</p>
            <p className="text-xs text-gray-500 mt-0.5">تفعيل أو تعطيل نظام الرسائل القصيرة بالكامل</p>
          </div>
          <button
            onClick={() =>
              setSettings({ ...settings, enabled: !settings.enabled })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.enabled ? "bg-clinic-blue" : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.enabled}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.enabled ? "-translate-x-6" : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Gateway Mode */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            نوع البوابة
          </label>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => setSettings({ ...settings, gatewayMode: "local_android" })}
              className={cn(
                "flex-1 px-3 py-2.5 text-xs font-medium rounded-lg border-2 transition text-center",
                settings.gatewayMode === "local_android" || !settings.gatewayMode
                  ? "border-clinic-blue bg-clinic-blue/5 text-clinic-blue"
                  : "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
              )}
            >
              <Phone className="w-4 h-4 mx-auto mb-1" />
              بوابة أندرويد محلية
            </button>
            <button
              type="button"
              onClick={() => setSettings({ ...settings, gatewayMode: "cloud_api" })}
              className={cn(
                "flex-1 px-3 py-2.5 text-xs font-medium rounded-lg border-2 transition text-center",
                settings.gatewayMode === "cloud_api"
                  ? "border-clinic-blue bg-clinic-blue/5 text-clinic-blue"
                  : "border-gray-200 bg-white text-gray-600 hover:border-gray-300"
              )}
            >
              <Zap className="w-4 h-4 mx-auto mb-1" />
              بوابة سحابية (API)
            </button>
          </div>
          <p className="text-xs text-gray-400 mt-1">
            {settings.gatewayMode === "cloud_api"
              ? "أدخل رابط إرسال الرسائل الكامل (Full Endpoint URL)"
              : "أدخل الرابط الأساسي للبوابة (Base URL) — سيتم إضافة /sms/send تلقائياً"}
          </p>
        </div>

        {/* Gateway URL */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            عنوان بوابة الرسائل (URL)
          </label>
          <input
            value={settings.apiUrl ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, apiUrl: e.target.value })
            }
            className={inputCls}
            placeholder={settings.gatewayMode === "cloud_api" ? "https://us-central1-xxx.cloudfunctions.net/api_sms_send" : "http://192.168.1.100:8080"}
            dir="ltr"
          />
          {settings.gatewayMode !== "cloud_api" && (
            <p className="text-xs text-gray-400 mt-1">
              سيتم إضافة <code className="bg-gray-100 px-1 rounded font-mono" dir="ltr">/sms/send</code> و <code className="bg-gray-100 px-1 rounded font-mono" dir="ltr">/status</code> تلقائياً
            </p>
          )}
        </div>

        {/* API Key */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            مفتاح API
          </label>
          <div className="relative">
            <input
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              type={showApiKey ? "text" : "password"}
              className={cn(inputCls, "pl-10")}
              placeholder={settings.hasApiKey ? "•••••••• (اتركه فارغًا للإبقاء على الحالي)" : "أدخل مفتاح API"}
              dir="ltr"
            />
            <button
              type="button"
              onClick={() => setShowApiKey(!showApiKey)}
              className="absolute left-2 top-1/2 -translate-y-1/2 p-1 text-gray-400 hover:text-gray-600 transition"
            >
              {showApiKey ? (
                <EyeOff className="w-4 h-4" />
              ) : (
                <Eye className="w-4 h-4" />
              )}
            </button>
          </div>
        </div>

        {/* Sender Name */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            اسم المرسل
          </label>
          <input
            value={settings.senderName ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, senderName: e.target.value })
            }
            className={inputCls}
            placeholder="AqlanDental"
            dir="ltr"
          />
        </div>

        {/* Daily Limit */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            الحد اليومي للرسائل
          </label>
          <input
            type="number"
            value={settings.dailyLimit}
            onChange={(e) =>
              setSettings({
                ...settings,
                dailyLimit: parseInt(e.target.value) || 0,
              })
            }
            className={inputCls}
            min={0}
            dir="ltr"
          />
        </div>
      </div>

      {/* Reminders Section */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-5">
        <h3 className="font-bold text-gray-900 flex items-center gap-2">
          <Clock className="w-4 h-4 text-clinic-blue" />
          التذكيرات
        </h3>

        {/* Toggle: Appointment Reminders */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">
              تذكيرات المواعيد
            </p>
            <p className="text-xs text-gray-500 mt-0.5">
              إرسال تذكير تلقائي قبل الموعد
            </p>
          </div>
          <button
            onClick={() =>
              setSettings({
                ...settings,
                sendAppointmentReminders: !settings.sendAppointmentReminders,
              })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.sendAppointmentReminders
                ? "bg-clinic-blue"
                : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.sendAppointmentReminders}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.sendAppointmentReminders
                  ? "-translate-x-6"
                  : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Toggle: Payment Reminders */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-gray-700">
              تذكيرات الدفع
            </p>
            <p className="text-xs text-gray-500 mt-0.5">
              إرسال تذكير بالمبالغ المستحقة
            </p>
          </div>
          <button
            onClick={() =>
              setSettings({
                ...settings,
                sendPaymentReminders: !settings.sendPaymentReminders,
              })
            }
            className={cn(
              "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
              settings.sendPaymentReminders
                ? "bg-clinic-blue"
                : "bg-gray-300"
            )}
            role="switch"
            aria-checked={settings.sendPaymentReminders}
          >
            <span
              className={cn(
                "inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform",
                settings.sendPaymentReminders
                  ? "-translate-x-6"
                  : "-translate-x-1"
              )}
            />
          </button>
        </div>

        {/* Reminder Hours */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            ساعات التذكير
          </label>
          <input
            value={settings.reminderHours ?? ""}
            onChange={(e) =>
              setSettings({ ...settings, reminderHours: e.target.value })
            }
            className={inputCls}
            placeholder="24,2"
            dir="ltr"
          />
          <p className="text-xs text-gray-400 mt-1">
            أدخل الساعات مفصولة بفواصل (مثال: 24,2 يعني تذكير قبل 24 ساعة وساعتين)
          </p>
        </div>
      </div>

      {/* Test & Save */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        {/* Test Connection */}
        <div className="space-y-3">
          <h3 className="font-bold text-gray-900 flex items-center gap-2">
            <ArrowUpDown className="w-4 h-4 text-clinic-blue" />
            اختبار الاتصال
          </h3>
          <button
            onClick={handleTestConnection}
            disabled={testLoading}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60 transition"
          >
            {testLoading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Zap className="w-4 h-4" />
            )}
            {testLoading ? "جارٍ الاختبار..." : "اختبار الاتصال"}
          </button>
          {testResult && (
            <div
              className={cn(
                "text-xs px-3 py-2 rounded-lg flex items-center gap-2",
                testResult.connected
                  ? "bg-green-50 text-green-700 border border-green-200"
                  : "bg-red-50 text-red-700 border border-red-200"
              )}
            >
              {testResult.connected ? (
                <CheckCircle2 className="w-4 h-4 flex-shrink-0" />
              ) : (
                <XCircle className="w-4 h-4 flex-shrink-0" />
              )}
              {testResult.message}
            </div>
          )}
        </div>

        {/* Save */}
        <div className="flex items-center gap-3 pt-2 border-t border-gray-100">
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
          </button>
          {saved && (
            <span className="text-sm text-green-600 font-medium flex items-center gap-1">
              <CheckCircle2 className="w-4 h-4" />
              تم الحفظ
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
