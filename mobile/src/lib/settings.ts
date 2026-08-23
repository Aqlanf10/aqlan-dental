export type ExchangeRatesView = {
  market: string;
  marketLabel: string;
  baseCurrency: string;
  currencies: string[];
  ratesToYer: Record<string, number>;
  updatedOn?: string | null;
  ageInDays: number;
  staleAfterDays: number;
  isStale: boolean;
  markets: Array<{ key: string; label: string }>;
};

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? value as UnknownRecord : null;
}

function property(source: UnknownRecord, camel: string, pascal: string): unknown {
  return source[camel] ?? source[pascal];
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function finiteNumber(value: unknown, fallback = 0): number {
  const number = typeof value === "number" ? value : Number(value);
  return Number.isFinite(number) ? number : fallback;
}

export function normalizeExchangeRates(value: unknown): ExchangeRatesView | null {
  const source = record(value);
  if (!source) return null;
  const rawRates = record(property(source, "ratesToYer", "RatesToYer"));
  const ratesToYer: Record<string, number> = {};
  for (const [currency, rawValue] of Object.entries(rawRates ?? {})) {
    const rate = finiteNumber(rawValue, Number.NaN);
    if (currency.trim() && Number.isFinite(rate) && rate >= 0) ratesToYer[currency.trim()] = rate;
  }
  const rawCurrencies = property(source, "currencies", "Currencies");
  const rawMarkets = property(source, "markets", "Markets");
  const markets = Array.isArray(rawMarkets)
    ? rawMarkets.flatMap((value) => {
        const item = record(value);
        if (!item) return [];
        const key = text(property(item, "key", "Key"));
        if (!key) return [];
        return [{ key, label: text(property(item, "label", "Label")) || key }];
      })
    : [];
  return {
    market: text(property(source, "market", "Market")) || "default",
    marketLabel: text(property(source, "marketLabel", "MarketLabel")) || "السوق الافتراضي",
    baseCurrency: text(property(source, "baseCurrency", "BaseCurrency")) || "YER",
    currencies: Array.isArray(rawCurrencies) ? rawCurrencies.map(text).filter(Boolean) : Object.keys(ratesToYer),
    ratesToYer,
    updatedOn: text(property(source, "updatedOn", "UpdatedOn")) || null,
    ageInDays: Math.max(0, finiteNumber(property(source, "ageInDays", "AgeInDays"))),
    staleAfterDays: Math.max(0, finiteNumber(property(source, "staleAfterDays", "StaleAfterDays"))),
    isStale: property(source, "isStale", "IsStale") === true,
    markets
  };
}

export function normalizeStringRecord(value: unknown, nullable = false): Record<string, string | null> | null {
  const source = record(value);
  if (!source) return null;
  const result: Record<string, string | null> = {};
  for (const [key, rawValue] of Object.entries(source)) {
    if (typeof rawValue === "string") result[key] = rawValue;
    else if (nullable && rawValue === null) result[key] = null;
  }
  return result;
}
