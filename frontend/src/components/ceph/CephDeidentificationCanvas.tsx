"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import NextImage from "next/image";
import { Eraser, Eye, ImagePlus, RotateCcw, ShieldCheck, Undo2 } from "lucide-react";
import {
  normalizeRedactionRect,
  type ImagePoint,
  type RedactionRect,
} from "@/lib/cephPilotImport";

export interface SanitizedPilotImage {
  blob: Blob;
  previewUrl: string;
  width: number;
  height: number;
  redactionCount: number;
}

interface Props {
  resetKey: number;
  onPrepared: (image: SanitizedPilotImage | null) => void;
}

const MAX_FILE_BYTES = 25 * 1024 * 1024;
const MIN_DIMENSION = 256;
const MAX_DIMENSION = 12000;

function canvasBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(blob => {
      if (blob) resolve(blob);
      else reject(new Error("تعذر إنشاء نسخة PNG منزوعة الهوية."));
    }, "image/png");
  });
}

export default function CephDeidentificationCanvas({ resetKey, onPrepared }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imageRef = useRef<HTMLImageElement | null>(null);
  const sourceUrlRef = useRef<string | null>(null);
  const previewUrlRef = useRef<string | null>(null);
  const dragStartRef = useRef<ImagePoint | null>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });
  const [redactions, setRedactions] = useState<RedactionRect[]>([]);
  const [draftRect, setDraftRect] = useState<RedactionRect | null>(null);
  const [prepared, setPrepared] = useState<SanitizedPilotImage | null>(null);
  const [error, setError] = useState("");

  const clearPrepared = useCallback(() => {
    if (previewUrlRef.current) URL.revokeObjectURL(previewUrlRef.current);
    previewUrlRef.current = null;
    setPrepared(null);
    onPrepared(null);
  }, [onPrepared]);

  const reset = useCallback(() => {
    clearPrepared();
    if (sourceUrlRef.current) URL.revokeObjectURL(sourceUrlRef.current);
    sourceUrlRef.current = null;
    imageRef.current = null;
    dragStartRef.current = null;
    setDimensions({ width: 0, height: 0 });
    setRedactions([]);
    setDraftRect(null);
    setError("");
  }, [clearPrepared]);

  useEffect(() => reset(), [resetKey, reset]);
  useEffect(() => () => {
    if (sourceUrlRef.current) URL.revokeObjectURL(sourceUrlRef.current);
    if (previewUrlRef.current) URL.revokeObjectURL(previewUrlRef.current);
  }, []);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const image = imageRef.current;
    if (!canvas || !image) return;
    const context = canvas.getContext("2d");
    if (!context) return;
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    context.fillStyle = "#000000";
    for (const rect of redactions) context.fillRect(rect.x, rect.y, rect.width, rect.height);
    if (draftRect) {
      context.fillStyle = "rgba(220, 38, 38, 0.58)";
      context.fillRect(draftRect.x, draftRect.y, draftRect.width, draftRect.height);
      context.strokeStyle = "#ffffff";
      context.lineWidth = Math.max(2, canvas.width / 700);
      context.setLineDash([10, 8]);
      context.strokeRect(draftRect.x, draftRect.y, draftRect.width, draftRect.height);
      context.setLineDash([]);
    }
  }, [draftRect, redactions]);

  useEffect(() => draw(), [draw]);
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || dimensions.width === 0) return;
    canvas.width = dimensions.width;
    canvas.height = dimensions.height;
    draw();
  }, [dimensions.height, dimensions.width, draw]);

  const selectFile = async (file: File | null) => {
    reset();
    if (!file) return;
    if (!(["image/jpeg", "image/png"].includes(file.type)) || file.size > MAX_FILE_BYTES) {
      setError("اختر صورة JPEG أو PNG لا تتجاوز 25 MB.");
      return;
    }

    const objectUrl = URL.createObjectURL(file);
    sourceUrlRef.current = objectUrl;
    const image = new window.Image();
    image.decoding = "async";
    image.src = objectUrl;
    try {
      await image.decode();
    } catch {
      reset();
      setError("تعذر قراءة الصورة المحددة.");
      return;
    }
    if (
      image.naturalWidth < MIN_DIMENSION || image.naturalHeight < MIN_DIMENSION ||
      image.naturalWidth > MAX_DIMENSION || image.naturalHeight > MAX_DIMENSION
    ) {
      reset();
      setError(`يجب أن تكون أبعاد الصورة بين ${MIN_DIMENSION} و${MAX_DIMENSION} بكسل.`);
      return;
    }

    imageRef.current = image;
    setDimensions({ width: image.naturalWidth, height: image.naturalHeight });
  };

  const imagePoint = (event: React.PointerEvent<HTMLCanvasElement>): ImagePoint => {
    const canvas = event.currentTarget;
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((event.clientX - rect.left) / rect.width) * canvas.width,
      y: ((event.clientY - rect.top) / rect.height) * canvas.height,
    };
  };

  const onPointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!imageRef.current || event.button !== 0) return;
    clearPrepared();
    event.currentTarget.setPointerCapture(event.pointerId);
    dragStartRef.current = imagePoint(event);
    setDraftRect(null);
  };

  const onPointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!dragStartRef.current) return;
    setDraftRect(normalizeRedactionRect(
      dragStartRef.current,
      imagePoint(event),
      dimensions.width,
      dimensions.height,
      1,
    ));
  };

  const finishPointer = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!dragStartRef.current) return;
    const next = normalizeRedactionRect(
      dragStartRef.current,
      imagePoint(event),
      dimensions.width,
      dimensions.height,
    );
    dragStartRef.current = null;
    setDraftRect(null);
    if (next) setRedactions(current => [...current, next]);
  };

  const prepare = async () => {
    const image = imageRef.current;
    if (!image) return;
    setError("");
    clearPrepared();
    const output = document.createElement("canvas");
    output.width = image.naturalWidth;
    output.height = image.naturalHeight;
    const context = output.getContext("2d");
    if (!context) {
      setError("تعذر تجهيز أداة إزالة الهوية في هذا المتصفح.");
      return;
    }
    context.drawImage(image, 0, 0, output.width, output.height);
    context.fillStyle = "#000000";
    for (const rect of redactions) context.fillRect(rect.x, rect.y, rect.width, rect.height);
    try {
      const blob = await canvasBlob(output);
      const previewUrl = URL.createObjectURL(blob);
      previewUrlRef.current = previewUrl;
      const result = {
        blob,
        previewUrl,
        width: output.width,
        height: output.height,
        redactionCount: redactions.length,
      };
      setPrepared(result);
      onPrepared(result);
    } catch (prepareError) {
      setError(prepareError instanceof Error ? prepareError.message : "تعذر تجهيز الصورة.");
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <label className="inline-flex cursor-pointer items-center gap-2 rounded-md bg-clinic-blue px-3 py-2 text-xs font-bold text-white hover:opacity-90">
          <ImagePlus className="h-4 w-4" />
          اختيار التصدير الرسمي
          <input
            key={resetKey}
            type="file"
            accept="image/jpeg,image/png,.jpg,.jpeg,.png"
            className="sr-only"
            onChange={event => void selectFile(event.target.files?.[0] ?? null)}
          />
        </label>
        <button
          type="button"
          title="التراجع عن آخر قناع"
          onClick={() => {
            clearPrepared();
            setRedactions(current => current.slice(0, -1));
          }}
          disabled={redactions.length === 0}
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-40"
        >
          <Undo2 className="h-4 w-4" />
        </button>
        <button
          type="button"
          title="مسح كل الأقنعة"
          onClick={() => {
            clearPrepared();
            setRedactions([]);
          }}
          disabled={redactions.length === 0}
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-40"
        >
          <Eraser className="h-4 w-4" />
        </button>
        <button
          type="button"
          title="إعادة اختيار الصورة"
          onClick={reset}
          disabled={!imageRef.current}
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-40"
        >
          <RotateCcw className="h-4 w-4" />
        </button>
      </div>

      {error ? <div role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-800">{error}</div> : null}

      <div className="relative flex min-h-96 items-center justify-center overflow-auto border border-gray-200 bg-black">
        {dimensions.width === 0 ? (
          <div className="px-6 text-center text-sm text-gray-400">
            اختر صورة الأشعة المصدرة رسميًا. لن تُرفع النسخة الأصلية إلى خادم Aqlan.
          </div>
        ) : (
          <canvas
            ref={canvasRef}
            aria-label="مساحة تحديد مناطق إزالة الهوية"
            onPointerDown={onPointerDown}
            onPointerMove={onPointerMove}
            onPointerUp={finishPointer}
            onPointerCancel={() => {
              dragStartRef.current = null;
              setDraftRect(null);
            }}
            className="block max-h-[68vh] max-w-full cursor-crosshair object-contain touch-none"
          />
        )}
      </div>

      {dimensions.width > 0 ? (
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-200 pt-3">
          <div className="text-xs text-gray-600">
            اسحب فوق كل اسم أو رقم أو تاريخ أو Barcode/QR. الأقنعة المطبقة: <b>{redactions.length}</b>
          </div>
          <button
            type="button"
            onClick={() => void prepare()}
            className="inline-flex items-center gap-2 rounded-md bg-gray-900 px-3 py-2 text-xs font-bold text-white hover:bg-black"
          >
            <ShieldCheck className="h-4 w-4" />
            إنشاء المعاينة النهائية
          </button>
        </div>
      ) : null}

      {prepared ? (
        <div className="border border-emerald-200 bg-emerald-50 p-3">
          <div className="mb-2 flex items-center gap-2 text-xs font-bold text-emerald-900">
            <Eye className="h-4 w-4" />
            المعاينة التي ستُرفع إلى Pilot
          </div>
          <NextImage
            src={prepared.previewUrl}
            alt="معاينة الأشعة بعد إزالة الهوية"
            width={prepared.width}
            height={prepared.height}
            unoptimized
            className="max-h-64 max-w-full border border-emerald-200 bg-black object-contain"
          />
        </div>
      ) : null}
    </div>
  );
}
