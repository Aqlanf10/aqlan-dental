"use client";
import { useRef, useEffect, useState, useCallback, useMemo } from "react";
import * as fabric from "fabric";
import type { CephLandmark } from "@/types/ceph";

// ─── Constants ────────────────────────────────────────────────────────────────

const MIN_ZOOM = 0.25;
const MAX_ZOOM = 5.0;
const ZOOM_STEP = 0.1;
const LANDMARK_RADIUS = 6;
const AI_LANDMARK_RADIUS = 7;
// HIT_TOLERANCE used for Fabric.js object selection tolerance
const PLANE_EXTENSION_FACTOR = 4;

// ─── Landmark Definitions ─────────────────────────────────────────────────────

export const LANDMARK_DEFS: Record<string, { nameAr: string; group: string; color: string }> = {
  S:   { nameAr: 'السرج',           group: 'cranial',  color: '#60A5FA' },
  N:   { nameAr: 'الناسيون',        group: 'cranial',  color: '#60A5FA' },
  Or:  { nameAr: 'قاع المدار',       group: 'cranial',  color: '#A78BFA' },
  Po:  { nameAr: 'قمة المسمع',       group: 'cranial',  color: '#A78BFA' },
  ANS: { nameAr: 'الشوكة الأمامية',  group: 'maxilla',  color: '#FB923C' },
  PNS: { nameAr: 'الشوكة الخلفية',  group: 'maxilla',  color: '#FB923C' },
  A:   { nameAr: 'النقطة A',        group: 'maxilla',  color: '#FB923C' },
  B:   { nameAr: 'النقطة B',        group: 'mandible', color: '#F87171' },
  Pog: { nameAr: 'الذقن البارز',     group: 'mandible', color: '#F87171' },
  Gn:  { nameAr: 'الذقن',           group: 'mandible', color: '#F87171' },
  Me:  { nameAr: 'الأسفل',         group: 'mandible', color: '#F87171' },
  Go:  { nameAr: 'زاوية الفك',       group: 'mandible', color: '#F87171' },
  Co:  { nameAr: 'رأس اللقمة',      group: 'mandible', color: '#EF4444' },
  Ar:  { nameAr: 'المفصل',         group: 'mandible', color: '#EF4444' },
  D:   { nameAr: 'النقطة D',        group: 'mandible', color: '#F87171' },
  Pm:  { nameAr: 'بروز الذقن',       group: 'mandible', color: '#F87171' },
  U1T: { nameAr: 'طرف القاطع ع',    group: 'dental',   color: '#34D399' },
  U1A: { nameAr: 'قمة القاطع ع',    group: 'dental',   color: '#34D399' },
  L1T: { nameAr: 'طرف القاطع س',    group: 'dental',   color: '#10B981' },
  L1A: { nameAr: 'قمة القاطع س',    group: 'dental',   color: '#10B981' },
  LS:  { nameAr: 'الشفة العلوية',   group: 'soft',     color: '#F472B6' },
  LI:  { nameAr: 'الشفة السفلية',   group: 'soft',     color: '#F472B6' },
  Pn:  { nameAr: 'طرف الأنف',       group: 'soft',     color: '#F472B6' },
  Cm:  { nameAr: 'قاعدة الأنف',     group: 'soft',     color: '#F472B6' },
};

export const LANDMARK_ORDER = [
  'S','N','Or','Po','ANS','PNS','A','B','Pog','Gn',
  'Me','Go','Co','Ar','D','Pm','U1T','U1A','L1T','L1A',
  'LS','LI','Pn','Cm',
];

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
];

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

// ─── Canvas Modes ─────────────────────────────────────────────────────────────

type CanvasMode = 'landmark' | 'pan' | 'calibrate';

// ─── Undo History ─────────────────────────────────────────────────────────────

