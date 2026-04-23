"use client";
import { useRef, useEffect, useState, useCallback, useMemo } from "react";
import type { CephLandmark } from "@/types/ceph";

const HIT_RADIUS = 10;

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
  LS:  { nameAr: 'الشفة العلوية',         group: 'soft',     color: '#F472B6' },
  LI:  { nameAr: 'الشفة السفلية',         group: 'soft',     color: '#F472B6' },
  Pn:  { nameAr: 'طرف الأنف',             group: 'soft',     color: '#F472B6' },
  Cm:  { nameAr: 'قاعدة الأنف',           group: 'soft',     color: '#F472B6' },
};

const LANDMARK_ORDER = ['S','N','Or','Po','ANS','PNS','A','B','Pog','Gn','Me','Go','Co','Ar','D','Pm','U1T','U1A','L1T','L1A','LS','LI','Pn','Cm'];

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
}

export function CephCanvas({
  imageUrl, imageWidth, imageHeight, landmarks, onLandmarksChange,
  selectedKey, onSelectKey, showPlanes, showSimulation, simulationScenario,
}: Props) {
  const canvasRef  = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [img, setImg]         = useState<HTMLImageElement | null>(null);
  const [dragging, setDragging] = useState<string | null>(null);
  const [hovered,  setHovered]  = useState<string | null>(null);

  useEffect(() => {
    if (!imageUrl) { setImg(null); return; }
    const el = new window.Image();
    el.crossOrigin = 'anonymous';
    el.onload  = () => setImg(el);
    el.onerror = () => setImg(null);
    el.src = imageUrl;
  }, [imageUrl]);

  const lmMap = useMemo(() => {
    const m: Record<string, CephLandmark> = {};
    landmarks.forEach(l => { m[l.key] = l; });
    return m;
  }, [landmarks]);

  const getT = useCallback(() => {
    const c = canvasRef.current;
    if (!c) return null;
    const W = c.width, H = c.height;
    const iW = imageWidth  || img?.naturalWidth  || W;
    const iH = imageHeight || img?.naturalHeight || H;
    const s = Math.min(W / iW, H / iH);
    const ox = (W - iW * s) / 2;
    const oy = (H - iH * s) / 2;
    return {
      s, ox, oy, iW, iH,
      tc: (x: number, y: number) => ({ x: ox + x * s, y: oy + y * s }),
      ti: (cx: number, cy: number) => ({ x: (cx - ox) / s, y: (cy - oy) / s }),
    };
  }, [imageWidth, imageHeight, img]);

  const draw = useCallback(() => {
    const c = canvasRef.current; if (!c) return;
    const ctx = c.getContext('2d'); if (!ctx) return;
    const T = getT(); if (!T) return;

    ctx.clearRect(0, 0, c.width, c.height);
    ctx.fillStyle = '#0F172A';
    ctx.fillRect(0, 0, c.width, c.height);

    // Image / placeholder
    if (img) {
      ctx.drawImage(img, T.ox, T.oy, T.iW * T.s, T.iH * T.s);
      // Slight darkening for landmark visibility
      ctx.fillStyle = 'rgba(0,0,0,0.15)';
      ctx.fillRect(T.ox, T.oy, T.iW * T.s, T.iH * T.s);
    } else {
      ctx.fillStyle = '#1E293B';
      ctx.fillRect(T.ox, T.oy, T.iW * T.s, T.iH * T.s);
      ctx.fillStyle = '#64748B';
      ctx.font = '14px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('أضف رابط صورة الأشعة السيفالومترية', c.width / 2, c.height / 2 - 10);
      ctx.font = '11px sans-serif';
      ctx.fillStyle = '#475569';
      ctx.fillText('سيتم عرض الصورة هنا للتحديد اليدوي أو بالذكاء الاصطناعي', c.width / 2, c.height / 2 + 10);
    }

    // Planes
    if (showPlanes) {
      PLANES.forEach(p => {
        const lm1 = lmMap[p.pts[0]], lm2 = lmMap[p.pts[1]];
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
      const def = LANDMARK_DEFS[lm.key]; if (!def) return;
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
        ctx.fillStyle = def.color + '55';
        ctx.strokeStyle = def.color;
        ctx.lineWidth = 1.5;
      } else {
        ctx.fillStyle = def.color;
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

    // Selected crosshair
    if (selectedKey && !lmMap[selectedKey]) {
      ctx.save();
      ctx.strokeStyle = '#FBBF24';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      ctx.beginPath(); ctx.moveTo(0, c.height / 2); ctx.lineTo(c.width, c.height / 2); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(c.width / 2, 0); ctx.lineTo(c.width / 2, c.height); ctx.stroke();
      ctx.restore();
    }
  }, [landmarks, lmMap, img, imageWidth, imageHeight, selectedKey, hovered, showPlanes, showSimulation, simulationScenario, getT]);

  useEffect(() => { draw(); }, [draw]);

  // Resize observer
  useEffect(() => {
    const el = containerRef.current; if (!el) return;
    const obs = new ResizeObserver(() => {
      const c = canvasRef.current; if (!c) return;
      c.width  = el.clientWidth;
      c.height = el.clientHeight;
      draw();
    });
    obs.observe(el);
    return () => obs.disconnect();
  }, [draw]);

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
    return LANDMARK_ORDER.slice().reverse().find(key => {
      const lm = lmMap[key]; if (!lm) return false;
      const cp = T.tc(lm.x, lm.y);
      return Math.hypot(cx - cp.x, cy - cp.y) < HIT_RADIUS;
    }) ?? null;
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const { cx, cy } = coords(e);
    const T = getT(); if (!T) return;
    const hit = hitLandmark(cx, cy);
    if (hit) {
      setDragging(hit); onSelectKey(hit);
    } else if (selectedKey) {
      const ip = T.ti(cx, cy);
      const def = LANDMARK_DEFS[selectedKey];
      if (!def) return;
      const existing = lmMap[selectedKey];
      if (existing) {
        onLandmarksChange(landmarks.map(l => l.key === selectedKey ? { ...l, x: ip.x, y: ip.y, isAiPlaced: false } : l));
      } else {
        onLandmarksChange([...landmarks, {
          key: selectedKey, name: selectedKey,
          nameAr: def.nameAr, x: ip.x, y: ip.y,
          isAiPlaced: false, group: def.group as CephLandmark['group'],
        }]);
      }
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const { cx, cy } = coords(e);
    const T = getT(); if (!T) return;
    setHovered(hitLandmark(cx, cy));
    if (dragging) {
      const ip = T.ti(cx, cy);
      onLandmarksChange(landmarks.map(l => l.key === dragging ? { ...l, x: ip.x, y: ip.y, isAiPlaced: false } : l));
    }
    if (canvasRef.current) {
      canvasRef.current.style.cursor = hitLandmark(cx, cy) ? 'grab' : dragging ? 'grabbing' : selectedKey ? 'crosshair' : 'default';
    }
  };

  const handleMouseUp = () => setDragging(null);

  return (
    <div ref={containerRef} className="relative w-full h-full bg-gray-950 rounded-lg overflow-hidden select-none">
      <canvas
        ref={canvasRef}
        className="w-full h-full"
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
      />
      {hovered && (
        <div className="absolute bottom-3 left-3 bg-black/80 text-white text-xs px-2.5 py-1.5 rounded-lg pointer-events-none flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full inline-block" style={{ backgroundColor: LANDMARK_DEFS[hovered]?.color }} />
          {LANDMARK_DEFS[hovered]?.nameAr ?? hovered}
          {lmMap[hovered]?.isAiPlaced && (
            <span className="text-purple-300">· AI {Math.round((lmMap[hovered]?.confidence ?? 0) * 100)}%</span>
          )}
        </div>
      )}
    </div>
  );
}

export { LANDMARK_ORDER };
