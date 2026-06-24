"use client";

import { useEffect, useMemo, useState } from "react";
import { ArrowLeftRight } from "lucide-react";
import { useOrthoPhotos } from "@/hooks/useOrtho";
import { formatArabicDate } from "@/lib/utils";
import type { OrthoPhoto } from "@/types/ortho";
import { TREATMENT_PHASE_LABELS, orthoSubtypeLabel } from "@/types/ortho";

const PHOTO_TYPE_LABELS: Record<string, string> = {
  Intraoral: "داخل الفم",
  Extraoral: "خارج الفم",
  Progress: "متابعة",
  Radiograph: "أشعة",
};

const inputCls =
  "w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue";

function resolveImageUrl(url: string) {
  if (url.startsWith("http") || url.startsWith("data:")) return url;
  // NAV-CEPH-FIX (Part 2): relative path → Next.js rewrite proxies /uploads/* same-origin.
  return url;
}

function photoLabel(photo: OrthoPhoto) {
  const type = PHOTO_TYPE_LABELS[photo.photoType] ?? photo.photoType;
  const parts = [type];
  // مرحلة العلاج (قبل/أثناء/بعد) والنوع الفرعي عند توفر الوسوم
  const phase = photo.treatmentPhase
    ? TREATMENT_PHASE_LABELS[photo.treatmentPhase]
    : undefined;
  if (phase) parts.push(phase);
  const subtype = orthoSubtypeLabel(photo.subtype);
  if (subtype) parts.push(subtype);
  if (photo.caption) parts.push(photo.caption);
  if (photo.takenAt) parts.push(formatArabicDate(photo.takenAt));
  return parts.join(" — ");
}

function PhotoSlot({
  title,
  photos,
  selectedId,
  onSelect,
}: {
  title: string;
  photos: OrthoPhoto[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  const selected = photos.find((p) => p.id === selectedId) ?? null;

  return (
    <div className="flex flex-1 flex-col gap-3 rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="rounded-full bg-clinic-blue-50 px-3 py-1 text-sm font-semibold text-clinic-blue">
          {title}
        </span>
        {selected?.takenAt && (
          <span className="text-xs text-gray-400">
            {formatArabicDate(selected.takenAt)}
          </span>
        )}
      </div>
      <select
        className={inputCls}
        value={selectedId ?? ""}
        onChange={(e) => onSelect(e.target.value)}
        aria-label={`اختيار صورة ${title}`}
      >
        {photos.map((p) => (
          <option key={p.id} value={p.id}>
            {photoLabel(p)}
          </option>
        ))}
      </select>
      <div className="flex h-72 items-center justify-center overflow-hidden rounded-lg border border-gray-100 bg-gray-50">
        {selected ? (
          /* eslint-disable-next-line @next/next/no-img-element */
          <img
            src={resolveImageUrl(selected.photoUrl)}
            alt={`${title}: ${photoLabel(selected)}`}
            className="h-full w-full object-contain"
          />
        ) : (
          <span className="text-sm text-gray-400">لم يتم اختيار صورة</span>
        )}
      </div>
    </div>
  );
}

export function OrthoBeforeAfterCompare({ caseId }: { caseId: string }) {
  const { data: photos = [] as OrthoPhoto[], isLoading } =
    useOrthoPhotos(caseId);

  // Sort oldest -> newest (takenAt first, fallback to sortOrder)
  const sorted = useMemo(() => {
    return [...photos].sort((a, b) => {
      if (a.takenAt && b.takenAt)
        return new Date(a.takenAt).getTime() - new Date(b.takenAt).getTime();
      if (a.takenAt) return -1;
      if (b.takenAt) return 1;
      return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
    });
  }, [photos]);

  const [beforeId, setBeforeId] = useState<string | null>(null);
  const [afterId, setAfterId] = useState<string | null>(null);

  // Sensible defaults — phase-aware when photos carry treatmentPhase tags:
  // «قبل» = أول صورة موسومة Initial (وإلا الأقدم)، «بعد» = آخر صورة موسومة Final (وإلا الأحدث)
  useEffect(() => {
    if (sorted.length < 2) return;
    const ids = new Set(sorted.map((p) => p.id));
    if (!beforeId || !ids.has(beforeId)) {
      const firstInitial = sorted.find((p) => p.treatmentPhase === "Initial");
      setBeforeId((firstInitial ?? sorted[0]).id);
    }
    if (!afterId || !ids.has(afterId)) {
      const finals = sorted.filter((p) => p.treatmentPhase === "Final");
      const lastFinal = finals.length > 0 ? finals[finals.length - 1] : undefined;
      setAfterId((lastFinal ?? sorted[sorted.length - 1]).id);
    }
  }, [sorted, beforeId, afterId]);

  const swap = () => {
    setBeforeId(afterId);
    setAfterId(beforeId);
  };

  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-2 animate-pulse">
        <div className="h-96 rounded-lg bg-gray-100" />
        <div className="h-96 rounded-lg bg-gray-100" />
      </div>
    );
  }

  if (sorted.length < 2) {
    return (
      <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 py-10 text-center text-sm text-gray-400">
        أضف صورتين على الأقل لاستخدام المقارنة
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-semibold text-gray-900">مقارنة قبل/بعد</h2>
        <button
          type="button"
          onClick={swap}
          className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
        >
          <ArrowLeftRight className="h-4 w-4" />
          تبديل
        </button>
      </div>
      <div className="flex flex-col gap-4 md:flex-row">
        <PhotoSlot
          title="قبل"
          photos={sorted}
          selectedId={beforeId}
          onSelect={setBeforeId}
        />
        <PhotoSlot
          title="بعد"
          photos={sorted}
          selectedId={afterId}
          onSelect={setAfterId}
        />
      </div>
    </div>
  );
}

export default OrthoBeforeAfterCompare;