interface UndoEntry {
  landmarks: CephLandmark[];
  description: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface CephCanvasExportRef {
  getDataUrl: () => string | null;
}

interface Props {
  imageUrl: string | null;
  imageWidth: number;
  imageHeight: number;
  landmarks: CephLandmark[];
  onLandmarksChange: (lm: CephLandmark[]) => void;
  selectedKey: string | null;
  onSelectKey: (key: string | null) => void;
  showPlanes: boolean;
  showSimulation: boolean;
  simulationScenario: string;
  /** Optional ref to expose canvas export method */
  exportRef?: React.MutableRefObject<CephCanvasExportRef | null>;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function CephCanvas({
  imageUrl, imageWidth, imageHeight, landmarks, onLandmarksChange,
  selectedKey, onSelectKey, showPlanes, showSimulation, simulationScenario,
  exportRef,
}: Props) {
  // Refs
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasElRef  = useRef<HTMLCanvasElement>(null);
  const fabricRef    = useRef<fabric.Canvas | null>(null);
  const bgImageRef   = useRef<fabric.FabricImage | null>(null);
  const landmarkObjectsRef = useRef<Map<string, fabric.Group>>(new Map());
  const planeObjectsRef    = useRef<fabric.Object[]>([]);
  const simObjectsRef      = useRef<fabric.Object[]>([]);
  const calibrationRef     = useRef<{ line: fabric.Line | null; pt1: fabric.Circle | null; pt2: fabric.Circle | null; label: fabric.Text | null }>({
    line: null, pt1: null, pt2: null, label: null,
  });
  const isPanningRef    = useRef(false);
  const lastPanPosRef   = useRef({ x: 0, y: 0 });
  const ignoreNextMouseUp = useRef(false);

  // State
  const [mode, setMode]                 = useState<CanvasMode>('landmark');
  const [zoomLevel, setZoomLevel]       = useState(100);
  const [calibStep, setCalibStep]       = useState<0 | 1 | 2>(0);
  const [calibP1, setCalibP1]           = useState<{ x: number; y: number } | null>(null);
  const [pixelsPerMm, setPixelsPerMm]   = useState<number | null>(null);
  const [hoveredKey, setHoveredKey]     = useState<string | null>(null);
  const [undoStack, setUndoStack]       = useState<UndoEntry[]>([]);
  const [redoStack, setRedoStack]       = useState<UndoEntry[]>([]);

  // Landmark map
  const lmMap = useMemo(() => {
    const m: Record<string, CephLandmark> = {};
    landmarks.forEach(l => { m[l.key] = l; });
    return m;
  }, [landmarks]);

  // ─── Undo / Redo Helpers ─────────────────────────────────────────────────

  const pushUndo = useCallback((description: string) => {
    setUndoStack(prev => [...prev.slice(-49), { landmarks: [...landmarks], description }]);
    setRedoStack([]);
  }, [landmarks]);

  const handleUndo = useCallback(() => {
    setUndoStack(prev => {
      if (prev.length === 0) return prev;
      const entry = prev[prev.length - 1];
      setRedoStack(r => [...r, { landmarks: [...landmarks], description: '' }]);
      onLandmarksChange(entry.landmarks);
      return prev.slice(0, -1);
    });
  }, [landmarks, onLandmarksChange]);

  const handleRedo = useCallback(() => {
    setRedoStack(prev => {
      if (prev.length === 0) return prev;
      const entry = prev[prev.length - 1];
      setUndoStack(u => [...u, { landmarks: [...landmarks], description: '' }]);
      onLandmarksChange(entry.landmarks);
      return prev.slice(0, -1);
    });
  }, [landmarks, onLandmarksChange]);

  // ─── Keyboard shortcuts ──────────────────────────────────────────────────

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'z') {
        e.preventDefault();
        if (e.shiftKey) handleRedo(); else handleUndo();
      }
      if ((e.ctrlKey || e.metaKey) && e.key === 'y') {
        e.preventDefault();
        handleRedo();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [handleUndo, handleRedo]);

  // ─── Initialize Fabric Canvas ────────────────────────────────────────────

