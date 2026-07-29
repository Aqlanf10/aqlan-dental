
import { useState } from "react";
import { MoveHorizontal } from "lucide-react";

interface Props {
  baseImageUrl: string;
  targetImageUrl: string;
  baseDate: string;
  targetDate: string;
}

export function PhotoLongitudinalImageCompare({ baseImageUrl, targetImageUrl, baseDate, targetDate }: Props) {
  const [position, setPosition] = useState(50);

  return (
    <section className="border border-gray-200 bg-white p-3">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-xs">
        <span className="font-bold text-blue-700">قبل: {baseDate}</span>
        <span className="font-bold text-emerald-700">بعد: {targetDate}</span>
      </div>
      <div className="relative mx-auto aspect-[4/5] max-h-[64vh] overflow-hidden bg-gray-950">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={targetImageUrl} alt={`الصورة الأحدث بتاريخ ${targetDate}`} className="absolute inset-0 h-full w-full object-contain" />
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={baseImageUrl}
          alt={`الصورة السابقة بتاريخ ${baseDate}`}
          className="absolute inset-0 h-full w-full object-contain"
          style={{ clipPath: `inset(0 0 0 ${position}%)` }}
        />
        <div className="pointer-events-none absolute inset-y-0 w-0.5 bg-white shadow" style={{ left: `${position}%` }} />
        <span className="absolute start-2 top-2 bg-blue-700 px-2 py-1 text-[10px] font-bold text-white">قبل</span>
        <span className="absolute end-2 top-2 bg-emerald-700 px-2 py-1 text-[10px] font-bold text-white">بعد</span>
      </div>
      <label className="mt-3 flex items-center gap-2 text-xs text-gray-600">
        <MoveHorizontal className="h-4 w-4 shrink-0" />
        <span className="sr-only">موضع فاصل المقارنة</span>
        <input
          type="range"
          min={0}
          max={100}
          value={position}
          onChange={event => setPosition(Number(event.target.value))}
          aria-label="موضع فاصل المقارنة قبل وبعد"
          className="w-full accent-clinic-blue"
        />
      </label>
    </section>
  );
}
