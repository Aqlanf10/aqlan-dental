"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Camera, Loader2, ScanLine, X } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";

/**
 * LABINV-REQ-008 — resolve a scanned lab order slip to its record.
 *
 * The problem: a box comes back from the lab and nothing on it links to the order except a
 * number someone has to read and then search for. That is the "تراكم التراكيب" bottleneck
 * at the receiving end — cases pile up on a bench because matching them to records is
 * slower than stacking them.
 *
 * Two deliberate constraints:
 *
 * 1. **Manual entry is always present, never a fallback that appears only on failure.**
 *    `BarcodeDetector` does not exist in Safari, and a label can be smudged or torn. A
 *    scanner that strands the user when the camera route fails is worse than a text box.
 *
 * 2. **The lookup is a normal permission-checked read.** Holding a printed slip grants
 *    nothing extra: the server runs the same permission, branch, and per-patient gates as
 *    opening the order from the list, and answers every failure identically so the scanner
 *    cannot be used to discover which order numbers exist.
 */

/** Minimal shape of the Barcode Detection API; it has no lib.dom typing. */
interface DetectedBarcode {
  rawValue: string;
}
interface BarcodeDetectorLike {
  detect: (source: CanvasImageSource) => Promise<DetectedBarcode[]>;
}
type BarcodeDetectorCtor = new (options?: { formats?: string[] }) => BarcodeDetectorLike;

function getBarcodeDetector(): BarcodeDetectorCtor | null {
  if (typeof window === "undefined") return null;
  const ctor = (window as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector;
  return typeof ctor === "function" ? ctor : null;
}

export interface ScannedOrder {
  id: string;
  orderNumber: string | null;
}

interface Props {
  onClose: () => void;
  onResolved: (order: ScannedOrder) => void;
}

export function ScanOrderDialog({ onClose, onResolved }: Props) {
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [isLooking, setIsLooking] = useState(false);
  const [cameraOn, setCameraOn] = useState(false);
  const [cameraError, setCameraError] = useState("");

  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rafRef = useRef<number | null>(null);
  const resolvedRef = useRef(false);

  const cameraSupported = getBarcodeDetector() !== null;

  const stopCamera = useCallback(() => {
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    setCameraOn(false);
  }, []);

  const lookup = useCallback(
    async (raw: string) => {
      const trimmed = raw.trim();
      if (!trimmed) {
        setError("أدخل رقم الطلب أو امسح الرمز");
        return;
      }
      setIsLooking(true);
      setError("");
      try {
        const { data } = await api.get<ScannedOrder>("/api/lab-orders/lookup", {
          params: { code: trimmed },
        });
        resolvedRef.current = true;
        stopCamera();
        onResolved({ id: data.id, orderNumber: data.orderNumber ?? trimmed });
      } catch (err) {
        // The server answers every miss the same way on purpose; the message is shown
        // verbatim rather than reinterpreted into a guess about why.
        setError(extractErrorMessage(err));
      } finally {
        setIsLooking(false);
      }
    },
    [onResolved, stopCamera],
  );

  const startCamera = useCallback(async () => {
    const Detector = getBarcodeDetector();
    if (!Detector) {
      setCameraError("متصفحك لا يدعم قراءة الرموز بالكاميرا — أدخل رقم الطلب يدويًا");
      return;
    }

    setCameraError("");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: "environment" },
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      setCameraOn(true);

      const detector = new Detector({ formats: ["qr_code", "code_128"] });

      const tick = async () => {
        if (resolvedRef.current || !videoRef.current) return;
        try {
          const found = await detector.detect(videoRef.current);
          const value = found[0]?.rawValue?.trim();
          if (value) {
            setCode(value);
            await lookup(value);
            return;
          }
        } catch {
          // A single failed frame is normal while focusing. Keep scanning rather than
          // tearing the camera down on the first miss.
        }
        rafRef.current = requestAnimationFrame(() => void tick());
      };
      rafRef.current = requestAnimationFrame(() => void tick());
    } catch {
      setCameraError("تعذر فتح الكاميرا — تأكد من منح الإذن، أو أدخل رقم الطلب يدويًا");
      stopCamera();
    }
  }, [lookup, stopCamera]);

  useEffect(() => stopCamera, [stopCamera]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-md rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div className="flex items-center gap-2">
            <ScanLine className="h-5 w-5 text-cyan-700" aria-hidden />
            <h2 className="text-base font-bold text-gray-900">مسح رمز أمر المختبر</h2>
          </div>
          <button
            type="button"
            onClick={() => {
              stopCamera();
              onClose();
            }}
            aria-label="إغلاق"
            className="text-gray-400 hover:text-gray-700"
          >
            <X className="h-5 w-5" aria-hidden />
          </button>
        </div>

        <div className="space-y-4 px-5 py-4">
          {cameraOn && (
            <div className="overflow-hidden rounded-xl bg-black">
              {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
              <video ref={videoRef} className="h-48 w-full object-cover" playsInline muted />
            </div>
          )}

          {!cameraOn && cameraSupported && (
            <button
              type="button"
              onClick={() => void startCamera()}
              className="flex w-full items-center justify-center gap-2 rounded-lg border border-cyan-200 bg-cyan-50 px-4 py-3 text-sm font-semibold text-cyan-800 hover:bg-cyan-100"
            >
              <Camera className="h-4 w-4" aria-hidden />
              فتح الكاميرا للمسح
            </button>
          )}

          {!cameraSupported && (
            <p className="rounded-lg bg-gray-50 px-3 py-2 text-xs text-gray-600">
              متصفحك لا يدعم قراءة الرموز بالكاميرا. أدخل رقم الطلب المطبوع أسفل الرمز.
            </p>
          )}

          {cameraError && (
            <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
              {cameraError}
            </p>
          )}

          <div className="space-y-1.5">
            <label htmlFor="lab-scan-code" className="text-sm font-medium text-gray-700">
              رقم الطلب
            </label>
            <div className="flex gap-2">
              <input
                id="lab-scan-code"
                value={code}
                onChange={(e) => {
                  setCode(e.target.value);
                  setError("");
                }}
                onKeyDown={(e) => {
                  // Barcode wedges type the code then press Enter.
                  if (e.key === "Enter") {
                    e.preventDefault();
                    void lookup(code);
                  }
                }}
                placeholder="LAB-2026-003"
                dir="ltr"
                autoFocus
                className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm"
              />
              <button
                type="button"
                onClick={() => void lookup(code)}
                disabled={isLooking}
                className="shrink-0 rounded-lg bg-cyan-700 px-4 py-2 text-sm font-semibold text-white hover:bg-cyan-800 disabled:opacity-50"
              >
                {isLooking ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : "بحث"}
              </button>
            </div>
          </div>

          {error && (
            <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-800">
              {error}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
