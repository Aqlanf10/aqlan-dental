"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Palette, X } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

/**
 * LABINV-REQ-007 — shade picker for lab order items.
 *
 * What this replaces: a text box labelled "الظل". "A2", "a2" and "A 2" were three
 * different values in the database, so no report could group by shade and no mismatch
 * between what was ordered and what arrived could be detected. A remake for a wrong
 * shade is a cost the clinic pays, not the lab.
 *
 * Like the tooth picker, it writes the same free-text column and accepts anything typed.
 * The guide itself comes from Settings so a clinic using a different system is not
 * forced onto VITA Classical by a constant in the source.
 */

/** VITA Classical, the default when the clinic has not configured its own guide. */
const DEFAULT_SHADE_GUIDE = [
  "A1", "A2", "A3", "A3.5", "A4",
  "B1", "B2", "B3", "B4",
  "C1", "C2", "C3", "C4",
  "D2", "D3", "D4",
];

/**
 * Approximate swatch colours. Deliberately not the selling point: a browser on an
 * uncalibrated screen cannot be used to choose a real shade, so these read as
 * identification aids next to the code, never as a substitute for the shade guide in
 * the operatory.
 */
const SWATCHES: Record<string, string> = {
  A1: "#EDE6D6", A2: "#E5DAC4", A3: "#DCCDB0", "A3.5": "#D2C09E", A4: "#C6B189",
  B1: "#F0EBDD", B2: "#E7DEC8", B3: "#DBCDAC", B4: "#CFBF9A",
  C1: "#E2DDD2", C2: "#D5CDBC", C3: "#C7BDA8", C4: "#B6AA92",
  D2: "#E4DCCD", D3: "#D8CEBB", D4: "#CBBFA8",
};

export function useShadeGuide() {
  return useQuery({
    queryKey: ["lab-shade-guide"],
    queryFn: async () => {
      const { data } = await api.get<{ shades: string[] }>("/api/settings/lab-shade-guide");
      const shades = data?.shades ?? [];
      return shades.length > 0 ? shades : DEFAULT_SHADE_GUIDE;
    },
    staleTime: 10 * 60 * 1000,
    // A missing or unreadable guide must not block ordering — fall back to the standard
    // one rather than leaving the clinician without a picker.
    retry: false,
    placeholderData: DEFAULT_SHADE_GUIDE,
  });
}

interface Props {
  value: string | undefined;
  onChange: (next: string) => void;
  label?: string;
}

export function ShadePicker({ value, onChange, label = "اختيار درجة اللون" }: Props) {
  const [open, setOpen] = useState(false);
  const { data: guide = DEFAULT_SHADE_GUIDE } = useShadeGuide();

  const current = (value ?? "").trim();

  // Case-insensitive so a stored "a2" still lights up A2 instead of looking unset.
  const matched = useMemo(
    () => guide.find((s) => s.toLowerCase() === current.toLowerCase()) ?? null,
    [guide, current],
  );

  const isUnrecognised = current.length > 0 && matched === null;

  return (
    // Same anchoring as ToothPicker, and for the same reason: this sits in a narrow grid
    // cell, so the guide floats above the row instead of competing with it for width.
    <div className="relative">
      <input
        className="w-full border border-gray-200 rounded-lg px-3 py-2 ps-9 text-sm"
        placeholder="الظل"
        aria-label="الظل (اللون)"
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value)}
        dir="ltr"
      />
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        aria-label={label}
        title={label}
        className={cn(
          "absolute top-1/2 -translate-y-1/2 rounded-md p-1.5 transition-colors",
          open ? "bg-cyan-50 text-cyan-700" : "text-gray-400 hover:text-cyan-700",
        )}
        style={{ insetInlineStart: "0.25rem" }}
      >
        <Palette className="h-4 w-4" aria-hidden />
      </button>

      {open && (
        <div
          className="absolute z-30 mt-1 w-max max-w-[min(88vw,26rem)] rounded-xl border border-gray-200 bg-white p-3 space-y-2 shadow-lg"
          style={{ insetInlineStart: 0 }}
        >
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-gray-600">دليل درجات اللون</span>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label="إغلاق دليل الألوان"
              className="text-gray-400 hover:text-gray-700"
            >
              <X className="h-4 w-4" aria-hidden />
            </button>
          </div>

          <div className="flex flex-wrap gap-1.5" dir="ltr">
            {guide.map((shade) => {
              const active = matched === shade;
              return (
                <button
                  key={shade}
                  type="button"
                  onClick={() => {
                    onChange(shade);
                    setOpen(false);
                  }}
                  aria-pressed={active}
                  aria-label={`درجة اللون ${shade}`}
                  className={cn(
                    "flex items-center gap-1.5 rounded-md border px-2 py-1.5 text-xs font-semibold transition-colors",
                    active
                      ? "border-cyan-600 ring-2 ring-cyan-200 text-cyan-900"
                      : "border-gray-200 text-gray-700 hover:border-cyan-400",
                  )}
                >
                  <span
                    className="h-4 w-4 rounded-sm border border-black/10"
                    style={{ backgroundColor: SWATCHES[shade] ?? "#E5E7EB" }}
                    aria-hidden
                  />
                  {shade}
                </button>
              );
            })}
          </div>

          <p className="text-[11px] text-gray-500">
            الألوان المعروضة للتعرّف على الرمز فقط — اعتمد دليل الألوان الفعلي في العيادة.
          </p>
        </div>
      )}

      {/* Stated, not corrected. A shade outside the guide may be a legitimate custom
          instruction to the lab; silently normalising it would change the order. */}
      {isUnrecognised && (
        <p className="text-xs text-amber-700">
          «{current}» ليست ضمن دليل الألوان المعتمد — ستُرسل كما هي.
        </p>
      )}
    </div>
  );
}