  useEffect(() => {
    if (!canvasElRef.current || !containerRef.current) return;

    const container = containerRef.current;
    const canvasEl = canvasElRef.current;

    const fc = new fabric.Canvas(canvasEl, {
      width: container.clientWidth,
      height: container.clientHeight,
      backgroundColor: '#0F172A',
      selection: false,
      preserveObjectStacking: true,
    });

    fabricRef.current = fc;

    // ── Zoom with mouse wheel ──
    fc.on('mouse:wheel', (opt) => {
      const e = opt.e as WheelEvent;
      const delta = e.deltaY;
      let zoom = fc.getZoom();
      zoom *= 0.999 ** delta;
      zoom = Math.min(Math.max(zoom, MIN_ZOOM), MAX_ZOOM);
      fc.zoomToPoint(new fabric.Point(e.offsetX, e.offsetY), zoom);
      setZoomLevel(Math.round(zoom * 100));
      e.preventDefault();
      e.stopPropagation();
      renderOverlays();
    });

    // ── Double-click to zoom 200% at point ──
    fc.on('mouse:dblclick', (opt) => {
      const e = opt.e as MouseEvent;
      if (mode === 'landmark') {
        const pointer = fc.getScenePoint(opt.e);
        if (pointer) {
          fc.zoomToPoint(new fabric.Point(e.offsetX, e.offsetY), 2.0);
          setZoomLevel(200);
          renderOverlays();
        }
      }
    });

    // ── Pan handling ──
    fc.on('mouse:down', (opt) => {
      const e = opt.e as MouseEvent;
      if (mode === 'pan' || e.altKey || e.button === 1) {
        isPanningRef.current = true;
        lastPanPosRef.current = { x: e.clientX, y: e.clientY };
        fc.selection = false;
        fc.setCursor('grabbing');
      }
    });

    fc.on('mouse:move', (opt) => {
      const e = opt.e as MouseEvent;
      if (isPanningRef.current) {
        const dx = e.clientX - lastPanPosRef.current.x;
        const dy = e.clientY - lastPanPosRef.current.y;
        lastPanPosRef.current = { x: e.clientX, y: e.clientY };
        const vpt = fc.viewportTransform!;
        vpt[4] += dx;
        vpt[5] += dy;
        fc.requestRenderAll();
      }
    });

    fc.on('mouse:up', () => {
      isPanningRef.current = false;
      if (mode === 'pan') {
        fc.setCursor('grab');
      }
    });

    // ── Landmark placement & calibration ──
    fc.on('mouse:up', (opt) => {
      if (ignoreNextMouseUp.current) {
        ignoreNextMouseUp.current = false;
        return;
      }
      if (isPanningRef.current) return;

      const target = opt.target as fabric.Object | undefined;
      const customProps = target as fabric.Object & { _landmarkKey?: string };

      // If we clicked on a landmark, select it
      if (customProps?._landmarkKey) {
        onSelectKey(customProps._landmarkKey);
        return;
      }

      // Calibration mode
      if (mode === 'calibrate') {
        const pointer = fc.getScenePoint(opt.e);
        if (!pointer) return;

        if (calibStep === 0) {
          setCalibP1({ x: pointer.x, y: pointer.y });
          setCalibStep(1);
          drawCalibrationPoint(pointer.x, pointer.y, 1);
        } else if (calibStep === 1) {
          const dist = Math.hypot(pointer.x - calibP1!.x, pointer.y - calibP1!.y);
          const mmStr = prompt('أدخل المسافة الحقيقية بالملم:');
          if (mmStr) {
            const mm = parseFloat(mmStr);
            if (mm > 0 && dist > 0) {
              const ppm = dist / mm;
              setPixelsPerMm(Math.round(ppm * 1000) / 1000);
            }
          }
          setCalibStep(0);
          setCalibP1(null);
          clearCalibrationObjects();
          drawCalibrationLine(calibP1!, { x: pointer.x, y: pointer.y });
        }
        return;
      }

      // Landmark placement mode
      if (mode === 'landmark' && selectedKey) {
        const pointer = fc.getScenePoint(opt.e);
        if (!pointer) return;

        const def = LANDMARK_DEFS[selectedKey];
        if (!def) return;

        pushUndo(`وضع ${def.nameAr}`);

        const existing = lmMap[selectedKey];
        if (existing) {
          onLandmarksChange(landmarks.map(l =>
            l.key === selectedKey ? { ...l, x: pointer.x, y: pointer.y, isAiPlaced: false } : l
          ));
        } else {
          onLandmarksChange([...landmarks, {
            key: selectedKey, name: selectedKey,
            nameAr: def.nameAr, x: pointer.x, y: pointer.y,
            isAiPlaced: false, group: def.group as CephLandmark['group'],
          }]);
        }
      }
    });

    // ── Hover detection ──
    fc.on('mouse:over', (opt) => {
      const target = opt.target as fabric.Object & { _landmarkKey?: string };
      if (target?._landmarkKey) {
        setHoveredKey(target._landmarkKey);
      }
    });
    fc.on('mouse:out', () => {
      setHoveredKey(null);
    });

    // ── Cursor ──
    fc.on('mouse:move', () => {
      if (mode === 'pan' && !isPanningRef.current) {
        fc.setCursor('grab');
      } else if (mode === 'landmark') {
        fc.setCursor(selectedKey ? 'crosshair' : 'default');
      } else if (mode === 'calibrate') {
        fc.setCursor('crosshair');
      }
    });

    return () => {
      fc.dispose();
      fabricRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ─── Re-attach mode-dependent event handlers when mode changes ──────────
  useEffect(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    if (mode === 'pan') {
      fc.setCursor('grab');
      fc.selection = false;
    } else if (mode === 'landmark') {
      fc.setCursor(selectedKey ? 'crosshair' : 'default');
      fc.selection = false;
    } else if (mode === 'calibrate') {
      fc.setCursor('crosshair');
      fc.selection = false;
    }
  }, [mode, selectedKey]);

  // ─── Load background image ──────────────────────────────────────────────

  useEffect(() => {
    const fc = fabricRef.current;
    if (!fc) return;

    if (!imageUrl) {
      // Remove old background
      if (bgImageRef.current) {
        fc.remove(bgImageRef.current);
        bgImageRef.current = null;
      }
      // Draw placeholder text
      drawPlaceholder(fc);
      fitCanvasToContainer();
      return;
    }

    const imgEl = new Image();
    imgEl.crossOrigin = 'anonymous';
    imgEl.onload = () => {
      if (bgImageRef.current) {
        fc.remove(bgImageRef.current);
      }

      const iW = imageWidth || imgEl.naturalWidth;
      const iH = imageHeight || imgEl.naturalHeight;

      const fImg = new fabric.FabricImage(imgEl, {
        left: 0,
        top: 0,
        scaleX: iW / imgEl.naturalWidth,
        scaleY: iH / imgEl.naturalHeight,
        selectable: false,
        evented: false,
        objectCaching: false,
      });

      fc.add(fImg);
      fc.sendObjectToBack(fImg);
      bgImageRef.current = fImg;

      // Fit to view
      fitImageToView(fc, iW, iH);
      renderOverlays();
    };
    imgEl.onerror = () => {
      drawPlaceholder(fc);
    };
    imgEl.src = imageUrl;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imageUrl, imageWidth, imageHeight]);

  // ─── Window resize ──────────────────────────────────────────────────────

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const obs = new ResizeObserver(() => {
      fitCanvasToContainer();
    });
    obs.observe(container);
    return () => obs.disconnect();
  }, []);

