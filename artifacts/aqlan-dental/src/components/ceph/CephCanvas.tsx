import { useRef, useEffect, useState, useCallback, useMemo } from "react";
import {
  ZoomIn, ZoomOut, Hand, Ruler, Brain, Loader2, RotateCw,
  RotateCcw, FlipHorizontal, FlipVertical, Trash2, DraftingCompass,
} from "lucide-react";
import type { CephLandmark, CephMeasurement } from "@/types/ceph";
import { tracingPolylines } from "@/lib/cephTracing";
import { cn } from "@/lib/utils";
import {
  applyViewerMatrix,
  buildViewerMatrix,
  DEFAULT_VIEWER_TRANSFORM,
  invertViewerMatrix,
  isDefaultViewerTransform,
  viewerAngle,
  viewerDistance,
  type ViewerPoint,
  type ViewerTransform,
} from "@/lib/cephViewer";

const HIT_RADIUS = 10;
const MIN_ZOOM = 0.25;
const MAX_ZOOM = 8;

export const LANDMARK_DEFS: Record<string, { nameAr: string; group: string; color: string }> = {
  S:   { nameAr: 'السرج',                  group: 'cranial',  color: '#60A5FA' },
  N:   { nameAr: 'الناسيون',              group: 'cranial',  color: '#60A5FA' },
  Or:  { nameAr: 'قاع المدار',             group: 'cranial',  color: '#A78BFA' },
  Po:  { nameAr: 'قمة المسمع',             group: 'cranial',  color: '#A78BFA' },
  ANS: { nameAr: 'الشوكة الأمامية',        group: 'maxilla',  color: '#FB923C' },
  PNS: { nameAr: 'الشوكة الخلفية',        group: 'maxilla',  color: '#FB923C' },
  A:   { nameAr: 'النقطة A',              group: 'maxilla',  color: '#FB923C' },
  B:   { nameAr: 'النقطة B',              group: 'mandible', color: '#F87171' },
  Pog: { nameAr: 'الذقن البارز',           group: 'mandible', color: '#F87171' },
  Gn:  { nameAr: 'الذقن',                 group: 'mandible', color: '#F87171' },
  Me:  { nameAr: 'الأسفل',               group: 'mandible', color: '#F87171' },
  Go:  { nameAr: 'زاوية الفك',             group: 'mandible', color: '#F87171' },
  Co:  { nameAr: 'رأس اللقمة',            group: 'mandible', color: '#EF4444' },
  Ar:  { nameAr: 'المفصل',               group: 'mandible', color: '#EF4444' },
  D:   { nameAr: 'النقطة D',              group: 'mandible', color: '#F87171' },
  Pm:  { nameAr: 'بروز الذقن',             group: 'mandible', color: '#F87171' },
  U1T: { nameAr: 'طرف القاطع ع',          group: 'dental',   color: '#34D399' },
  U1A: { nameAr: 'قمة القاطع ع',          group: 'dental',   color: '#34D399' },
  L1T: { nameAr: 'طرف القاطع س',          group: 'dental',   color: '#10B981' },
  L1A: { nameAr: 'قمة القاطع س',          group: 'dental',   color: '#10B981' },
  U6:  { nameAr: 'الرحى الأولى ع (U6)',   group: 'dental',   color: '#2DD4BF' },
  L6:  { nameAr: 'الرحى الأولى س (L6)',   group: 'dental',   color: '#14B8A6' },
  LS:  { nameAr: 'الشفة العلوية',         group: 'soft',     color: '#F472B6' },
  LI:  { nameAr: 'الشفة السفلية',         group: 'soft',     color: '#F472B6' },
  Pn:  { nameAr: 'طرف الأنف',             group: 'soft',     color: '#F472B6' },
  Cm:  { nameAr: 'قاعدة الأنف',           group: 'soft',     color: '#F472B6' },
  SPog:{ nameAr: 'الذقن الرخو (Pog′)',    group: 'soft',     color: '#F472B6' },
};

const LANDMARK_ORDER = ['S','N','Or','Po','ANS','PNS','A','B','Pog','Gn','Me','Go','Co','Ar','D','Pm','U1T','U1A','L1T','L1A','U6','L6','LS','LI','Pn','Cm','SPog'];

const PLANES = [
  { key: 'SN',   pts: ['S',   'N'],   color: '#60A5FA', dash: [] as number[],   label: 'SN' },
  { key: 'FH',   pts: ['Po',  'Or'],  color: '#A78BFA', dash: [6, 4],           label: 'FH' },
  { key: 'MdP',  pts: ['Go',  'Me'],  color: '#F87171', dash: [] as number[],   label: 'MdP' },
  { key: 'NA',   pts: ['N',   'A'],   color: '#FB923C', dash: [4, 4],           label: 'N-A' },
  { key: 'NB',   pts: ['N',   'B'],   color: '#FCD34D', dash: [4, 4],           label: 'N-B' },
  { key: 'NPog', pts: ['N',   'Pog'], color: '#FBBF24', dash: [2, 4],           label: '' },
  { key: 'MaxP', pts: ['ANS', 'PNS'], color: '#FB923C', dash: [] as number[],   label: '' },
  { key: 'U1ax', pts: ['U1A', 'U1T'], color: '#34D399', dash: [] as number[],   label: '' },
  { key: 'L1ax', pts: ['L1A', 'L1T'], color: '#10B981', dash: [] as number[],   label: '' },
  // Jarabak (Björk) polygon legs — S-N (=SN above) / S-Ar / Ar-Go / Go-Me (=MdP).
  { key: 'SAr',  pts: ['S',   'Ar'],  color: '#C084FC', dash: [] as number[],   label: 'S-Ar' },
  { key: 'ArGo', pts: ['Ar',  'Go'],  color: '#C084FC', dash: [] as number[],   label: 'Ar-Go' },
  // SEQ-40: functional occlusal plane — incisal overbite midpoint → first-molar
  // occlusion midpoint. A "A|B" endpoint means the midpoint of two landmarks.
  { key: 'OcP',  pts: ['U1T|L1T', 'U6|L6'], color: '#22D3EE', dash: [8, 4],     label: 'OcP' },
];

// Planes that are part of the measurement overlay (drawn even when the
// general planes toggle is off, as long as showMeasurements is on).
const MEASUREMENT_PLANE_KEYS = new Set(['SN', 'FH', 'MdP', 'NA', 'NB', 'U1ax', 'L1ax', 'OcP']);

