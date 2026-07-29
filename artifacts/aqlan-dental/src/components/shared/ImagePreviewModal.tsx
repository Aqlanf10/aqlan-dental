
import { useEffect, useCallback } from "react";
import { X as XIcon, Download, ZoomIn, ChevronRight, ChevronLeft } from "lucide-react";

interface ImagePreviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  url: string;
  fileName?: string;
  mimeType?: string;
  /** Navigation: pass all items and current index for prev/next */
  items?: { url: string; fileName?: string; mimeType?: string }[];
  currentIndex?: number;
  onNavigate?: (index: number) => void;
}

export function ImagePreviewModal({
  isOpen,
  onClose,
  url,
  fileName,
  mimeType,
  items,
  currentIndex = 0,
  onNavigate,
}: ImagePreviewModalProps) {
  const isImage = !mimeType || mimeType.startsWith("image/");
  const isPdf = mimeType === "application/pdf";

  const hasPrev = items && currentIndex > 0;
  const hasNext = items && currentIndex < items.length - 1;

  const goPrev = useCallback(() => {
    if (hasPrev && onNavigate) onNavigate(currentIndex - 1);
  }, [hasPrev, onNavigate, currentIndex]);

  const goNext = useCallback(() => {
    if (hasNext && onNavigate) onNavigate(currentIndex + 1);
  }, [hasNext, onNavigate, currentIndex]);

  // Keyboard navigation
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
      if (e.key === "ArrowRight") goPrev(); // RTL: right = prev
      if (e.key === "ArrowLeft") goNext();  // RTL: left = next
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [isOpen, onClose, goPrev, goNext]);

  // Prevent body scroll
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => { document.body.style.overflow = ""; };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center"
      onClick={onClose}
     
    >
      <div
        className="relative w-full h-full flex items-center justify-center p-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Close button */}
        <button
          onClick={onClose}
          className="absolute top-4 left-4 z-10 w-10 h-10 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white transition"
        >
          <XIcon className="w-5 h-5" />
        </button>

        {/* File name */}
        {fileName && (
          <div className="absolute top-4 right-4 z-10 bg-black/50 rounded-lg px-3 py-1.5">
            <p className="text-sm text-white truncate max-w-[300px]">{fileName}</p>
          </div>
        )}

        {/* Prev/Next navigation */}
        {items && items.length > 1 && (
          <>
            {hasPrev && (
              <button
                onClick={goPrev}
                className="absolute right-4 top-1/2 -translate-y-1/2 z-10 w-10 h-10 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white transition"
              >
                <ChevronRight className="w-5 h-5" />
              </button>
            )}
            {hasNext && (
              <button
                onClick={goNext}
                className="absolute left-4 top-1/2 -translate-y-1/2 z-10 w-10 h-10 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white transition"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
            )}
            <div className="absolute bottom-4 left-1/2 -translate-x-1/2 z-10 bg-black/50 rounded-lg px-3 py-1">
              <p className="text-xs text-white">{currentIndex + 1} / {items.length}</p>
            </div>
          </>
        )}

        {/* Content */}
        {isImage ? (
          <>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={url}
            alt={fileName ?? "صورة"}
            className="max-w-[90vw] max-h-[85vh] object-contain rounded-lg"
            onClick={(e) => e.stopPropagation()}
          />
          </>
        ) : isPdf ? (
          <iframe
            src={url}
            className="w-[90vw] h-[85vh] rounded-lg border-0"
            title={fileName ?? "PDF"}
            onClick={(e) => e.stopPropagation()}
          />
        ) : (
          <div className="bg-white rounded-2xl p-8 text-center max-w-md">
            <ZoomIn className="w-12 h-12 mx-auto mb-4 text-[#94a3b8]" />
            <p className="text-sm text-[#64748b] mb-4">لا يمكن عرض هذا الملف مباشرة</p>
            <a
              href={url}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition"
            >
              <Download className="w-4 h-4" />
              تحميل الملف
            </a>
          </div>
        )}
      </div>
    </div>
  );
}
