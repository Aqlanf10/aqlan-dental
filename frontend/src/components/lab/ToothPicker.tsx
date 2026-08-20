"use client";

import { useMemo, useState } from "react";
import { Grid3x3, X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * LABINV-REQ-006 — FDI tooth selector for lab order items.
 *
 * What this replaces: a bare text box labelled "الأسنان" where the user typed
 * "11, 12, 21" by hand for every item. A transposed digit there is not a typo in a
 * comment field — it is a crown made for the wrong tooth, discovered at the try-in,
 * paid for twice.
 *
 * It writes the same free-text field it always did. That is deliberate: existing orders
 * carry values in whatever notation the clinic used, and a picker that could not
 * represent them would be a picker staff route around. Anything the parser does not
 * recognise is preserved verbatim and shown as an untouched extra token.
 */

/** FDI quadrants, written outward from the midline exactly as they are numbered. */
export const FDI_QUADRANTS = {
  upperRight: ["18", "17", "16", "15", "14", "13", "12", "11"],
  upperLeft: ["21", "22", "23", "24", "25", "26", "27", "28"],
  lowerRight: ["48", "47", "46", "45", "44", "43", "42", "41"],
  lowerLeft: ["31", "32", "33", "34", "35", "36", "37", "38"],
} as const;

const ALL_FDI = new Set<string>([
  ...FDI_QUADRANTS.upperRight,
  ...FDI_QUADRANTS.upperLeft,
  ...FDI_QUADRANTS.lowerRight,
  ...FDI_QUADRANTS.lowerLeft,
]);

/**
 * Splits a stored value into the FDI numbers the chart can show and the tokens it cannot.
 * Unrecognised tokens are never discarded — a value the picker does not understand is
 * still the clinic's data.
 */
export function parseToothValue(raw: string | undefined | null): {
  selected: string[];
  extras: string[];
} {
  const tokens = (raw ?? "")
    .split(/[,،\s/]+/)
    .map((t) => t.trim())
    .filter(Boolean);

  const selected: string[] = [];
  const extras: string[] = [];

  for (const token of tokens) {
    if (ALL_FDI.has(token)) {
      if (!selected.includes(token)) selected.push(token);
    } else {
      extras.push(token);
    }
  }
  return { selected, extras };
}

/** Renders the stored string back, FDI numbers first then anything preserved. */
export function formatToothValue(selected: string[], extras: string[]): string {
  return [...selected, ...extras].join(", ");
}

interface Props {
  value: string | undefined;
  onChange: (next: string) => void;
  /** Rendered on the trigger button for screen readers when several items are on screen. */
  label?: string;
}

export function ToothPicker({ value, onChange, label = "اختيار الأسنان" }: Props) {
  const [open, setOpen] = useState(false);
  const { selected, extras } = useMemo(() => parseToothValue(value), [value]);

  const toggle = (tooth: string) => {
    const next = selected.includes(tooth)
      ? selected.filter((t) => t !== tooth)
      : [...selected, tooth];
    onChange(formatToothValue(next, extras));
  };

  const clear = () => onChange(formatToothValue([], extras));

  return (
    <div className="space-y-1.5">
      <div className="flex items-center gap-2">
        <input
          className="flex-1 border border-gray-200 rounded-lg px-3 py-2 text-sm"
          placeholder="الأسنان"
          aria-label="رقم الأسنان"
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
            "shrink-0 rounded-lg border px-2.5 py-2 transition-colors",
            open
              ? "border-cyan-500 bg-cyan-50 text-cyan-700"
              : "border-gray-200 text-gray-500 hover:text-cyan-700 hover:border-cyan-300",
          )}
        >
          <Grid3x3 className="h-4 w-4" aria-hidden />
        </button>
      </div>

      {open && (
        <div className="rounded-xl border border-gray-200 bg-white p-3 space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-gray-600">
              ترقيم FDI — اضغط السن لتحديده
            </span>
            <div className="flex items-center gap-3">
              {selected.length > 0 && (
                <button
                  type="button"
                  onClick={clear}
                  className="text-xs text-gray-500 hover:text-red-600"
                >
                  مسح التحديد
                </button>
              )}
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="إغلاق مخطط الأسنان"
                className="text-gray-400 hover:text-gray-700"
              >
                <X className="h-4 w-4" aria-hidden />
              </button>
            </div>
          </div>

          {/* Laid out as the mouth is seen, not as the array is ordered: upper row above
              lower row, right quadrant on the right. The midline gap is what lets a
              clinician read "16" as a position rather than a number in a list. */}
          <div className="space-y-1.5" dir="ltr">
            <ToothRow
              left={FDI_QUADRANTS.upperRight}
              right={FDI_QUADRANTS.upperLeft}
              selected={selected}
              onToggle={toggle}
            />
            <div className="h-px bg-gray-200" />
            <ToothRow
              left={FDI_QUADRANTS.lowerRight}
              right={FDI_QUADRANTS.lowerLeft}
              selected={selected}
              onToggle={toggle}
            />
          </div>

          {extras.length > 0 && (
            <p className="text-xs text-amber-700">
              قيم محفوظة كما هي ولم يتعرّف عليها المخطط: {extras.join("، ")}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function ToothRow({
  left,
  right,
  selected,
  onToggle,
}: {
  left: readonly string[];
  right: readonly string[];
  selected: string[];
  onToggle: (tooth: string) => void;
}) {
  return (
    <div className="flex items-center justify-center gap-2 overflow-x-auto">
      <div className="flex gap-1">
        {left.map((t) => (
          <ToothButton key={t} tooth={t} active={selected.includes(t)} onToggle={onToggle} />
        ))}
      </div>
      <div className="w-px self-stretch bg-gray-300" aria-hidden />
      <div className="flex gap-1">
        {right.map((t) => (
          <ToothButton key={t} tooth={t} active={selected.includes(t)} onToggle={onToggle} />
        ))}
      </div>
    </div>
  );
}

function ToothButton({
  tooth,
  active,
  onToggle,
}: {
  tooth: string;
  active: boolean;
  onToggle: (tooth: string) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onToggle(tooth)}
      aria-pressed={active}
      aria-label={`السن ${tooth}`}
      className={cn(
        "h-8 w-8 shrink-0 rounded-md border text-xs font-semibold tabular-nums transition-colors",
        active
          ? "border-cyan-600 bg-cyan-600 text-white"
          : "border-gray-200 bg-white text-gray-700 hover:border-cyan-400 hover:text-cyan-700",
      )}
    >
      {tooth}
    </button>
  );
}