// Floating value labels for the measurement overlay: measurement → anchor
// landmark + canvas-space offset (kept constant regardless of zoom).
const MEASUREMENT_LABELS: { name: string; anchor: string; dx: number; dy: number }[] = [
  { name: 'SNA',  anchor: 'A',   dx: 14, dy: -14 },
  { name: 'SNB',  anchor: 'B',   dx: 14, dy: -14 },
  { name: 'ANB',  anchor: 'N',   dx: 14, dy: -18 },
  { name: 'FMA',  anchor: 'Go',  dx: -14, dy: 18 },
  { name: 'IMPA', anchor: 'L1T', dx: 14, dy: 18 },
];

const SEVERITY_COLOR: Record<string, string> = {
  normal: '#4ADE80',
  mild:   '#FBBF24',
  severe: '#F87171',
};

export const SIMULATION_SCENARIOS: Record<string, {
  label: string;
  vectors: Record<string, { dx: number; dy: number }>;
}> = {
  extraction_retraction: {
    label: 'قلع + ارتداد',
    vectors: {
      U1T: { dx: -0.038, dy: 0.006 }, U1A: { dx: -0.028, dy: 0.004 },
      L1T: { dx: -0.028, dy: -0.005 }, L1A: { dx: -0.022, dy: -0.003 },
      LS:  { dx: -0.023, dy: 0.002 }, LI:  { dx: -0.017, dy: 0.001 },
      Pog: { dx: -0.005, dy: 0 },
    },
  },
  class3_camouflage: {
    label: 'تقنع الدرجة الثالثة',
    vectors: {
      U1T: { dx: 0.030, dy: -0.004 }, U1A: { dx: 0.022, dy: -0.003 },
      L1T: { dx: -0.022, dy: 0.004 }, L1A: { dx: -0.016, dy: 0.003 },
      LS:  { dx: 0.018, dy: -0.002 }, LI:  { dx: -0.014, dy: 0.002 },
    },
  },
  surgical_advancement: {
    label: 'تقدم الفك السفلي جراحياً',
    vectors: {
      B:   { dx: 0.035, dy: 0 }, Pog: { dx: 0.038, dy: 0 },
      Gn:  { dx: 0.035, dy: 0 }, Me:  { dx: 0.032, dy: 0 },
      Go:  { dx: 0.020, dy: -0.005 },
      L1T: { dx: 0.030, dy: 0 }, L1A: { dx: 0.030, dy: 0 },
      LI:  { dx: 0.020, dy: 0 },
    },
  },
};

type Pt = { x: number; y: number };
type View = { s: number; ox: number; oy: number };

interface Props {
  imageUrl: string | null;
  imageWidth: number;
  imageHeight: number;
  landmarks: CephLandmark[];
  onLandmarksChange: (lm: CephLandmark[]) => void;
  selectedKey: string | null;
  onSelectKey: (key: string | null) => void;
  showPlanes: boolean;
  /** Draw the anatomical tracing (contours connecting placed landmarks). */
  showTracing?: boolean;
  showSimulation: boolean;
  simulationScenario: string;
  /** Measurement overlay: draw value labels for SNA/SNB/ANB/FMA/IMPA. */
  showMeasurements?: boolean;
  /** Live measurement list computed by the page (same map the report uses). */
  measurements?: CephMeasurement[];
  /** Called when the two-point ruler calibration is applied (px per mm). */
  onCalibrate?: (pixelsPerMm: number) => void;
  /** Saved calibration used by the transient viewer distance ruler. */
  pixelsPerMm?: number | null;
  imageAdjustments?: {
    brightness: number;
    contrast: number;
    inverted: boolean;
  };
  onImageDimensions?: (width: number, height: number) => void;
  /** Restores page-owned brightness/contrast/invert controls. */
  onResetImageAdjustments?: () => void;
  /**
   * AI-assisted per-landmark refinement callback. Fired when the orthodontist
   * right-clicks (or long-presses on touch) an AI-placed landmark and picks
   * "تحسين موضع النقطة بالذكاء الاصطناعي" from the context menu. The page
   * calls POST /api/ceph/{id}/ai/refine-landmark with the current position and
   * replaces the landmark with the refined one (still an UNSAVED AI draft).
   */
  onRefineLandmark?: (key: string) => void;
  /** True while a refine request is in flight — disables the menu item. */
  refining?: boolean;
}

