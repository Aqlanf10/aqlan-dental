"use client";

import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, RefreshCw } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";

/**
 * LABINV-REQ-010 — the exchange rate on a lab order.
 *
 * What this replaces: a bare number input labelled "سعر الصرف الفعلي: 1 SAR = كم YER؟".
 * Nothing checked it, nothing remembered it, and nothing compared it to what the previous
 * person typed. That number multiplies into the order cost, the supplier bill, and the
 * lab-cost deduction inside the doctor's commission — so two staff disagreeing by a
 * hundred rial produced two different commissions for identical work.
 *
 * What it does now: offers the clinic-configured market rate, states which market it came
 * from and how old it is, and still allows a manual override — because a rate the user
 * cannot correct is a rate they will work around.
 */

export interface ExchangeRateSnapshot {
  market: string;
  marketLabel: string;
  baseCurrency: string;
  currencies: string[];
  ratesToYer: Record<string, number>;
  updatedOn: string | null;
  ageInDays: number | null;
  staleAfterDays: number;
  isStale: boolean;
  markets: { key: string; label: string }[];
}

export function useExchangeRates() {
  return useQuery({
    queryKey: ["exchange-rates"],
    queryFn: async () => {
      const { data } = await api.get<ExchangeRateSnapshot>("/api/settings/exchange-rates");
      return data;
    },
    // Rates change on the owner's schedule, not per render. Refetching on every modal
    // open would hammer the endpoint without ever producing a different number.
    staleTime: 5 * 60 * 1000,
  });
}

interface Props {
  currency: string;
  value: number | undefined;
  onChange: (rate: number | undefined) => void;
}

export function ExchangeRateField({ currency, value, onChange }: Props) {
  const { data, isLoading, isError, error, refetch, isFetching } = useExchangeRates();

  const suggested = data?.ratesToYer?.[currency];

  // Prefill once per currency, and only while the field is untouched. Overwriting a rate
  // the user deliberately typed would be exactly the silent substitution this feature
  // exists to remove.
  const prefilledFor = useRef<string | null>(null);
  useEffect(() => {
    if (prefilledFor.current === currency) return;
    if (value !== undefined) {
      prefilledFor.current = currency;
      return;
    }
    if (suggested && suggested > 0) {
      onChange(suggested);
      prefilledFor.current = currency;
    }
    // `onChange` and `value` are intentionally excluded: this must run on currency change
    // and on first arrival of the rate, not on every keystroke in the field.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currency, suggested]);

  const isManual = suggested !== undefined && value !== undefined && value !== suggested;

  return (
    <div className="space-y-1.5">
      <label className="text-sm font-medium text-gray-700" htmlFor="lab-exchange-rate">
        سعر الصرف: 1 {currency} = كم {data?.baseCurrency ?? "YER"}؟
      </label>

      <div className="flex items-center gap-2">
        <input
          id="lab-exchange-rate"
          type="number"
          min="0.000001"
          step="0.000001"
          value={value ?? ""}
          onChange={(event) =>
            onChange(event.target.value === "" ? undefined : Number(event.target.value))
          }
          className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
          dir="ltr"
        />

        {suggested !== undefined && suggested > 0 && isManual && (
          <button
            type="button"
            onClick={() => onChange(suggested)}
            className="shrink-0 text-xs font-medium text-cyan-700 hover:text-cyan-900 whitespace-nowrap"
          >
            استعادة سعر السوق
          </button>
        )}
      </div>

      {isLoading && <p className="text-xs text-gray-500">جاري جلب سعر الصرف المعتمد…</p>}

      {/* A failed lookup is stated, not papered over. The user may still type a rate —
          but they are told the system could not offer one, rather than being handed a
          silent default they would reasonably assume was verified. */}
      {isError && (
        <div className="flex items-start gap-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" aria-hidden />
          <div className="space-y-1">
            <p className="text-xs text-amber-900">
              تعذر جلب سعر الصرف المعتمد: {extractErrorMessage(error)} — أدخل السعر يدويًا.
            </p>
            <button
              type="button"
              onClick={() => void refetch()}
              disabled={isFetching}
              className="inline-flex items-center gap-1 text-xs font-medium text-amber-800 hover:text-amber-950 disabled:opacity-50"
            >
              <RefreshCw className={`h-3 w-3 ${isFetching ? "animate-spin" : ""}`} aria-hidden />
              إعادة المحاولة
            </button>
          </div>
        </div>
      )}

      {data && !isError && (
        <p className="text-xs text-gray-500">
          {suggested && suggested > 0 ? (
            <>
              السعر المعتمد ({data.marketLabel}): {suggested.toLocaleString()} {data.baseCurrency}
              {" · "}
              {data.updatedOn
                ? `آخر مراجعة ${data.updatedOn}`
                : "لم تُراجَع أسعار الصرف بعد"}
            </>
          ) : (
            <>لا يوجد سعر معتمد لعملة {currency} — أدخله يدويًا.</>
          )}
        </p>
      )}

      {/* Staleness is a warning, not a block. Refusing to show an old rate would just push
          the user back to typing an unverifiable number from memory. */}
      {data && !isError && data.isStale && (
        <div className="flex items-start gap-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" aria-hidden />
          <p className="text-xs text-amber-900">
            {data.updatedOn
              ? `أسعار الصرف لم تُراجَع منذ ${data.ageInDays} يومًا (الحد ${data.staleAfterDays}). راجعها من الإعدادات قبل الاعتماد عليها.`
              : "أسعار الصرف لم تُراجَع بعد منذ تركيب النظام. راجعها من الإعدادات قبل الاعتماد عليها."}
          </p>
        </div>
      )}

      {isManual && (
        <p className="text-xs text-cyan-700">
          سعر مخصص لهذا الطلب — يختلف عن سعر {data?.marketLabel ?? "السوق المعتمد"}.
        </p>
      )}
    </div>
  );
}
