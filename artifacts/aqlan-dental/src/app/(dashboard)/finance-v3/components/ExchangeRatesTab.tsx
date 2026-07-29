
import { useEffect, useState } from "react";
import { RefreshCw, Save, TrendingUp } from "lucide-react";
import { api } from "@/lib/api";
import { EmptyState, LoadingSkeleton, tokens } from "./FinanceSharedUI";

interface ExchangeRateRow {
  currency: "SAR" | "USD";
  baseCurrency: "YER";
  rateToYer: number | null;
  source: string;
}

interface ExchangeRatesResponse {
  baseCurrency: "YER";
  updatedAt: string;
  rates: ExchangeRateRow[];
}

const inputStyle: React.CSSProperties = {
  width: "100%",
  height: 38,
  border: `1px solid ${tokens.border}`,
  borderRadius: 6,
  padding: "0 10px",
  fontSize: 13,
  color: tokens.textPrimary,
  backgroundColor: tokens.card,
};

const btnPrimary: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  gap: 8,
  height: 38,
  border: "none",
  borderRadius: 6,
  padding: "0 14px",
  backgroundColor: tokens.brand,
  color: tokens.textOnBrand,
  fontSize: 13,
  fontWeight: 600,
};

const btnSecondary: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  gap: 8,
  height: 38,
  border: `1px solid ${tokens.border}`,
  borderRadius: 6,
  padding: "0 14px",
  backgroundColor: tokens.card,
  color: tokens.textSecondary,
  fontSize: 13,
  fontWeight: 600,
};

export function ExchangeRatesTab() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sarToYer, setSarToYer] = useState("");
  const [usdToYer, setUsdToYer] = useState("");
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.get<ExchangeRatesResponse>("/api/finance-v3/exchange-rates");
      const sar = data.rates.find((r) => r.currency === "SAR")?.rateToYer;
      const usd = data.rates.find((r) => r.currency === "USD")?.rateToYer;
      setSarToYer(sar ? String(sar) : "");
      setUsdToYer(usd ? String(usd) : "");
      setUpdatedAt(data.updatedAt);
    } catch {
      setError("تعذر تحميل أسعار الصرف");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const save = async () => {
    const sar = Number(sarToYer);
    const usd = Number(usdToYer);
    if (!Number.isFinite(sar) || sar <= 0 || !Number.isFinite(usd) || usd <= 0) {
      setError("أدخل أسعار صرف صحيحة أكبر من صفر");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await api.put("/api/finance-v3/exchange-rates", { sarToYer: sar, usdToYer: usd });
      await load();
    } catch {
      setError("تعذر حفظ أسعار الصرف");
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="p-5 space-y-4">
      <div className="rounded-lg border p-5" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <div className="flex items-start justify-between gap-4 mb-5">
          <div>
            <h2 className="text-base font-bold" style={{ color: tokens.textPrimary }}>أسعار الصرف</h2>
            <p className="text-xs mt-1" style={{ color: tokens.textSecondary }}>
              الريال اليمني هو العملة الأساسية. هذه الأسعار تستخدم عند دفع مريض بعملة تختلف عن عملة حسابه.
            </p>
          </div>
          <button onClick={() => void load()} style={btnSecondary} type="button">
            <RefreshCw className="w-4 h-4" /> تحديث
          </button>
        </div>

        {error && (
          <div className="mb-4 rounded-md border px-3 py-2 text-sm" style={{ borderColor: tokens.dangerBorder, color: tokens.dangerBorder, backgroundColor: tokens.dangerBg }}>
            {error}
          </div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold mb-1" style={{ color: tokens.textSecondary }}>
              1 ريال سعودي = كم ريال يمني؟
            </label>
            <input
              type="number"
              min="0.000001"
              step="0.000001"
              dir="ltr"
              value={sarToYer}
              onChange={(e) => setSarToYer(e.target.value)}
              style={inputStyle}
              placeholder="مثال: 140"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold mb-1" style={{ color: tokens.textSecondary }}>
              1 دولار = كم ريال يمني؟
            </label>
            <input
              type="number"
              min="0.000001"
              step="0.000001"
              dir="ltr"
              value={usdToYer}
              onChange={(e) => setUsdToYer(e.target.value)}
              style={inputStyle}
              placeholder="مثال: 530"
            />
          </div>
        </div>

        <div className="flex items-center justify-between mt-5">
          <span className="text-xs" style={{ color: tokens.textTertiary }}>
            {updatedAt ? `آخر قراءة: ${new Date(updatedAt).toLocaleString("ar-YE")}` : "لا توجد قراءة محفوظة بعد"}
          </span>
          <button onClick={() => void save()} disabled={saving} style={{ ...btnPrimary, opacity: saving ? 0.65 : 1 }} type="button">
            <Save className="w-4 h-4" /> {saving ? "جارٍ الحفظ..." : "حفظ أسعار الصرف"}
          </button>
        </div>
      </div>

      <div className="rounded-lg border p-5" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <EmptyState
          icon={TrendingUp}
          message="يمكن لاحقاً ربط مصدر موثوق أو مساعد AI يقترح السعر كل 6 ساعات، لكن الاعتماد النهائي يبقى بعد موافقة المدير أو المحاسب."
        />
      </div>
    </div>
  );
}
