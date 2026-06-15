"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Check,
  Crop,
  FlipHorizontal2,
  FlipVertical2,
  Loader2,
  RotateCcw,
  Save,
  X,
} from "lucide-react";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { cn } from "@/lib/utils";
import { orthoService } from "@/services/orthoService";
import { toast } from "@/stores/toastStore";
import type {
  OrthoImagePreparation,
  OrthoImagePreparationStatus,
  OrthoPhoto,
  SaveOrthoImagePreparationRequest,
} from "@/types/ortho";

interface Props {
  caseId: string;
  photo: OrthoPhoto | null;
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}

interface Preset {
  key: string;
  label: string;
  aspectRatio: string;
  cropWidth: number;
  cropHeight: number;
}

const DEFAULT_VALUE: OrthoImagePreparation = {
  photoId: "",
  originalPhotoUrl: "",
  cropX: 0,
  cropY: 0,
  cropWidth: 1,
  cropHeight: 1,
  zoom: 1,
  rotationDegrees: 0,
  brightness: 0,
  contrast: 0,
  flipHorizontal: false,
  flipVertical: false,
  aspectRatio: "Original",
  status: "OriginalUploaded",
};

const STATUS_OPTIONS: {
  value: Exclude<OrthoImagePreparationStatus, "OriginalUploaded">;
  label: string;
}[] = [
  { value: "PreparedForReport", label: "مجهزة للتقرير" },
  { value: "SelectedForPresentation", label: "مختارة للعرض" },
  { value: "ApprovedForPresentation", label: "معتمدة للعرض" },
];

const ASPECT_RATIO_VALUE: Record<string, string> = {
  Original: "4 / 3",
  "4:5": "4 / 5",
  "3:4": "3 / 4",
  "16:9": "16 / 9",
  "2:1": "2 / 1",
  "4:3": "4 / 3",
  "1:1": "1 / 1",
};

function presetsFor(photo: OrthoPhoto): Preset[] {
  const subtype = photo.subtype?.toLowerCase() ?? "";
  if (subtype.includes("smile")) {
    return [
      { key: "SmileWide", label: "ابتسامة عريضة", aspectRatio: "2:1", cropWidth: 1, cropHeight: 0.55 },
      { key: "SmileStandard", label: "ابتسامة قياسية", aspectRatio: "16:9", cropWidth: 1, cropHeight: 0.68 },
    ];
  }
  if (subtype.includes("profile")) {
    return [
      { key: "ExtraoralProfile", label: "بروفايل 4:5", aspectRatio: "4:5", cropWidth: 0.8, cropHeight: 1 },
      { key: "ExtraoralProfileTall", label: "بروفايل 3:4", aspectRatio: "3:4", cropWidth: 0.75, cropHeight: 1 },
    ];
  }
  if (subtype.includes("occlusal")) {
    return [
      { key: "Occlusal43", label: "إطباقية 4:3", aspectRatio: "4:3", cropWidth: 1, cropHeight: 0.75 },
      { key: "OcclusalSquare", label: "إطباقية مربعة", aspectRatio: "1:1", cropWidth: 0.86, cropHeight: 0.86 },
    ];
  }
  if (subtype.includes("opg")) {
    return [
      { key: "OpgWide", label: "بانوراما 2:1", aspectRatio: "2:1", cropWidth: 1, cropHeight: 0.52 },
      { key: "OpgStandard", label: "بانوراما 16:9", aspectRatio: "16:9", cropWidth: 1, cropHeight: 0.64 },
    ];
  }
  if (subtype.includes("ceph")) {
    return [
      { key: "LateralCeph45", label: "سيفالو 4:5", aspectRatio: "4:5", cropWidth: 0.8, cropHeight: 1 },
      { key: "LateralCeph34", label: "سيفالو 3:4", aspectRatio: "3:4", cropWidth: 0.75, cropHeight: 1 },
    ];
  }
  if (photo.category === "Intraoral") {
    return [
      { key: "Intraoral43", label: "داخل الفم 4:3", aspectRatio: "4:3", cropWidth: 1, cropHeight: 0.75 },
      { key: "IntraoralWide", label: "داخل الفم 16:9", aspectRatio: "16:9", cropWidth: 1, cropHeight: 0.64 },
    ];
  }
  return [
    { key: "Extraoral45", label: "وجه 4:5", aspectRatio: "4:5", cropWidth: 0.8, cropHeight: 1 },
    { key: "Extraoral34", label: "وجه 3:4", aspectRatio: "3:4", cropWidth: 0.75, cropHeight: 1 },
  ];
}

