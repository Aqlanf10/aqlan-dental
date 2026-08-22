"use client";

import { useCallback, useRef, useState } from "react";
import { MoveHorizontal } from "lucide-react";

// ─── CephImageSliderCompare ─────────────────────────────────────────────────
//
// CEPH-EPIC batch C-B — before/after radiograph wipe slider.
//
// Renders two X-ray images stacked: the AFTER (target) image as the bottom
// layer and the BEFORE (base) image as the top layer, clipped to the right
// portion of the container. A draggable vertical handle divides them.
//
// RTL behaviour (matches Arabic reading order):
//   • "قبل" (before, base date) label is anchored to the RIGHT edge.
//   • "بعد" (after, target date) label is anchored to the LEFT edge.
//   • Dragging the handle to the RIGHT grows the BEFORE (right) portion →
//     "drag right = reveal before" per CEPH-EPIC.
//   • Dragging the handle to the LEFT grows the AFTER (left) portion.
//
// Image-loading rules (per CEPH-EPIC, mirroring CephCanvas):
//   • `crossOrigin` is NEVER set on the <img>. The slider only displays the
//     radiographs (no canvas pixel reads), so a tainted state is harmless,
//     and asking for CORS against an uploads host that sends no CORS headers
//     would make the image silently fail to load.
//   • `draggable={false}` + `pointer-events-none` on the images so the drag
//     gestures are captured by the container, not the browser image ghost.
//
// Touch support: Pointer Events API (unified mouse + touch + pen). The
// container has `touch-none` so vertical page scroll doesn't hijack the
// horizontal drag on mobile.

interface Props {
  baseImageUrl: string;
  targetImageUrl: string;
  baseDate: string;
  targetDate: string;
}

const MIN_PCT = 0;
const MAX_PCT = 100;
const DEFAULT_PCT = 50;

export function CephImageSliderCompare({
  baseImageUrl,
  targetImageUrl,
  baseDate,
  targetDate,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const draggingRef = useRef(false);
  const [position, setPosition] = useState(DEFAULT_PCT);
  const [baseError, setBaseError] = useState(false);
  const [targetError, setTargetError] = useState(false);

  // Hooks must run unconditionally on every render (rules-of-hooks), so the
  // drag-position updater is declared BEFORE the missing-image early return.
  const updateFromClientX = useCallback((clientX: number) => {
    const el = containerRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    if (rect.width <= 0) return;
    const pct = ((clientX - rect.left) / rect.width) * 100;
    setPosition(Math.max(MIN_PCT, Math.min(MAX_PCT, pct)));
  }, []);

  // Missing-image guard — friendly Arabic message instead of a broken slider.
  if (!baseImageUrl || !targetImageUrl || baseError || targetError) {
    return (
      <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-8 text-center text-sm text-gray-500">
        صورة الأشعة غير متوفرة لهذا التحليل — التمرير قبل/بعد يتطلب رفع صورة الأشعة في كلا التحليلين.
      </div>
    );
  }

  const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    draggingRef.current = true;
    try {
      (e.currentTarget as Element).setPointerCapture(e.pointerId);
    } catch {
      // setPointerCapture can throw if the pointer was already released —
      // safe to ignore, the move/up handlers still work via window events.
    }
    updateFromClientX(e.clientX);
  };

  const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!draggingRef.current) return;
    updateFromClientX(e.clientX);
  };

  const endDrag = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!draggingRef.current) return;
    draggingRef.current = false;
    try {
      (e.currentTarget as Element).releasePointerCapture(e.pointerId);
    } catch {
      // same defensive guard as setPointerCapture
    }
  };

  // `position` is the handle's distance from the LEFT edge (0–100).
  // The BEFORE image (top layer) is clipped to show only the RIGHT portion
  // (from `position`% to 100%), so dragging the handle right grows the
  // before portion. The AFTER image (bottom layer) is the full container.
  const beforeClipLeft = MAX_PCT - position;

  return (
    <div>
      <div className="mb-2 flex flex-wrap items-center gap-3 text-xs">
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full bg-blue-600" />
          <span className="font-medium text-blue-700">قبل — {baseDate}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full bg-emerald-600" />
          <span className="font-medium text-emerald-700">بعد — {targetDate}</span>
        </span>
        <span className="text-gray-400 ms-auto flex items-center gap-1">
          <MoveHorizontal className="w-3.5 h-3.5" />
          اسحب المقبض يمينًا لكشف صورة «قبل» ويسارًا لكشف «بعد»
        </span>
      </div>

      <div
        ref={containerRef}
        className="relative w-full select-none overflow-hidden rounded-lg border border-gray-200 bg-black touch-none"
        style={{ aspectRatio: "4 / 5", maxHeight: "70vh" }}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        role="slider"
        aria-label="مقارنة صور الأشعة قبل وبعد — اسحب المقبض"
        aria-valuemin={MIN_PCT}
        aria-valuemax={MAX_PCT}
        aria-valuenow={Math.round(position)}
      >
        {/* AFTER (target) image — bottom layer, full container */}
        <img
          src={targetImageUrl}
          alt={`بعد — ${targetDate}`}
          draggable={false}
          onError={() => setTargetError(true)}
          className="pointer-events-none absolute inset-0 h-full w-full object-contain"
        />

        {/* BEFORE (base) image — top layer, clipped to the right portion */}
        <img
          src={baseImageUrl}
          alt={`قبل — ${baseDate}`}
          draggable={false}
          onError={() => setBaseError(true)}
          className="pointer-events-none absolute inset-0 h-full w-full object-contain"
          style={{ clipPath: `inset(0 0 0 ${beforeClipLeft}%)` }}
        />

        {/* Slider handle (vertical line + circular grip) */}
        <div
          className="pointer-events-none absolute top-0 bottom-0 w-0.5 bg-white shadow-[0_0_6px_rgba(0,0,0,0.6)]"
          style={{ left: `${position}%`, transform: "translateX(-0.5px)" }}
        >
          <div className="absolute top-1/2 left-1/2 flex h-9 w-9 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border-2 border-clinic-blue bg-white text-clinic-blue shadow-md">
            <MoveHorizontal className="h-4 w-4" />
          </div>
        </div>

        {/* Labels — BEFORE on the right (Arabic natural), AFTER on the left */}
        <span className="pointer-events-none absolute top-2 right-2 rounded-md bg-blue-600/85 px-2 py-0.5 text-[11px] font-bold text-white shadow-sm">
          قبل
        </span>
        <span className="pointer-events-none absolute top-2 left-2 rounded-md bg-emerald-600/85 px-2 py-0.5 text-[11px] font-bold text-white shadow-sm">
          بعد
        </span>
      </div>
    </div>
  );
}
