// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import { useEffect, useState } from "react";
import {
  AlertTriangle, Eye, EyeOff, CheckCircle2, XCircle, Loader2,
  Save, PlugZap,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { inputCls } from "./_shared";

// ─── AI Tab (Ceph C-D draft assistant configuration) ─────────────────────────
interface AiKeyStatus {
  configured: boolean;
  /** "********xxxx" (last 4 only) — the raw key never leaves the server. */
  masked: string | null;
  source: "settings" | "environment" | "none" | "invalid";
}

interface AiSettingsDto {
  enabled: boolean;
  provider: string;
  model: string;
  maxTokens: number;
  temperature: number;
  monthlyLimit: number;
  usageThisMonth: number;
  usageAvailable: boolean;
  usageWarning?: string | null;
  keyStatus: Record<string, AiKeyStatus>;
}

const AI_PROVIDERS: { value: string; label: string; envVar: string; available: boolean }[] = [
  { value: "gemini",    label: "Gemini (Google)",  envVar: "GEMINI_API_KEY",    available: true },
  { value: "anthropic", label: "Anthropic (Claude)", envVar: "ANTHROPIC_API_KEY", available: true },
  { value: "openai",    label: "OpenAI (قريبًا)",   envVar: "OPENAI_API_KEY",    available: false },
];

export function AiTab() {
  const [settings, setSettings] = useState<AiSettingsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [secretValue, setSecretValue] = useState("");
  const [showSecret, setShowSecret] = useState(false);
  const [clearStoredSecret, setClearStoredSecret] = useState(false);

  useEffect(() => {
    api.get<AiSettingsDto>("/api/ai-settings")
      .then((r) => setSettings(r.data))
      .catch((err) => setError(extractErrorMessage(err, "تعذر تحميل إعدادات الذكاء الاصطناعي")))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      const { data } = await api.put<AiSettingsDto>("/api/ai-settings", {
        enabled: settings.enabled,
        provider: settings.provider,
        model: settings.model,
        maxTokens: settings.maxTokens,
        temperature: settings.temperature,
        monthlyLimit: settings.monthlyLimit,
        secretValue: secretValue.trim() || undefined,
        clearStoredSecret,
      });
      setSettings(data);
      setSecretValue("");
      setShowSecret(false);
      setClearStoredSecret(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      setError(extractErrorMessage(err, "حدث خطأ أثناء حفظ إعدادات الذكاء الاصطناعي"));
    } finally {
      setSaving(false);
    }
  };

  const handleTestConnection = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const { data } = await api.post<{ ok: boolean; message: string }>("/api/ai-settings/test-connection");
      setTestResult(data);
    } catch (err) {
      setTestResult({ ok: false, message: extractErrorMessage(err, "تعذر إجراء التحقق المحلي") });
    } finally {
      setTesting(false);
    }
  };

  if (loading) {
    return <div className="animate-pulse space-y-3">{Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  if (!settings) {
    return <p className="text-sm text-red-600 bg-red-50 px-3 py-2 rounded-lg">{error ?? "تعذر تحميل إعدادات الذكاء الاصطناعي"}</p>;
  }

  return (
    <div className="space-y-5">
      {error && <p className="text-xs text-red-600 bg-red-50 px-3 py-2 rounded-lg">{error}</p>}
      {!settings.usageAvailable && settings.usageWarning && (
        <p className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
          <span>{settings.usageWarning}. يمكن تعديل الإعدادات الآن، لكن عداد الاستخدام سيبقى متوقفًا.</span>
        </p>
      )}

      <div className="text-xs px-3 py-2 rounded-lg bg-amber-50 text-amber-800 border border-amber-200 flex items-start gap-2">
        <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
        <span>
          مساعد السيفالو يولّد <b>مسودة نقاط ومسودة تشخيص</b> تتطلبان مراجعة واعتماد أخصائي التقويم.
          عند طلب مسودة النقاط تُرسل صورة الأشعة إلى المزود المحدد، ولا يُحفظ أي ناتج تلقائيًا،
          وكل استخدام يُسجَّل في سجل التدقيق.
        </span>
      </div>

      {/* Enable toggle */}
      <label className="flex items-center gap-3 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={settings.enabled}
          onChange={(e) => setSettings({ ...settings, enabled: e.target.checked })}
          className="w-4 h-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
        />
        <span className="text-sm font-medium text-gray-800">تفعيل مساعد السيفالو (مسودة النقاط والتشخيص)</span>
      </label>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">المزود</label>
          <select
            value={settings.provider}
            onChange={(e) => setSettings({ ...settings, provider: e.target.value })}
            className={inputCls}
          >
            {AI_PROVIDERS.map((p) => (
              <option key={p.value} value={p.value}>{p.label}</option>
            ))}
          </select>
          {settings.provider === "openai" && (
            <p className="text-xs text-amber-600 mt-1">مزود openai غير مدعوم بعد — سيرفض النظام التوليد بهذا المزود.</p>
          )}
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">النموذج</label>
          <input
            value={settings.model}
            onChange={(e) => setSettings({ ...settings, model: e.target.value })}
            className={inputCls}
            placeholder="gemini-3.5-flash"
            dir="ltr"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">الحد الأقصى للرموز (100 – 8000)</label>
          <input
            type="number"
            min={100}
            max={8000}
            value={settings.maxTokens}
            onChange={(e) => setSettings({ ...settings, maxTokens: Number(e.target.value) })}
            className={inputCls}
            dir="ltr"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">درجة الإبداع Temperature (0 – 1)</label>
          <input
            type="number"
            min={0}
            max={1}
            step={0.1}
            value={settings.temperature}
            onChange={(e) => setSettings({ ...settings, temperature: Number(e.target.value) })}
            className={inputCls}
            dir="ltr"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">الحد الشهري للاستخدام (0 = بلا حد)</label>
          <input
            type="number"
            min={0}
            value={settings.monthlyLimit}
            onChange={(e) => setSettings({ ...settings, monthlyLimit: Number(e.target.value) })}
            className={inputCls}
            dir="ltr"
          />
          <p className="text-xs text-gray-500 mt-1">الاستخدام هذا الشهر: <b>{settings.usageThisMonth}</b> توليد ناجح</p>
        </div>
      </div>

      {/* Key status — read-only, masked. Keys are configured via server env vars only. */}
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-3">
        <p className="text-sm font-semibold text-gray-700">إدارة مفتاح API</p>
        <p className="text-xs text-gray-500">
          يُحفظ المفتاح مشفراً في الخادم ولا يُعاد إلى المتصفح. يظهر فقط آخر 4 خانات، ويمكن لمتغيرات بيئة الاستضافة أن تعمل كخيار احتياطي.
        </p>
        <div className="space-y-2">
          {AI_PROVIDERS.map((p) => {
            const status = settings.keyStatus[p.value];
            return (
              <div key={p.value} className="flex items-center justify-between rounded-lg border border-white bg-white px-3 py-2">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-gray-800">{p.label}</span>
                  <code className="text-[10px] text-gray-400" dir="ltr">{p.envVar}</code>
                </div>
                {status?.configured ? (
                  <div className="flex items-center gap-2">
                    <span className="text-[10px] text-gray-500">
                      {status.source === "settings" ? "محفوظ مشفراً" : "متغير استضافة"}
                    </span>
                    <span className="text-xs bg-green-50 text-green-700 px-2 py-0.5 rounded-full font-medium font-mono" dir="ltr">
                      {status.masked ?? "********"}
                    </span>
                  </div>
                ) : (
                  <span className="text-xs bg-gray-100 text-gray-500 px-2 py-0.5 rounded-full font-medium">غير مهيأ</span>
                )}
              </div>
            );
          })}
        </div>

        <div className="border-t border-gray-200 pt-3">
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            استبدال مفتاح {AI_PROVIDERS.find((p) => p.value === settings.provider)?.label}
          </label>
          <div className="relative">
            <input
              value={secretValue}
              onChange={(event) => {
                setSecretValue(event.target.value);
                if (event.target.value) setClearStoredSecret(false);
              }}
              type={showSecret ? "text" : "password"}
              autoComplete="new-password"
              className={`${inputCls} pl-10 font-mono`}
              placeholder={settings.keyStatus[settings.provider]?.configured
                ? "اتركه فارغاً للإبقاء على المفتاح الحالي"
                : "أدخل مفتاح API"}
              dir="ltr"
            />
            <button
              type="button"
              onClick={() => setShowSecret((value) => !value)}
              className="absolute left-2 top-1/2 -translate-y-1/2 rounded p-1 text-gray-500 hover:bg-gray-100"
              title={showSecret ? "إخفاء المفتاح" : "إظهار المفتاح"}
            >
              {showSecret ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
          {settings.keyStatus[settings.provider]?.source === "settings" && (
            <label className="mt-2 flex items-center gap-2 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={clearStoredSecret}
                onChange={(event) => {
                  setClearStoredSecret(event.target.checked);
                  if (event.target.checked) setSecretValue("");
                }}
                className="h-4 w-4 rounded border-gray-300 text-clinic-blue"
              />
              حذف المفتاح المحفوظ والعودة إلى متغير الاستضافة إن وُجد
            </label>
          )}
        </div>
      </div>

      {testResult && (
        <div className={cn(
          "text-xs px-3 py-2 rounded-lg border flex items-start gap-2",
          testResult.ok ? "bg-green-50 text-green-800 border-green-200" : "bg-red-50 text-red-700 border-red-200"
        )}>
          {testResult.ok ? <CheckCircle2 className="w-4 h-4 mt-0.5 flex-shrink-0" /> : <XCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />}
          <span>{testResult.message}</span>
        </div>
      )}

      <div className="flex items-center gap-3 pt-2 flex-wrap">
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
        </button>
        <button
          onClick={handleTestConnection}
          disabled={testing}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 bg-white text-gray-700 hover:border-clinic-blue hover:text-clinic-blue disabled:opacity-60 transition"
        >
          {testing ? <Loader2 className="w-4 h-4 animate-spin" /> : <PlugZap className="w-4 h-4" />}
          فحص الإعداد (تحقق محلي)
        </button>
        {saved && <span className="text-sm text-green-600 font-medium">✓ تم الحفظ</span>}
      </div>
    </div>
  );
}