function NumberSlider({
  label,
  value,
  min,
  max,
  step = 1,
  unit,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  unit?: string;
  onChange: (value: number) => void;
}) {
  return (
    <label className="grid gap-1.5 text-xs text-gray-600">
      <span className="flex items-center justify-between">
        <span>{label}</span>
        <span className="font-semibold text-clinic-navy">
          {Number.isInteger(step) ? value : value.toFixed(2)}
          {unit}
        </span>
      </span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="h-1.5 w-full cursor-pointer accent-clinic-blue"
      />
    </label>
  );
}

export function OrthoImagePreparationDialog({
  caseId,
  photo,
  open,
  onClose,
  onSaved,
}: Props) {
  const [value, setValue] = useState<OrthoImagePreparation>(DEFAULT_VALUE);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const presets = useMemo(() => (photo ? presetsFor(photo) : []), [photo]);

  useEffect(() => {
    if (!open || !photo) return;
    let active = true;
    setLoading(true);
    orthoService
      .getImagePreparation(caseId, photo.id)
      .then(({ data }) => {
        if (active) setValue(data);
      })
      .catch(() => toast.error("تعذر تحميل إعدادات تجهيز الصورة"))
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [caseId, open, photo]);

  if (!open || !photo) return null;

  const update = (patch: Partial<OrthoImagePreparation>) =>
    setValue((current) => ({ ...current, ...patch }));

  const applyPreset = (preset: Preset) => {
    update({
      preset: preset.key,
      aspectRatio: preset.aspectRatio,
      cropX: Math.max(0, (1 - preset.cropWidth) / 2),
      cropY: Math.max(0, (1 - preset.cropHeight) / 2),
      cropWidth: preset.cropWidth,
      cropHeight: preset.cropHeight,
    });
  };

  const save = async () => {
    setSaving(true);
    try {
      const payload: SaveOrthoImagePreparationRequest = {
        cropX: value.cropX,
        cropY: value.cropY,
        cropWidth: value.cropWidth,
        cropHeight: value.cropHeight,
        zoom: value.zoom,
        rotationDegrees: value.rotationDegrees,
        brightness: value.brightness,
        contrast: value.contrast,
        flipHorizontal: value.flipHorizontal,
        flipVertical: value.flipVertical,
        aspectRatio: value.aspectRatio,
        preset: value.preset,
        status: value.status === "OriginalUploaded" ? "PreparedForReport" : value.status,
      };
      await orthoService.saveImagePreparation(caseId, photo.id, payload);
      toast.success("تم حفظ تجهيز الصورة");
      onSaved();
      onClose();
    } catch {
      toast.error("تعذر حفظ تجهيز الصورة");
    } finally {
      setSaving(false);
    }
  };

  const reset = async () => {
    setSaving(true);
    try {
      const { data } = await orthoService.resetImagePreparation(caseId, photo.id);
      setValue(data);
      toast.success("تمت استعادة الصورة الأصلية");
      onSaved();
    } catch {
      toast.error("تعذر استعادة الصورة الأصلية");
    } finally {
      setSaving(false);
    }
  };

  const cropCenterX = (value.cropX + value.cropWidth / 2) * 100;
  const cropCenterY = (value.cropY + value.cropHeight / 2) * 100;
  const imageTransform = [
    `scale(${value.zoom})`,
    `rotate(${value.rotationDegrees}deg)`,
    `scaleX(${value.flipHorizontal ? -1 : 1})`,
    `scaleY(${value.flipVertical ? -1 : 1})`,
  ].join(" ");

  return (
    <div
      className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-3"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تجهيز صورة التقويم"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div className="flex max-h-[94vh] w-full max-w-6xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl">
        <header className="flex h-14 shrink-0 items-center justify-between border-b px-4">
          <div className="flex items-center gap-2">
            <Crop className="h-5 w-5 text-clinic-blue" />
            <div>
              <h2 className="text-sm font-bold text-clinic-navy">تجهيز الصورة</h2>
              <p className="text-[11px] text-gray-500">{photo.caption || photo.subtype || photo.photoType}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-md text-gray-500 hover:bg-gray-100"
            aria-label="إغلاق"
          >
            <X className="h-4 w-4" />
          </button>
        </header>

        {loading ? (
          <div className="grid min-h-[460px] place-items-center">
            <Loader2 className="h-7 w-7 animate-spin text-clinic-blue" />
          </div>
        ) : (
          <div className="grid min-h-0 flex-1 lg:grid-cols-[minmax(0,1.45fr)_360px]">
            <section className="grid min-h-[360px] place-items-center bg-slate-950 p-5">
              <div
                className="relative max-h-[68vh] w-full max-w-3xl overflow-hidden bg-black shadow-xl"
                style={{ aspectRatio: ASPECT_RATIO_VALUE[value.aspectRatio] ?? "4 / 3" }}
              >
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={resolveImageUrl(value.originalPhotoUrl || photo.photoUrl)}
                  alt={photo.caption || "صورة تقويم"}
                  className="h-full w-full object-cover transition-[transform,filter] duration-150"
                  style={{
                    objectPosition: `${cropCenterX}% ${cropCenterY}%`,
                    transform: imageTransform,
                    filter: `brightness(${100 + value.brightness}%) contrast(${100 + value.contrast}%)`,
                  }}
                />
                <div className="pointer-events-none absolute inset-3 border border-white/60" />
              </div>
            </section>

            <aside className="min-h-0 overflow-y-auto border-r bg-white p-4">
              <div className="space-y-5">
                <section className="space-y-2">
                  <h3 className="text-xs font-bold text-clinic-navy">القوالب القياسية</h3>
                  <div className="grid grid-cols-2 gap-2">
                    {presets.map((preset) => (
                      <button
                        key={preset.key}
                        type="button"
                        onClick={() => applyPreset(preset)}
                        className={cn(
                          "min-h-9 rounded-md border px-2 text-xs font-medium transition",
                          value.preset === preset.key
                            ? "border-clinic-blue bg-clinic-blue-50 text-clinic-blue"
                            : "border-gray-200 text-gray-600 hover:border-clinic-blue/50"
                        )}
                      >
                        {preset.label}
                      </button>
                    ))}
                  </div>
                </section>

                <section className="space-y-3 border-t pt-4">
                  <div className="grid grid-cols-2 gap-2">
                    <label className="grid gap-1 text-xs text-gray-600">
                      نسبة العرض
                      <select
                        value={value.aspectRatio}
                        onChange={(event) => update({ aspectRatio: event.target.value })}
                        className="h-9 rounded-md border border-gray-200 bg-white px-2 text-xs text-clinic-navy"
                      >
                        {Object.keys(ASPECT_RATIO_VALUE).map((ratio) => (
                          <option key={ratio} value={ratio}>{ratio === "Original" ? "أصلية" : ratio}</option>
                        ))}
                      </select>
                    </label>
                    <NumberSlider label="التكبير" value={value.zoom} min={1} max={4} step={0.05} onChange={(zoom) => update({ zoom })} />
                  </div>
                  <NumberSlider label="الدوران" value={value.rotationDegrees} min={-180} max={180} unit="°" onChange={(rotationDegrees) => update({ rotationDegrees })} />
                  <NumberSlider label="الإضاءة" value={value.brightness} min={-100} max={100} unit="%" onChange={(brightness) => update({ brightness })} />
                  <NumberSlider label="التباين" value={value.contrast} min={-100} max={100} unit="%" onChange={(contrast) => update({ contrast })} />
                </section>

                <section className="space-y-3 border-t pt-4">
                  <h3 className="text-xs font-bold text-clinic-navy">حدود القص</h3>
                  <div className="grid grid-cols-2 gap-x-3 gap-y-2">
                    <NumberSlider label="أفقي" value={Math.round(value.cropX * 100)} min={0} max={Math.round((1 - value.cropWidth) * 100)} unit="%" onChange={(cropX) => update({ cropX: cropX / 100 })} />
                    <NumberSlider label="رأسي" value={Math.round(value.cropY * 100)} min={0} max={Math.round((1 - value.cropHeight) * 100)} unit="%" onChange={(cropY) => update({ cropY: cropY / 100 })} />
                    <NumberSlider label="العرض" value={Math.round(value.cropWidth * 100)} min={20} max={Math.round((1 - value.cropX) * 100)} unit="%" onChange={(cropWidth) => update({ cropWidth: cropWidth / 100 })} />
                    <NumberSlider label="الارتفاع" value={Math.round(value.cropHeight * 100)} min={20} max={Math.round((1 - value.cropY) * 100)} unit="%" onChange={(cropHeight) => update({ cropHeight: cropHeight / 100 })} />
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      type="button"
                      onClick={() => update({ flipHorizontal: !value.flipHorizontal })}
                      className={cn("inline-flex h-9 items-center justify-center gap-2 rounded-md border text-xs", value.flipHorizontal && "border-clinic-blue bg-clinic-blue-50 text-clinic-blue")}
                    >
                      <FlipHorizontal2 className="h-4 w-4" />
                      عكس أفقي
                    </button>
                    <button
                      type="button"
                      onClick={() => update({ flipVertical: !value.flipVertical })}
                      className={cn("inline-flex h-9 items-center justify-center gap-2 rounded-md border text-xs", value.flipVertical && "border-clinic-blue bg-clinic-blue-50 text-clinic-blue")}
                    >
                      <FlipVertical2 className="h-4 w-4" />
                      عكس رأسي
                    </button>
                  </div>
                </section>

                <section className="space-y-2 border-t pt-4">
                  <h3 className="text-xs font-bold text-clinic-navy">حالة الصورة</h3>
                  <div className="grid gap-2">
                    {STATUS_OPTIONS.map((option) => (
                      <button
                        key={option.value}
                        type="button"
                        onClick={() => update({ status: option.value })}
                        className={cn(
                          "flex h-9 items-center justify-between rounded-md border px-3 text-xs",
                          value.status === option.value
                            ? "border-clinic-blue bg-clinic-blue-50 font-semibold text-clinic-blue"
                            : "border-gray-200 text-gray-600"
                        )}
                      >
                        {option.label}
                        {value.status === option.value && <Check className="h-4 w-4" />}
                      </button>
                    ))}
                  </div>
                </section>
              </div>
            </aside>
          </div>
        )}

        <footer className="flex min-h-14 shrink-0 items-center justify-between gap-3 border-t px-4 py-2">
          <button
            type="button"
            onClick={reset}
            disabled={saving || loading}
            className="inline-flex h-9 items-center gap-2 rounded-md border border-gray-200 px-3 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
          >
            <RotateCcw className="h-4 w-4" />
            استعادة الأصل
          </button>
          <button
            type="button"
            onClick={save}
            disabled={saving || loading}
            className="inline-flex h-9 items-center gap-2 rounded-md bg-clinic-blue px-4 text-xs font-bold text-white hover:bg-clinic-blue-600 disabled:opacity-50"
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            حفظ التجهيز
          </button>
        </footer>
      </div>
    </div>
  );
}