  // ─── Render overlays when landmarks / planes / simulation change ─────────

  useEffect(() => {
    renderOverlays();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [landmarks, selectedKey, showPlanes, showSimulation, simulationScenario, hoveredKey]);

  // ─── Helper: fit canvas to container ────────────────────────────────────

  const fitCanvasToContainer = useCallback(() => {
    const fc = fabricRef.current;
    const container = containerRef.current;
    if (!fc || !container) return;
    fc.setDimensions({ width: container.clientWidth, height: container.clientHeight });
    fc.requestRenderAll();
  }, []);

  // ─── Helper: fit image to view ──────────────────────────────────────────

  const fitImageToView = useCallback((fc: fabric.Canvas, iW: number, iH: number) => {
    const container = containerRef.current;
    if (!container) return;
    const cW = container.clientWidth;
    const cH = container.clientHeight;
    const scale = Math.min(cW / iW, cH / iH) * 0.92;
    fc.setViewportTransform([1, 0, 0, 1, 0, 0]);
    fc.zoomToPoint(new fabric.Point(cW / 2, cH / 2), scale);
    const vpt = fc.viewportTransform!;
    vpt[4] = (cW - iW * scale) / 2;
    vpt[5] = (cH - iH * scale) / 2;
    fc.requestRenderAll();
    setZoomLevel(Math.round(scale * 100));
  }, []);

  // ─── Helper: draw placeholder ───────────────────────────────────────────

  const drawPlaceholder = useCallback((fc: fabric.Canvas) => {
    // Remove any old placeholder objects
    const toRemove: fabric.Object[] = [];
    fc.getObjects().forEach(o => {
      if ((o as fabric.Object & { _placeholder?: boolean })._placeholder) toRemove.push(o);
    });
    toRemove.forEach(o => fc.remove(o));

    const cW = fc.getWidth();
    const cH = fc.getHeight();

    const rect = new fabric.Rect({
      left: cW * 0.1, top: cH * 0.1,
      width: cW * 0.8, height: cH * 0.8,
      fill: '#1E293B',
      selectable: false, evented: false,
    });
    (rect as fabric.Object & { _placeholder?: boolean })._placeholder = true;

    const text1 = new fabric.Text('أضف رابط صورة الأشعة السيفالومترية', {
      left: cW / 2, top: cH / 2 - 15,
      fontSize: 16, fill: '#64748B',
      fontFamily: 'Tajawal, sans-serif',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (text1 as fabric.Object & { _placeholder?: boolean })._placeholder = true;

    const text2 = new fabric.Text('سيتم عرض الصورة هنا للتحديد اليدوي أو بالذكاء الاصطناعي', {
      left: cW / 2, top: cH / 2 + 15,
      fontSize: 12, fill: '#475569',
      fontFamily: 'Tajawal, sans-serif',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (text2 as fabric.Object & { _placeholder?: boolean })._placeholder = true;

    fc.add(rect, text1, text2);
    fc.requestRenderAll();
  }, []);

  // ─── Helper: draw calibration point ─────────────────────────────────────

  const drawCalibrationPoint = useCallback((x: number, y: number, step: number) => {
    const fc = fabricRef.current;
    if (!fc) return;
    const circle = new fabric.Circle({
      left: x, top: y,
      radius: 4, fill: '#FBBF24',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (circle as fabric.Object & { _calibration?: boolean })._calibration = true;
    fc.add(circle);
    if (step === 1) calibrationRef.current.pt1 = circle;
    else calibrationRef.current.pt2 = circle;
    fc.requestRenderAll();
  }, []);

  // ─── Helper: draw calibration line ──────────────────────────────────────

  const drawCalibrationLine = useCallback((p1: { x: number; y: number }, p2: { x: number; y: number }) => {
    const fc = fabricRef.current;
    if (!fc) return;

    clearCalibrationObjects();

    const line = new fabric.Line([p1.x, p1.y, p2.x, p2.y], {
      stroke: '#FBBF24', strokeWidth: 2,
      strokeDashArray: [6, 3],
      selectable: false, evented: false,
    });
    (line as fabric.Object & { _calibration?: boolean })._calibration = true;

    const dist = Math.hypot(p2.x - p1.x, p2.y - p1.y);
    const midX = (p1.x + p2.x) / 2;
    const midY = (p1.y + p2.y) / 2 - 12;

    const ppm = pixelsPerMm;
    const mmText = ppm ? `${(dist / ppm).toFixed(1)} mm` : `${dist.toFixed(0)} px`;

    const label = new fabric.Text(mmText, {
      left: midX, top: midY,
      fontSize: 11, fill: '#FBBF24',
      fontFamily: 'Tajawal, monospace',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (label as fabric.Object & { _calibration?: boolean })._calibration = true;

    const dot1 = new fabric.Circle({
      left: p1.x, top: p1.y, radius: 3, fill: '#FBBF24',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (dot1 as fabric.Object & { _calibration?: boolean })._calibration = true;

    const dot2 = new fabric.Circle({
      left: p2.x, top: p2.y, radius: 3, fill: '#FBBF24',
      originX: 'center', originY: 'center',
      selectable: false, evented: false,
    });
    (dot2 as fabric.Object & { _calibration?: boolean })._calibration = true;

    fc.add(dot1, dot2, line, label);
    calibrationRef.current = { line, pt1: dot1, pt2: dot2, label };
    fc.requestRenderAll();
  }, [pixelsPerMm]);

  // ─── Helper: clear calibration objects ──────────────────────────────────

  const clearCalibrationObjects = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const toRemove: fabric.Object[] = [];
    fc.getObjects().forEach(o => {
      if ((o as fabric.Object & { _calibration?: boolean })._calibration) toRemove.push(o);
    });
    toRemove.forEach(o => fc.remove(o));
    calibrationRef.current = { line: null, pt1: null, pt2: null, label: null };
    fc.requestRenderAll();
  }, []);

  // ─── Main overlay renderer ──────────────────────────────────────────────

  const renderOverlays = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;

    // Remove old landmark objects
    landmarkObjectsRef.current.forEach(obj => fc.remove(obj));
    landmarkObjectsRef.current.clear();

    // Remove old plane objects
    planeObjectsRef.current.forEach(obj => fc.remove(obj));
    planeObjectsRef.current = [];

    // Remove old simulation objects
    simObjectsRef.current.forEach(obj => fc.remove(obj));
    simObjectsRef.current = [];

    // ── Draw planes ──
    if (showPlanes) {
      PLANES.forEach(p => {
        const lm1 = lmMap[p.pts[0]], lm2 = lmMap[p.pts[1]];
        if (!lm1 || !lm2) return;
        const dx = lm2.x - lm1.x, dy = lm2.y - lm1.y;
        const extX1 = lm1.x - dx * PLANE_EXTENSION_FACTOR;
        const extY1 = lm1.y - dy * PLANE_EXTENSION_FACTOR;
        const extX2 = lm2.x + dx * PLANE_EXTENSION_FACTOR;
        const extY2 = lm2.y + dy * PLANE_EXTENSION_FACTOR;

        const line = new fabric.Line([extX1, extY1, extX2, extY2], {
          stroke: p.color, strokeWidth: 1.2,
          strokeDashArray: p.dash.length > 0 ? p.dash : undefined,
          opacity: 0.65,
          selectable: false, evented: false,
        });
        (line as fabric.Object & { _overlay?: boolean })._overlay = true;
        fc.add(line);
        planeObjectsRef.current.push(line);

        if (p.label) {
          const midX = (lm1.x + lm2.x) / 2 + 10;
          const midY = (lm1.y + lm2.y) / 2 - 8;
          const text = new fabric.Text(p.label, {
            left: midX, top: midY,
            fontSize: 10, fill: p.color,
            fontFamily: 'monospace', fontWeight: 'bold',
            originX: 'center', originY: 'center',
            opacity: 0.7,
            selectable: false, evented: false,
          });
          (text as fabric.Object & { _overlay?: boolean })._overlay = true;
          fc.add(text);
          planeObjectsRef.current.push(text);
        }
      });
    }

    // ── Draw simulation ──
    if (showSimulation) {
      const sc = SIMULATION_SCENARIOS[simulationScenario];
      if (sc) {
        Object.entries(sc.vectors).forEach(([key, v]) => {
          const lm = lmMap[key];
          if (!lm) return;
          const iW = imageWidth || 800;
          const sx = lm.x + v.dx * iW;
          const sy = lm.y + v.dy * iW;

          // Displacement line (dashed)
          const line = new fabric.Line([lm.x, lm.y, sx, sy], {
            stroke: '#4ADE80', strokeWidth: 1.5,
            strokeDashArray: [4, 3],
            opacity: 0.7,
            selectable: false, evented: false,
          });
          (line as fabric.Object & { _overlay?: boolean })._overlay = true;
          fc.add(line);
          simObjectsRef.current.push(line);

          // Predicted position (hollow circle)
          const predCircle = new fabric.Circle({
            left: sx, top: sy,
            radius: 5, fill: 'transparent',
            stroke: '#4ADE80', strokeWidth: 1.5,
            originX: 'center', originY: 'center',
            selectable: false, evented: false,
          });
          (predCircle as fabric.Object & { _overlay?: boolean })._overlay = true;
          fc.add(predCircle);
          simObjectsRef.current.push(predCircle);

          // Arrow head
          const ang = Math.atan2(sy - lm.y, sx - lm.x);
          const ax1 = sx - 7 * Math.cos(ang - 0.4);
          const ay1 = sy - 7 * Math.sin(ang - 0.4);
          const ax2 = sx - 7 * Math.cos(ang + 0.4);
          const ay2 = sy - 7 * Math.sin(ang + 0.4);
          const arrow = new fabric.Polygon(
            [{ x: sx, y: sy }, { x: ax1, y: ay1 }, { x: ax2, y: ay2 }],
            { fill: '#4ADE80', opacity: 0.7, selectable: false, evented: false }
          );
          (arrow as fabric.Object & { _overlay?: boolean })._overlay = true;
          fc.add(arrow);
          simObjectsRef.current.push(arrow);
        });
      }
    }

    // ── Draw landmarks ──
    landmarks.forEach(lm => {
      const def = LANDMARK_DEFS[lm.key];
      if (!def) return;

      const isSelected = lm.key === selectedKey;
      const isHovered = lm.key === hoveredKey;
      const isAi = lm.isAiPlaced;
      const r = isAi ? AI_LANDMARK_RADIUS : LANDMARK_RADIUS;
      const displayR = isSelected ? r + 1 : isHovered ? r + 0.5 : r;

      const parts: fabric.Object[] = [];

      // Selection ring
      if (isSelected) {
        const ring = new fabric.Circle({
          left: lm.x, top: lm.y,
          radius: displayR + 5,
          fill: 'transparent',
          stroke: 'rgba(255,255,255,0.9)',
          strokeWidth: 2.5,
          shadow: new fabric.Shadow({ color: def.color, blur: 12 }),
          originX: 'center', originY: 'center',
        });
        parts.push(ring);
      }

      // Main circle
      if (isAi) {
        // AI landmark: hollow with dashed border
        const aiCircle = new fabric.Circle({
          left: lm.x, top: lm.y,
          radius: displayR,
          fill: def.color + '40',
          stroke: def.color,
          strokeWidth: 1.5,
          strokeDashArray: [3, 2],
          originX: 'center', originY: 'center',
        });
        parts.push(aiCircle);
      } else {
        // Manual landmark: solid
        const circle = new fabric.Circle({
          left: lm.x, top: lm.y,
          radius: displayR,
          fill: def.color,
          originX: 'center', originY: 'center',
        });
        parts.push(circle);
      }

      // Label
      const label = new fabric.Text(lm.key, {
        left: lm.x + displayR + 4,
        top: lm.y - 4,
        fontSize: isSelected ? 11 : 9,
        fill: '#FFFFFF',
        fontFamily: 'monospace',
        fontWeight: 'bold',
        shadow: new fabric.Shadow({ color: 'rgba(0,0,0,0.8)', blur: 3, offsetX: 0, offsetY: 0 }),
        selectable: false, evented: false,
      });
      parts.push(label);

      // AI confidence bar
      if (isAi && lm.confidence !== undefined) {
        const barW = 16;
        const barH = 2.5;
        const barLeft = lm.x - barW / 2;
        const barTop = lm.y + displayR + 4;

        const bgBar = new fabric.Rect({
          left: barLeft, top: barTop,
          width: barW, height: barH,
          fill: '#1E293B',
          selectable: false, evented: false,
        });
        parts.push(bgBar);

        const confColor = lm.confidence > 0.85 ? '#4ADE80' : lm.confidence > 0.70 ? '#FBBF24' : '#F87171';
        const fgBar = new fabric.Rect({
          left: barLeft, top: barTop,
          width: barW * lm.confidence, height: barH,
          fill: confColor,
          selectable: false, evented: false,
        });
        parts.push(fgBar);
      }

      // Create a group for the landmark
      const group = new fabric.Group(parts, {
        selectable: true,
        evented: true,
        hasControls: false,
        hasBorders: false,
        lockRotation: true,
        lockScalingX: true,
        lockScalingY: true,
        originX: 'center',
        originY: 'center',
        subTargetCheck: false,
      });

      (group as fabric.Object & { _landmarkKey?: string })._landmarkKey = lm.key;

      // Handle drag to reposition
      group.on('moving', () => {
        const left = group.left ?? 0;
        const top = group.top ?? 0;
        // Convert group position back to scene coordinates
        const fc2 = fabricRef.current;
        if (!fc2) return;
        const vpt = fc2.viewportTransform!;
        const sceneX = (left - vpt[4]) / fc2.getZoom();
        const sceneY = (top - vpt[5]) / fc2.getZoom();

        const updated = landmarks.map(l =>
          l.key === lm.key ? { ...l, x: sceneX, y: sceneY, isAiPlaced: false } : l
        );
        onLandmarksChange(updated);
      });

      group.on('mouseup', () => {
        onSelectKey(lm.key);
      });

      fc.add(group);
      landmarkObjectsRef.current.set(lm.key, group);
    });

    fc.requestRenderAll();
  }, [landmarks, lmMap, selectedKey, hoveredKey, showPlanes, showSimulation, simulationScenario, imageWidth, imageHeight, onLandmarksChange, onSelectKey]);

  // ─── Zoom controls ──────────────────────────────────────────────────────

  const zoomIn = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    let z = fc.getZoom() + ZOOM_STEP;
    z = Math.min(z, MAX_ZOOM);
    const center = fc.getCenterPoint();
    fc.zoomToPoint(center, z);
    setZoomLevel(Math.round(z * 100));
    renderOverlays();
  }, [renderOverlays]);

  const zoomOut = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    let z = fc.getZoom() - ZOOM_STEP;
    z = Math.max(z, MIN_ZOOM);
    const center = fc.getCenterPoint();
    fc.zoomToPoint(center, z);
    setZoomLevel(Math.round(z * 100));
    renderOverlays();
  }, [renderOverlays]);

  const zoomFit = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const iW = imageWidth || 800;
    const iH = imageHeight || 600;
    fitImageToView(fc, iW, iH);
    renderOverlays();
  }, [imageWidth, imageHeight, fitImageToView, renderOverlays]);

  const zoom100 = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const center = fc.getCenterPoint();
    fc.zoomToPoint(center, 1.0);
    setZoomLevel(100);
    renderOverlays();
  }, [renderOverlays]);