export function CephCanvas({
  imageUrl, imageWidth, imageHeight, landmarks, onLandmarksChange,
  selectedKey, onSelectKey, showPlanes, showTracing = false, showSimulation, simulationScenario,
  showMeasurements = false, measurements, onCalibrate,
  pixelsPerMm = null,
  imageAdjustments = { brightness: 100, contrast: 100, inverted: false },
  onImageDimensions,
  onResetImageAdjustments,
  onRefineLandmark,
  refining = false,
}: Props) {
  const canvasRef  = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [img, setImg]         = useState<HTMLImageElement | null>(null);
  const [imgError, setImgError] = useState(false);
  const [dragging, setDragging] = useState<string | null>(null);
  const [hovered,  setHovered]  = useState<string | null>(null);
  // Right-click / long-press context menu for AI refinement of a single landmark.
  const [ctxMenu, setCtxMenu] = useState<{ key: string; x: number; y: number } | null>(null);
  const longPressTimer = useRef<number | null>(null);

  // ── Zoom / pan state ──
  // `view` is the explicit transform; null = auto fit-to-container.
  const [view, setView]       = useState<View | null>(null);
  const [panMode, setPanMode] = useState(false);
  const [spaceHeld, setSpaceHeld] = useState(false);
  const [canvasSize, setCanvasSize] = useState({ w: 0, h: 0 });
  const panRef = useRef<{ sx: number; sy: number; ox: number; oy: number } | null>(null);

  // ── Calibration state (points stored in IMAGE space, kept across mode toggles) ──
  const [calMode, setCalMode]     = useState(false);
  const [calPoints, setCalPoints] = useState<Pt[]>([]);
  const [calCursor, setCalCursor] = useState<Pt | null>(null);
  const [calMm, setCalMm]         = useState('10');

  // Viewer-only tools never modify or persist landmarks/image data.
  const [viewerTool, setViewerTool] = useState<'distance' | 'angle' | null>(null);
  const [viewerPoints, setViewerPoints] = useState<ViewerPoint[]>([]);
  const [viewerCursor, setViewerCursor] = useState<ViewerPoint | null>(null);
  const [viewerTransform, setViewerTransform] = useState<ViewerTransform>(DEFAULT_VIEWER_TRANSFORM);

  useEffect(() => {
    if (!imageUrl) { setImg(null); setImgError(false); return; }
    const el = new window.Image();
    // NOTE: do NOT set crossOrigin. The canvas only draws the radiograph (with
    // ctx.filter for brightness/contrast) and never reads its pixels, so a
    // tainted canvas is fine. Requesting CORS ('anonymous') against an uploads
    // host that sends no CORS headers made the image silently fail to load
    // (onerror → blank), which looked like "the image won't show".
    el.onload  = () => {
      setImg(el);
      setImgError(false);
      onImageDimensions?.(el.naturalWidth, el.naturalHeight);
    };
    el.onerror = () => { setImg(null); setImgError(true); };
    el.src = imageUrl;
  }, [imageUrl, onImageDimensions]);

  const lmMap = useMemo(() => {
    const m: Record<string, CephLandmark> = {};
    landmarks.forEach(l => { m[l.key] = l; });
    return m;
  }, [landmarks]);

  const measMap = useMemo(() => {
    const m: Record<string, CephMeasurement> = {};
    (measurements ?? []).forEach(x => { m[x.name] = x; });
    return m;
  }, [measurements]);

  /** Fit-to-container transform (the 100% baseline). */
  const getFit = useCallback(() => {
    const c = canvasRef.current;
    if (!c || !c.width || !c.height) return null;
    const W = c.width, H = c.height;
    const iW = imageWidth  || img?.naturalWidth  || W;
    const iH = imageHeight || img?.naturalHeight || H;
    const matrix = buildViewerMatrix(iW, iH, viewerTransform);
    const s = Math.min(W / matrix.width, H / matrix.height);
    return {
      s,
      ox: (W - matrix.width * s) / 2,
      oy: (H - matrix.height * s) / 2,
      iW,
      iH,
      displayW: matrix.width,
      displayH: matrix.height,
      matrix,
    };
  }, [imageWidth, imageHeight, img, viewerTransform]);

  /** Active transform: explicit view (zoom/pan) or fit. Landmarks stay in image space. */
  const getT = useCallback(() => {
    const fit = getFit();
    if (!fit) return null;
    const v = view ?? fit;
    return {
      s: v.s, ox: v.ox, oy: v.oy, iW: fit.iW, iH: fit.iH,
      displayW: fit.displayW, displayH: fit.displayH, matrix: fit.matrix, fitS: fit.s,
      tc: (x: number, y: number) => {
        const p = applyViewerMatrix({ x, y }, fit.matrix);
        return { x: v.ox + p.x * v.s, y: v.oy + p.y * v.s };
      },
      ti: (cx: number, cy: number) => invertViewerMatrix({
        x: (cx - v.ox) / v.s,
        y: (cy - v.oy) / v.s,
      }, fit.matrix),
    };
  }, [getFit, view]);

  /** Zoom by `factor` keeping the canvas point (cx, cy) fixed. Clamped 0.25x–8x of fit. */
  const zoomAt = useCallback((cx: number, cy: number, factor: number) => {
    const fit = getFit(); if (!fit) return;
    const cur = view ?? fit;
    const z = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, (cur.s / fit.s) * factor));
    const ns = fit.s * z;
    if (ns === cur.s) return;
    setView({
      s: ns,
      ox: cx - (cx - cur.ox) * (ns / cur.s),
      oy: cy - (cy - cur.oy) * (ns / cur.s),
    });
  }, [getFit, view]);

  const centerZoom = useCallback((factor: number) => {
    const c = canvasRef.current; if (!c) return;
    zoomAt(c.width / 2, c.height / 2, factor);
  }, [zoomAt]);

  const resetFit = useCallback(() => setView(null), []);

  const draw = useCallback(() => {
    const c = canvasRef.current; if (!c) return;
    const ctx = c.getContext('2d'); if (!ctx) return;
    const T = getT(); if (!T) return;

    ctx.clearRect(0, 0, c.width, c.height);
    ctx.fillStyle = '#0F172A';
    ctx.fillRect(0, 0, c.width, c.height);

    // Image / placeholder
    if (img) {
      ctx.save();
      ctx.filter = [
        `brightness(${imageAdjustments.brightness}%)`,
        `contrast(${imageAdjustments.contrast}%)`,
        imageAdjustments.inverted ? "invert(1)" : "invert(0)",
      ].join(" ");
      ctx.translate(T.ox, T.oy);
      ctx.scale(T.s, T.s);
      ctx.transform(
        T.matrix.a, T.matrix.b, T.matrix.c,
        T.matrix.d, T.matrix.e, T.matrix.f,
      );
      ctx.drawImage(img, 0, 0, T.iW, T.iH);
      ctx.restore();
      // A light veil keeps colored landmarks legible without obscuring anatomy.
      ctx.fillStyle = 'rgba(0,0,0,0.06)';
      ctx.fillRect(T.ox, T.oy, T.displayW * T.s, T.displayH * T.s);
    } else {
      ctx.fillStyle = '#1E293B';
      ctx.fillRect(T.ox, T.oy, T.displayW * T.s, T.displayH * T.s);
      ctx.textAlign = 'center';
      if (imageUrl && imgError) {
        // A URL is set but the file could not be loaded (missing/expired upload
        // or unreachable host) — say so honestly instead of "add an image".
        ctx.fillStyle = '#F87171';
        ctx.font = '14px sans-serif';
        ctx.fillText('تعذّر تحميل صورة الأشعة', c.width / 2, c.height / 2 - 10);
        ctx.font = '11px sans-serif';
        ctx.fillStyle = '#94A3B8';
        ctx.fillText('قد تكون الصورة فُقدت بعد إعادة النشر — أعد رفعها', c.width / 2, c.height / 2 + 10);
      } else {
        ctx.fillStyle = '#64748B';
        ctx.font = '14px sans-serif';
        ctx.fillText('أضف رابط صورة الأشعة السيفالومترية', c.width / 2, c.height / 2 - 10);
        ctx.font = '11px sans-serif';
        ctx.fillStyle = '#475569';
        ctx.fillText('سيتم عرض الصورة هنا للتحديد اليدوي', c.width / 2, c.height / 2 + 10);
      }
    }

    // Planes (measurement-overlay planes are drawn even when planes are hidden)
    if (showPlanes || showMeasurements) {
      // "A|B" plane endpoints resolve to the midpoint of two landmarks
      // (used by the occlusal plane); plain keys resolve to the landmark.
      const resolvePt = (spec: string): { x: number; y: number } | null => {
        if (spec.includes('|')) {
          const [a, b] = spec.split('|');
          const la = lmMap[a], lb = lmMap[b];
          if (!la || !lb) return null;
          return { x: (la.x + lb.x) / 2, y: (la.y + lb.y) / 2 };
        }
        const lm = lmMap[spec];
        return lm ? { x: lm.x, y: lm.y } : null;
      };
      PLANES.forEach(p => {
        if (!showPlanes && !MEASUREMENT_PLANE_KEYS.has(p.key)) return;
        const lm1 = resolvePt(p.pts[0]), lm2 = resolvePt(p.pts[1]);
        if (!lm1 || !lm2) return;
        const p1 = T.tc(lm1.x, lm1.y), p2 = T.tc(lm2.x, lm2.y);
        const dx = p2.x - p1.x, dy = p2.y - p1.y;
        ctx.save();
        ctx.globalAlpha = 0.65;
        ctx.strokeStyle = p.color;
        ctx.lineWidth = 1.2;
        if (p.dash.length) ctx.setLineDash(p.dash);
        ctx.beginPath();
        ctx.moveTo(p1.x - dx * 4, p1.y - dy * 4);
        ctx.lineTo(p2.x + dx * 4, p2.y + dy * 4);
        ctx.stroke();
        ctx.setLineDash([]);
        if (p.label) {
          ctx.fillStyle = p.color;
          ctx.font = 'bold 10px monospace';
          ctx.textAlign = 'center';
          ctx.fillText(p.label, (p1.x + p2.x) / 2 + 10, (p1.y + p2.y) / 2 - 5);
        }
        ctx.restore();
      });
    }

    // Anatomical tracing — connect placed landmarks into the standard outline.
    if (showTracing) {
      const polylines = tracingPolylines(k => Boolean(lmMap[k]));
      ctx.save();
      ctx.lineWidth = 2;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.globalAlpha = 0.85;
      for (const line of polylines) {
        ctx.strokeStyle = line.color;
        ctx.beginPath();
        line.keys.forEach((key, i) => {
          const lm = lmMap[key];
          const cp = T.tc(lm.x, lm.y);
          if (i === 0) ctx.moveTo(cp.x, cp.y);
          else ctx.lineTo(cp.x, cp.y);
        });
        ctx.stroke();
      }
      ctx.restore();
    }

    // Simulation overlay
    if (showSimulation) {
      const sc = SIMULATION_SCENARIOS[simulationScenario];
      if (sc) {
        Object.entries(sc.vectors).forEach(([key, v]) => {
          const lm = lmMap[key]; if (!lm) return;
          const sx = lm.x + v.dx * T.iW, sy = lm.y + v.dy * T.iW;
          const cp0 = T.tc(lm.x, lm.y), cp = T.tc(sx, sy);
          ctx.save();
          ctx.globalAlpha = 0.7;
          ctx.strokeStyle = '#4ADE80';
          ctx.lineWidth = 1.5;
          ctx.setLineDash([4, 3]);
          ctx.beginPath(); ctx.moveTo(cp0.x, cp0.y); ctx.lineTo(cp.x, cp.y); ctx.stroke();
          ctx.setLineDash([]);
          ctx.fillStyle = '#4ADE80';
          ctx.beginPath(); ctx.arc(cp.x, cp.y, 5, 0, Math.PI * 2); ctx.fill();
          // Arrow head
          const ang = Math.atan2(cp.y - cp0.y, cp.x - cp0.x);
          ctx.beginPath();
          ctx.moveTo(cp.x, cp.y);
          ctx.lineTo(cp.x - 7 * Math.cos(ang - 0.4), cp.y - 7 * Math.sin(ang - 0.4));
          ctx.lineTo(cp.x - 7 * Math.cos(ang + 0.4), cp.y - 7 * Math.sin(ang + 0.4));
          ctx.closePath(); ctx.fill();
          ctx.restore();
        });
      }
    }

    // Landmarks
    landmarks.forEach(lm => {
      const def = LANDMARK_DEFS[lm.key];
      const color = def?.color ?? '#38BDF8';
      const cp = T.tc(lm.x, lm.y);
      const isSel = lm.key === selectedKey;
      const isHov = lm.key === hovered;
      const r = isSel ? 7 : isHov ? 6 : 5;

      // Selection ring
      if (isSel) {
        ctx.save();
        ctx.strokeStyle = 'rgba(255,255,255,0.9)';
        ctx.lineWidth = 2;
        ctx.beginPath(); ctx.arc(cp.x, cp.y, r + 4, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }

      ctx.save();
      if (lm.isAiPlaced) {
        ctx.fillStyle = color + '55';
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
      } else {
        ctx.fillStyle = color;
      }
      ctx.beginPath(); ctx.arc(cp.x, cp.y, r, 0, Math.PI * 2);
      ctx.fill();
      if (lm.isAiPlaced) ctx.stroke();

      // Label
      ctx.fillStyle = '#FFFFFF';
      ctx.font = `bold ${isSel ? 10 : 9}px monospace`;
      ctx.textAlign = 'left';
      ctx.shadowColor = 'rgba(0,0,0,0.8)';
      ctx.shadowBlur = 3;
      ctx.fillText(lm.key, cp.x + r + 3, cp.y + 4);
      ctx.shadowBlur = 0;

      // AI confidence bar
      if (lm.isAiPlaced && lm.confidence !== undefined) {
        const bW = 14;
        ctx.fillStyle = '#1E293B';
        ctx.fillRect(cp.x - bW / 2, cp.y + r + 2, bW, 2);
        ctx.fillStyle = lm.confidence > 0.85 ? '#4ADE80' : lm.confidence > 0.70 ? '#FBBF24' : '#F87171';
        ctx.fillRect(cp.x - bW / 2, cp.y + r + 2, bW * lm.confidence, 2);
      }
      ctx.restore();
    });

    // Measurement value labels (SNA/SNB/ANB/FMA/IMPA) near their vertices,
    // colored by classification: green normal / yellow mild / red severe.
    if (showMeasurements) {
      MEASUREMENT_LABELS.forEach(({ name, anchor, dx, dy }) => {
        const m = measMap[name];
        if (!m || m.value === null) return;
        const lm = lmMap[anchor]; if (!lm) return;
        const cp = T.tc(lm.x, lm.y);
        const color = SEVERITY_COLOR[m.severity] ?? SEVERITY_COLOR.normal;
        const text = `${name} ${m.value}${m.unit}`;
        ctx.save();
        ctx.font = 'bold 11px monospace';
        const w = ctx.measureText(text).width + 10;
        const h = 16;
        const x = dx >= 0 ? cp.x + dx : cp.x + dx - w;
        const y = cp.y + dy - h / 2;
        ctx.fillStyle = 'rgba(15,23,42,0.85)';
        ctx.fillRect(x, y, w, h);
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.strokeRect(x, y, w, h);
        ctx.fillStyle = color;
        ctx.textAlign = 'left';
        ctx.fillText(text, x + 5, y + 12);
        ctx.restore();
      });
    }

    // Calibration ruler line + points
    if (calPoints.length > 0) {
      const a = calPoints[0];
      const b = calPoints.length === 2 ? calPoints[1] : (calMode ? calCursor : null);
      if (b) {
        const ca = T.tc(a.x, a.y), cb = T.tc(b.x, b.y);
        const pxLen = Math.hypot(b.x - a.x, b.y - a.y); // image-space pixels
        ctx.save();
        ctx.strokeStyle = '#FBBF24';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([6, 4]);
        ctx.beginPath(); ctx.moveTo(ca.x, ca.y); ctx.lineTo(cb.x, cb.y); ctx.stroke();
        ctx.setLineDash([]);
        ctx.fillStyle = '#FBBF24';
        ctx.font = 'bold 11px monospace';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'rgba(0,0,0,0.9)';
        ctx.shadowBlur = 3;
        ctx.fillText(`${Math.round(pxLen)} px`, (ca.x + cb.x) / 2, (ca.y + cb.y) / 2 - 8);
        ctx.restore();
      }
      ctx.save();
      calPoints.forEach(p => {
        const cp = T.tc(p.x, p.y);
        ctx.strokeStyle = '#FBBF24';
        ctx.lineWidth = 1.5;
        ctx.beginPath(); ctx.arc(cp.x, cp.y, 5, 0, Math.PI * 2); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(cp.x - 8, cp.y); ctx.lineTo(cp.x + 8, cp.y);
        ctx.moveTo(cp.x, cp.y - 8); ctx.lineTo(cp.x, cp.y + 8);
        ctx.stroke();
      });
      ctx.restore();
    }

    // Transient viewer rulers. Points remain in original image space so the
    // result is stable while the preview is rotated, flipped, zoomed or panned.
    if (viewerTool && viewerPoints.length > 0) {
      const required = viewerTool === 'distance' ? 2 : 3;
      const previewPoints = viewerPoints.length < required && viewerCursor
        ? [...viewerPoints, viewerCursor]
        : viewerPoints;
      const canvasPoints = previewPoints.map(point => T.tc(point.x, point.y));
      ctx.save();
      ctx.strokeStyle = '#22D3EE';
      ctx.fillStyle = '#22D3EE';
      ctx.lineWidth = 2;
      ctx.setLineDash(viewerPoints.length < required ? [5, 4] : []);
      ctx.beginPath();
      canvasPoints.forEach((point, index) => {
        if (index === 0) ctx.moveTo(point.x, point.y);
        else ctx.lineTo(point.x, point.y);
      });
      ctx.stroke();
      ctx.setLineDash([]);
      viewerPoints.forEach(point => {
        const cp = T.tc(point.x, point.y);
        ctx.beginPath();
        ctx.arc(cp.x, cp.y, 4.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = '#ECFEFF';
        ctx.lineWidth = 1;
        ctx.stroke();
        ctx.strokeStyle = '#22D3EE';
        ctx.lineWidth = 2;
      });

      let label: string | null = null;
      let labelPoint: Pt | null = null;
      if (viewerTool === 'distance' && viewerPoints.length === 2) {
        const result = viewerDistance(viewerPoints[0], viewerPoints[1], pixelsPerMm);
        label = result.millimeters === null
          ? `${result.pixels.toFixed(1)} px (غير معاير)`
          : `${result.millimeters.toFixed(2)} mm`;
        const a = T.tc(viewerPoints[0].x, viewerPoints[0].y);
        const b = T.tc(viewerPoints[1].x, viewerPoints[1].y);
        labelPoint = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 - 12 };
      } else if (viewerTool === 'angle' && viewerPoints.length === 3) {
        label = `${viewerAngle(viewerPoints[0], viewerPoints[1], viewerPoints[2]).toFixed(1)}°`;
        const vertex = T.tc(viewerPoints[1].x, viewerPoints[1].y);
        labelPoint = { x: vertex.x + 14, y: vertex.y - 14 };
      }

      if (label && labelPoint) {
        ctx.font = 'bold 12px monospace';
        const width = ctx.measureText(label).width + 12;
        ctx.fillStyle = 'rgba(8,47,73,0.92)';
        ctx.fillRect(labelPoint.x - width / 2, labelPoint.y - 13, width, 20);
        ctx.strokeStyle = '#22D3EE';
        ctx.lineWidth = 1;
        ctx.strokeRect(labelPoint.x - width / 2, labelPoint.y - 13, width, 20);
        ctx.fillStyle = '#CFFAFE';
        ctx.textAlign = 'center';
        ctx.fillText(label, labelPoint.x, labelPoint.y + 2);
      }
      ctx.restore();
    }

    // Selected crosshair
    if (selectedKey && !lmMap[selectedKey] && !calMode && !viewerTool) {
      ctx.save();
      ctx.strokeStyle = '#FBBF24';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      ctx.beginPath(); ctx.moveTo(0, c.height / 2); ctx.lineTo(c.width, c.height / 2); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(c.width / 2, 0); ctx.lineTo(c.width / 2, c.height); ctx.stroke();
      ctx.restore();
    }
  }, [landmarks, lmMap, measMap, img, imgError, imageUrl, selectedKey, hovered, showPlanes, showTracing, showSimulation,
      simulationScenario, showMeasurements, calMode, calPoints, calCursor, viewerTool,
      viewerPoints, viewerCursor, pixelsPerMm, getT, imageAdjustments]);

  useEffect(() => { draw(); }, [draw]);

  // Resize observer
  useEffect(() => {
    const el = containerRef.current; if (!el) return;
    const obs = new ResizeObserver(() => {
      const c = canvasRef.current; if (!c) return;
      c.width  = el.clientWidth;
      c.height = el.clientHeight;
      setCanvasSize({ w: el.clientWidth, h: el.clientHeight });
      draw();
    });
    obs.observe(el);
    return () => obs.disconnect();
  }, [draw]);

  // Wheel zoom toward the cursor. Native listener with passive:false because
  // React delegates wheel events as passive (preventDefault would be ignored).
  useEffect(() => {
    const c = canvasRef.current; if (!c) return;
    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const r = c.getBoundingClientRect();
      const cx = (e.clientX - r.left) * (c.width / r.width);
      const cy = (e.clientY - r.top)  * (c.height / r.height);
      zoomAt(cx, cy, e.deltaY < 0 ? 1.15 : 1 / 1.15);
    };
    c.addEventListener('wheel', onWheel, { passive: false });
    return () => c.removeEventListener('wheel', onWheel);
  }, [zoomAt]);

  // Keyboard: +/- zoom, 0 reset-fit, Space = temporary pan, Escape exits a ruler.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement | null;
      if (t && (['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName) || t.isContentEditable)) return;
      if (e.key === ' ') {
        if (t?.tagName !== 'BUTTON') { setSpaceHeld(true); e.preventDefault(); }
        return;
      }
      if (e.key === '+' || e.key === '=') { centerZoom(1.2); e.preventDefault(); }
      else if (e.key === '-' || e.key === '_') { centerZoom(1 / 1.2); e.preventDefault(); }
      else if (e.key === '0') { resetFit(); }
      else if (e.key === 'Escape' && calMode) { setCalMode(false); setCalCursor(null); }
      else if (e.key === 'Escape' && viewerTool) {
        setViewerTool(null);
        setViewerPoints([]);
        setViewerCursor(null);
      }
    };
    const onKeyUp = (e: KeyboardEvent) => { if (e.key === ' ') setSpaceHeld(false); };
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
    };
  }, [centerZoom, resetFit, calMode, viewerTool]);

  const coords = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const c = canvasRef.current!;
    const r = c.getBoundingClientRect();
    return {
      cx: (e.clientX - r.left) * (c.width / r.width),
      cy: (e.clientY - r.top)  * (c.height / r.height),
    };
  };

  const hitLandmark = (cx: number, cy: number) => {
    const T = getT(); if (!T) return null;
    return landmarks.slice().reverse().find(lm => {
      const cp = T.tc(lm.x, lm.y);
      return Math.hypot(cx - cp.x, cy - cp.y) < HIT_RADIUS;
    })?.key ?? null;
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const { cx, cy } = coords(e);
    const T = getT(); if (!T) return;

    // Pan: middle button, space held, or pan-mode toggle
    if (e.button === 1 || panMode || spaceHeld) {
      e.preventDefault();
      if (!view) setView({ s: T.s, ox: T.ox, oy: T.oy });
      panRef.current = { sx: cx, sy: cy, ox: T.ox, oy: T.oy };
      return;
    }

    // Right-click on an AI-placed landmark opens the refine context menu.
    // (Suppressed when no refine callback is wired up.)
    if (e.button === 2) {
      if (!onRefineLandmark) return;
      const hit = hitLandmark(cx, cy);
      if (hit && lmMap[hit]?.isAiPlaced) {
        e.preventDefault();
        const rect = canvasRef.current!.getBoundingClientRect();
        setCtxMenu({
          key: hit,
          x: e.clientX - rect.left,
          y: e.clientY - rect.top,
        });
      }
      return;
    }

    if (e.button !== 0) return;

    // Long-press on touch devices: start a timer that, if not cancelled by a
    // drag/pointer-up, opens the same refine context menu for the touched
    // landmark. Mirrors the right-click path for tablets (orthodontists often
    // use touch screens during ceph review).
    if (onRefineLandmark && e.detail === 1) {
      const hit = hitLandmark(cx, cy);
      if (hit && lmMap[hit]?.isAiPlaced) {
        if (longPressTimer.current !== null) window.clearTimeout(longPressTimer.current);
        longPressTimer.current = window.setTimeout(() => {
          const rect = canvasRef.current!.getBoundingClientRect();
          setCtxMenu(prev => prev ?? {
            key: hit,
            x: rect.left + cx * (rect.width / canvasRef.current!.width),
            y: rect.top + cy * (rect.height / canvasRef.current!.height),
          });
        }, 550);
      }
    }

    // Calibration: place the two ruler points (third click restarts)
    if (calMode) {
      const ip = T.ti(cx, cy);
      setCalPoints(prev => prev.length >= 2 ? [{ x: ip.x, y: ip.y }] : [...prev, { x: ip.x, y: ip.y }]);
      return;
    }

    if (viewerTool) {
      const ip = T.ti(cx, cy);
      if (ip.x < 0 || ip.y < 0 || ip.x > T.iW || ip.y > T.iH) return;
      const required = viewerTool === 'distance' ? 2 : 3;
      setViewerPoints(prev => prev.length >= required ? [ip] : [...prev, ip]);
      return;
    }

    const hit = hitLandmark(cx, cy);
    if (hit) {
      setDragging(hit); onSelectKey(hit);
    } else if (selectedKey) {
      const ip = T.ti(cx, cy);
      const def = LANDMARK_DEFS[selectedKey];
      if (!def) return;
      const existing = lmMap[selectedKey];
      if (existing) {
        onLandmarksChange(landmarks.map(l => l.key === selectedKey ? {
          ...l,
          x: ip.x,
          y: ip.y,
          isAiPlaced: false,
          isReviewed: true,
        } : l));
      } else {
        onLandmarksChange([...landmarks, {
          key: selectedKey, name: selectedKey,
          nameAr: def.nameAr, x: ip.x, y: ip.y,
          isAiPlaced: false,
          placementSource: 'manual',
          isReviewed: true,
          group: def.group as CephLandmark['group'],
        }]);
      }
    }
  };

  const handleMouseUp = () => {
    setDragging(null);
    panRef.current = null;
    if (longPressTimer.current !== null) {
      window.clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const { cx, cy } = coords(e);
    const T = getT(); if (!T) return;

    if (panRef.current) {
      const pan = panRef.current;
      setView(v => v ? { ...v, ox: pan.ox + (cx - pan.sx), oy: pan.oy + (cy - pan.sy) } : v);
      if (canvasRef.current) canvasRef.current.style.cursor = 'grabbing';
      return;
    }

    if (calMode) {
      if (calPoints.length === 1) setCalCursor(T.ti(cx, cy));
      if (canvasRef.current) canvasRef.current.style.cursor = 'crosshair';
      return;
    }

    if (viewerTool) {
      const ip = T.ti(cx, cy);
      setViewerCursor(ip.x >= 0 && ip.y >= 0 && ip.x <= T.iW && ip.y <= T.iH ? ip : null);
      setHovered(null);
      if (canvasRef.current) canvasRef.current.style.cursor = 'crosshair';
      return;
    }

    const hit = hitLandmark(cx, cy);
    setHovered(hit);
    if (dragging) {
      const ip = T.ti(cx, cy);
      onLandmarksChange(landmarks.map(l => l.key === dragging ? {
        ...l,
        x: ip.x,
        y: ip.y,
        isAiPlaced: false,
        isReviewed: true,
      } : l));
      // Dragging cancels any pending long-press — the user wants to move, not refine.
      if (longPressTimer.current !== null) {
        window.clearTimeout(longPressTimer.current);
        longPressTimer.current = null;
      }
    }
    if (canvasRef.current) {
      canvasRef.current.style.cursor =
        (panMode || spaceHeld) ? 'grab' :
        dragging ? 'grabbing' :
        hit ? 'grab' :
        selectedKey ? 'crosshair' : 'default';
    }
  };

  // ── Toolbar / panel derived values ──
  const iW = imageWidth  || img?.naturalWidth  || 0;
  const iH = imageHeight || img?.naturalHeight || 0;
  const fitS = (iW > 0 && iH > 0 && canvasSize.w > 0 && canvasSize.h > 0)
    ? Math.min(canvasSize.w / iW, canvasSize.h / iH)
    : 0;
  const zoomPct = fitS > 0 ? Math.round(((view?.s ?? fitS) / fitS) * 100) : 100;

  const calPxLen = calPoints.length === 2
    ? Math.hypot(calPoints[1].x - calPoints[0].x, calPoints[1].y - calPoints[0].y)
    : 0;

  const applyCalibration = () => {
    const mm = parseFloat(calMm);
    if (!onCalibrate || !(mm > 0) || !(calPxLen > 0)) return;
    onCalibrate(+(calPxLen / mm).toFixed(3));
    setCalMode(false);
    setCalCursor(null);
  };

  const toggleViewerTool = (tool: 'distance' | 'angle') => {
    setCalMode(false);
    setCalCursor(null);
    setViewerCursor(null);
    setViewerTool(viewerTool === tool ? null : tool);
    setViewerPoints([]);
  };

  const updateViewerTransform = (update: (current: ViewerTransform) => ViewerTransform) => {
    setViewerTransform(current => update(current));
    setView(null);
  };

  const resetViewer = () => {
    setView(null);
    setPanMode(false);
    setCalMode(false);
    setCalCursor(null);
    setCalPoints([]);
    setViewerTool(null);
    setViewerPoints([]);
    setViewerCursor(null);
    setViewerTransform(DEFAULT_VIEWER_TRANSFORM);
    onResetImageAdjustments?.();
  };

  const hasTemporaryView =
    view !== null ||
    viewerPoints.length > 0 ||
    viewerTool !== null ||
    !isDefaultViewerTransform(viewerTransform) ||
    imageAdjustments.brightness !== 100 ||
    imageAdjustments.contrast !== 100 ||
    imageAdjustments.inverted;

  const toolBtn = "p-1.5 rounded-md text-gray-300 hover:bg-white/15 hover:text-white transition";

  return (
    <div ref={containerRef} className="relative w-full h-full bg-gray-950 rounded-lg overflow-hidden select-none">
      <canvas
        ref={canvasRef}
        className="w-full h-full"
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={() => { handleMouseUp(); setViewerCursor(null); }}
        // Suppress the browser's default right-click menu on the canvas so the
        // AI-refine context menu is the only one the orthodontist sees.
        onContextMenu={e => { if (onRefineLandmark) e.preventDefault(); }}
      />

      {/* ── Toolbar overlay (top-left, fixed position regardless of RTL) ── */}
      <div className="absolute top-3 left-3 z-10 flex items-center gap-0.5 bg-black/70 backdrop-blur-sm rounded-lg p-1" dir="ltr">
        <button type="button" onClick={() => centerZoom(1.2)} className={toolBtn} title="تكبير (+)" aria-label="تكبير">
          <ZoomIn className="w-3.5 h-3.5" />
        </button>
        <button type="button" onClick={() => centerZoom(1 / 1.2)} className={toolBtn} title="تصغير (-)" aria-label="تصغير">
          <ZoomOut className="w-3.5 h-3.5" />
        </button>
        <span className="text-[10px] text-gray-200 font-mono w-11 text-center tabular-nums">{zoomPct}%</span>
        <button type="button" onClick={resetFit}
          className="px-1.5 py-1 rounded-md text-[10px] font-semibold text-gray-300 hover:bg-white/15 hover:text-white transition"
          title="ملاءمة الصورة للإطار (0)">
          ملاءمة
        </button>
        <span className="w-px h-4 bg-white/20 mx-0.5" />
        <button type="button" onClick={() => setPanMode(p => !p)}
          className={cn(toolBtn, panMode && "bg-white/25 text-white")}
          title="وضع التحريك (أو اسحب بالزر الأوسط / مع مفتاح المسافة)" aria-pressed={panMode}>
          <Hand className="w-3.5 h-3.5" />
        </button>
        <button type="button"
          onClick={() => {
            setCalMode(m => !m);
            setCalCursor(null);
            setViewerTool(null);
            setViewerCursor(null);
          }}
          className={cn(
            "flex items-center gap-1 px-1.5 py-1 rounded-md text-[10px] font-semibold transition",
            calMode ? "bg-amber-400/90 text-gray-900" : "text-gray-300 hover:bg-white/15 hover:text-white"
          )}
          title="معايرة بنقطتين على مسطرة الصورة" aria-pressed={calMode}>
          <Ruler className="w-3.5 h-3.5" />
          معايرة
        </button>
      </div>

      {/* Viewer-only tools: no source image, landmark or analysis mutation. */}
      <div className="absolute top-12 left-3 z-10 flex items-center gap-0.5 bg-black/70 backdrop-blur-sm rounded-lg p-1" dir="ltr">
        <button
          type="button"
          onClick={() => toggleViewerTool('distance')}
          className={cn(toolBtn, viewerTool === 'distance' && "bg-cyan-400/25 text-cyan-200")}
          title="قياس مسافة مؤقتة"
          aria-label="قياس مسافة مؤقتة"
          aria-pressed={viewerTool === 'distance'}
        >
          <Ruler className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={() => toggleViewerTool('angle')}
          className={cn(toolBtn, viewerTool === 'angle' && "bg-cyan-400/25 text-cyan-200")}
          title="قياس زاوية مؤقتة"
          aria-label="قياس زاوية مؤقتة"
          aria-pressed={viewerTool === 'angle'}
        >
          <DraftingCompass className="w-3.5 h-3.5" />
        </button>
        <span className="w-px h-4 bg-white/20 mx-0.5" />
        <button
          type="button"
          onClick={() => updateViewerTransform(current => ({
            ...current,
            rotation: ((current.rotation + 90) % 360) as ViewerTransform['rotation'],
          }))}
          className={toolBtn}
          title="تدوير العرض 90 درجة"
          aria-label="تدوير العرض 90 درجة"
        >
          <RotateCw className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={() => updateViewerTransform(current => ({ ...current, flipHorizontal: !current.flipHorizontal }))}
          className={cn(toolBtn, viewerTransform.flipHorizontal && "bg-white/25 text-white")}
          title="قلب العرض أفقياً"
          aria-label="قلب العرض أفقياً"
          aria-pressed={viewerTransform.flipHorizontal}
        >
          <FlipHorizontal className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={() => updateViewerTransform(current => ({ ...current, flipVertical: !current.flipVertical }))}
          className={cn(toolBtn, viewerTransform.flipVertical && "bg-white/25 text-white")}
          title="قلب العرض عمودياً"
          aria-label="قلب العرض عمودياً"
          aria-pressed={viewerTransform.flipVertical}
        >
          <FlipVertical className="w-3.5 h-3.5" />
        </button>
        <span className="w-px h-4 bg-white/20 mx-0.5" />
        <button
          type="button"
          onClick={() => setViewerPoints([])}
          disabled={viewerPoints.length === 0}
          className={cn(toolBtn, "disabled:opacity-35 disabled:cursor-not-allowed")}
          title="مسح قياسات العارض"
          aria-label="مسح قياسات العارض"
        >
          <Trash2 className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={resetViewer}
          className={toolBtn}
          title="إعادة ضبط العارض بالكامل"
          aria-label="إعادة ضبط العارض بالكامل"
        >
          <RotateCcw className="w-3.5 h-3.5" />
        </button>
      </div>

      {hasTemporaryView && (
        <div className="absolute top-3 right-3 z-10 rounded border border-cyan-300/30 bg-cyan-950/85 px-2 py-1 text-[10px] font-semibold text-cyan-100">
          عرض مؤقت · غير محفوظ
        </div>
      )}

      {/* ── Calibration panel ── */}
      {calMode && (
        <div className="absolute top-24 left-3 z-10 w-60 bg-black/80 backdrop-blur-sm rounded-lg p-2.5 space-y-2">
          <p className="text-[10px] font-semibold text-amber-300">
            وضع المعايرة: انقر نقطتين على مسطرة صورة الأشعة
          </p>
          {calPoints.length === 2 ? (
            <>
              <p className="text-[10px] text-gray-200 font-mono" dir="ltr">
                {Math.round(calPxLen)} px
              </p>
              <div className="flex items-center gap-1.5">
                <label className="text-[10px] text-gray-300 whitespace-nowrap" htmlFor="ceph-cal-mm">
                  المسافة الحقيقية (مم)
                </label>
                <input
                  id="ceph-cal-mm"
                  type="number"
                  min={0.1}
                  step={0.1}
                  value={calMm}
                  onChange={e => setCalMm(e.target.value)}
                  className="w-14 text-[10px] px-1.5 py-0.5 rounded bg-white/10 border border-white/20 text-white focus:outline-none focus:ring-1 focus:ring-amber-400"
                  dir="ltr"
                />
              </div>
              <div className="flex items-center gap-1.5">
                <button type="button" onClick={applyCalibration}
                  disabled={!(parseFloat(calMm) > 0)}
                  className="flex-1 px-2 py-1 rounded-md bg-amber-400 text-gray-900 text-[10px] font-bold hover:bg-amber-300 disabled:opacity-50 transition">
                  تطبيق المعايرة
                </button>
                <button type="button" onClick={() => setCalPoints([])}
                  className="px-2 py-1 rounded-md border border-white/25 text-gray-300 text-[10px] hover:bg-white/10 transition">
                  مسح النقاط
                </button>
              </div>
            </>
          ) : (
            <p className="text-[10px] text-gray-400">
              {calPoints.length === 0 ? 'النقطة الأولى: بداية المسافة المعلومة' : 'النقطة الثانية: نهاية المسافة المعلومة'}
            </p>
          )}
          <p className="text-[9px] text-gray-500">Esc للخروج من وضع المعايرة</p>
        </div>
      )}

      {hovered && !calMode && !viewerTool && (
        <div className="absolute bottom-3 left-3 bg-black/80 text-white text-xs px-2.5 py-1.5 rounded-lg pointer-events-none flex items-center gap-1.5 max-w-[80%]">
          <span className="w-2 h-2 rounded-full inline-block" style={{ backgroundColor: LANDMARK_DEFS[hovered]?.color ?? "#38BDF8" }} />
          {LANDMARK_DEFS[hovered]?.nameAr
            ?? lmMap[hovered]?.sourceLandmarkKey
            ?? lmMap[hovered]?.nameAr
            ?? lmMap[hovered]?.name
            ?? hovered}
          {lmMap[hovered]?.placementSource === "ai" && (
            <span className={lmMap[hovered]?.isReviewed ? "text-emerald-300" : "text-purple-300"}>
              · AI {lmMap[hovered]?.isReviewed ? "مراجع" : `${Math.round((lmMap[hovered]?.confidence ?? 0) * 100)}%`}
            </span>
          )}
          {lmMap[hovered]?.placementSource === "webceph-import" && (
            <span className={lmMap[hovered]?.isReviewed ? "text-emerald-300" : "text-cyan-300"}>
              · WebCeph {lmMap[hovered]?.isReviewed ? "مراجع" : "يحتاج مراجعة"}
            </span>
          )}
          {lmMap[hovered]?.reasoning && (
            <span className="text-slate-300 truncate" title={lmMap[hovered]?.reasoning}>
              · {lmMap[hovered]?.reasoning}
            </span>
          )}
        </div>
      )}

      {/* ── AI-refine context menu (right-click / long-press on an AI-placed
              landmark). Calls onRefineLandmark(key); the page posts to
              /api/ceph/{id}/ai/refine-landmark and replaces the point. ── */}
      {ctxMenu && onRefineLandmark && (
        <>
          <div
            className="fixed inset-0 z-20"
            onClick={() => setCtxMenu(null)}
            onContextMenu={e => { e.preventDefault(); setCtxMenu(null); }}
            aria-hidden="true"
          />
          <div
            className="absolute z-30 min-w-[15rem] rounded-md border border-violet-200 bg-white py-1 shadow-lg"
            style={{ left: ctxMenu.x, top: ctxMenu.y }}
          >
            <div className="border-b border-violet-100 px-3 py-1.5 text-[10px] font-bold text-violet-700">
              {LANDMARK_DEFS[ctxMenu.key]?.nameAr ?? ctxMenu.key} · نقطة AI
            </div>
            <button
              type="button"
              disabled={refining}
              onClick={() => {
                const key = ctxMenu.key;
                setCtxMenu(null);
                onRefineLandmark(key);
              }}
              className="flex w-full items-center gap-2 px-3 py-2 text-start text-xs font-medium text-violet-800 hover:bg-violet-50 disabled:opacity-50 disabled:cursor-not-allowed transition"
            >
              {refining ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Brain className="w-3.5 h-3.5" />}
              تحسين موضع النقطة بالذكاء الاصطناعي
            </button>
            <p className="px-3 py-1 text-[9px] text-gray-400">
              مسودة AI — تتطلب مراجعة أخصائي التقويم قبل الحفظ
            </p>
          </div>
        </>
      )}
    </div>
  );
}

export { LANDMARK_ORDER };