  // ─── Export functions ────────────────────────────────────────────────────

  const exportPNG = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const dataUrl = fc.toDataURL({ format: 'png', multiplier: 2 });
    const link = document.createElement('a');
    link.download = 'ceph-analysis.png';
    link.href = dataUrl;
    link.click();
  }, []);

  const exportSVG = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const svg = fc.toSVG();
    const blob = new Blob([svg], { type: 'image/svg+xml' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.download = 'ceph-analysis.svg';
    link.href = url;
    link.click();
    URL.revokeObjectURL(url);
  }, []);

  const handlePrint = useCallback(() => {
    const fc = fabricRef.current;
    if (!fc) return;
    const dataUrl = fc.toDataURL({ format: 'png', multiplier: 2 });
    const win = window.open('', '_blank');
    if (win) {
      win.document.write(`<html><head><title>طباعة التحليل السيفالومتري</title><style>body{margin:0;display:flex;justify-content:center;align-items:center;min-height:100vh;}img{max-width:100%;max-height:100vh;}</style></head><body><img src="${dataUrl}" onload="window.print();window.close();" /></body></html>`);
      win.document.close();
    }
  }, []);

  // ─── Calibration reset ──────────────────────────────────────────────────

  const startCalibration = useCallback(() => {
    setMode('calibrate');
    setCalibStep(0);
    setCalibP1(null);
    clearCalibrationObjects();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ─── Expose export ref ──────────────────────────────────────────────────

  useEffect(() => {
    if (exportRef) {
      exportRef.current = {
        getDataUrl: () => {
          const fc = fabricRef.current;
          if (!fc) return null;
          try {
            return fc.toDataURL({ format: 'png', multiplier: 2 });
          } catch {
            return null;
          }
        },
      };
    }
    return () => {
      if (exportRef) exportRef.current = null;
    };
  }, [exportRef]);

  // ─── Render ─────────────────────────────────────────────────────────────

  return (
    <div ref={containerRef} className="relative w-full h-full bg-gray-950 rounded-lg overflow-hidden select-none">
      <canvas ref={canvasElRef} />

      {/* ── Toolbar ── */}
      <div className="absolute top-2 left-2 right-2 flex items-center justify-between pointer-events-none z-10">
        {/* Mode selector */}
        <div className="flex items-center gap-1 pointer-events-auto bg-gray-900/85 backdrop-blur-sm rounded-lg p-1 border border-gray-700/50">
          {([
            { key: 'landmark' as CanvasMode, icon: '⊕', label: 'النقاط' },
            { key: 'pan' as CanvasMode,       icon: '✋', label: 'تحريك' },
            { key: 'calibrate' as CanvasMode, icon: '📏', label: 'معايرة' },
          ]).map(m => (
            <button
              key={m.key}
              onClick={() => m.key === 'calibrate' ? startCalibration() : setMode(m.key)}
              className={`px-2 py-1 text-[10px] rounded-md transition flex items-center gap-1 ${
                mode === m.key
                  ? 'bg-white/15 text-white shadow-sm'
                  : 'text-gray-400 hover:text-white hover:bg-white/5'
              }`}
              title={m.label}
            >
              <span>{m.icon}</span>
              <span className="hidden sm:inline">{m.label}</span>
            </button>
          ))}
        </div>

        {/* Zoom controls */}
        <div className="flex items-center gap-1 pointer-events-auto bg-gray-900/85 backdrop-blur-sm rounded-lg p-1 border border-gray-700/50">
          <button onClick={zoomOut} className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 transition text-sm" title="تصغير">−</button>
          <span className="text-[10px] text-gray-300 font-mono min-w-[36px] text-center">{zoomLevel}%</span>
          <button onClick={zoomIn} className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 transition text-sm" title="تكبير">+</button>
          <div className="w-px h-4 bg-gray-600 mx-0.5" />
          <button onClick={zoomFit} className="px-1.5 py-0.5 text-[9px] text-gray-300 hover:text-white hover:bg-white/10 rounded-md transition" title="ملاءمة">⊞</button>
          <button onClick={zoom100} className="px-1.5 py-0.5 text-[9px] text-gray-300 hover:text-white hover:bg-white/10 rounded-md transition" title="100%">1:1</button>
        </div>

        {/* Action buttons */}
        <div className="flex items-center gap-1 pointer-events-auto bg-gray-900/85 backdrop-blur-sm rounded-lg p-1 border border-gray-700/50">
          <button
            onClick={handleUndo}
            disabled={undoStack.length === 0}
            className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 disabled:text-gray-600 disabled:cursor-not-allowed transition text-[10px]"
            title="تراجع (Ctrl+Z)"
          >↩</button>
          <button
            onClick={handleRedo}
            disabled={redoStack.length === 0}
            className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 disabled:text-gray-600 disabled:cursor-not-allowed transition text-[10px]"
            title="إعادة (Ctrl+Y)"
          >↪</button>
          <div className="w-px h-4 bg-gray-600 mx-0.5" />
          <button onClick={exportPNG} className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 transition text-[10px]" title="تصدير PNG">🖼</button>
          <button onClick={exportSVG} className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 transition text-[10px]" title="تصدير SVG">📐</button>
          <button onClick={handlePrint} className="p-1 rounded-md text-gray-300 hover:text-white hover:bg-white/10 transition text-[10px]" title="طباعة">🖨</button>
        </div>
      </div>

      {/* ── Calibration banner ── */}
      {mode === 'calibrate' && (
        <div className="absolute top-12 left-1/2 -translate-x-1/2 bg-yellow-600/90 text-white text-[11px] px-4 py-1.5 rounded-lg pointer-events-auto z-10 flex items-center gap-2">
          <span>📏</span>
          {calibStep === 0 && <span>انقر على النقطة الأولى للمعايرة</span>}
          {calibStep === 1 && <span>انقر على النقطة الثانية</span>}
          {pixelsPerMm && <span className="text-yellow-200 font-mono">({pixelsPerMm} px/mm)</span>}
          <button onClick={() => { setMode('landmark'); clearCalibrationObjects(); }} className="text-yellow-200 hover:text-white text-xs">✕</button>
        </div>
      )}

      {/* ── Hovered landmark tooltip ── */}
      {hoveredKey && LANDMARK_DEFS[hoveredKey] && (
        <div className="absolute bottom-3 left-3 bg-black/80 text-white text-xs px-2.5 py-1.5 rounded-lg pointer-events-none flex items-center gap-1.5 z-10">
          <span className="w-2 h-2 rounded-full inline-block" style={{ backgroundColor: LANDMARK_DEFS[hoveredKey]?.color }} />
          {LANDMARK_DEFS[hoveredKey]?.nameAr ?? hoveredKey}
          {lmMap[hoveredKey]?.isAiPlaced && (
            <span className="text-purple-300">· AI {Math.round((lmMap[hoveredKey]?.confidence ?? 0) * 100)}%</span>
          )}
        </div>
      )}

      {/* ── Calibration status indicator ── */}
      {pixelsPerMm && (
        <div className="absolute bottom-3 right-3 bg-green-600/80 text-white text-[10px] px-2 py-1 rounded-lg pointer-events-none z-10 flex items-center gap-1">
          <span>📏</span> معاير: {pixelsPerMm} px/mm
        </div>
      )}

      {/* ── Keyboard shortcuts hint ── */}
      <div className="absolute bottom-3 left-1/2 -translate-x-1/2 text-gray-500 text-[9px] pointer-events-none z-10 hidden sm:block">
        عجلة الماوس: تكبير/تصغير · Alt+سحب: تحريك · Ctrl+Z: تراجع · نقر مزدوج: تكبير 200%
      </div>
    </div>
  );
}
