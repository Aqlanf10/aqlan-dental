import { describe, it, expect } from 'vitest';
import {
  dist, angle3, angleBetweenLines, perpDist, signedPerpDist, lineAngle,
  computeJarabak, similarityFromPairs, applySimilarity,
} from '@/lib/cephMath';

type Pt = { x: number; y: number };

// ─── dist ─────────────────────────────────────────────────────────────────────

describe('dist', () => {
  it('returns 0 for identical points', () => {
    const p: Pt = { x: 1, y: 2 };
    expect(dist(p, p)).toBe(0);
  });

  it('calculates horizontal distance', () => {
    const a: Pt = { x: 0, y: 0 };
    const b: Pt = { x: 3, y: 0 };
    expect(dist(a, b)).toBe(3);
  });

  it('calculates vertical distance', () => {
    const a: Pt = { x: 0, y: 0 };
    const b: Pt = { x: 0, y: 4 };
    expect(dist(a, b)).toBe(4);
  });

  it('calculates diagonal distance (3-4-5 triangle)', () => {
    const a: Pt = { x: 0, y: 0 };
    const b: Pt = { x: 3, y: 4 };
    expect(dist(a, b)).toBeCloseTo(5, 6);
  });

  it('is commutative', () => {
    const a: Pt = { x: 1.5, y: 2.7 };
    const b: Pt = { x: 5.3, y: -1.2 };
    expect(dist(a, b)).toBeCloseTo(dist(b, a), 10);
  });
});

// ─── angle3 ───────────────────────────────────────────────────────────────────

describe('angle3', () => {
  it('returns 0 when both vectors are zero-length', () => {
    const v: Pt = { x: 1, y: 1 };
    expect(angle3(v, v, v)).toBe(0);
  });

  it('returns 0 when all three points are collinear with vertex in middle', () => {
    // p1 — vertex — p3 in a straight line
    const p1: Pt = { x: 0, y: 0 };
    const vertex: Pt = { x: 1, y: 0 };
    const p3: Pt = { x: 2, y: 0 };
    expect(angle3(p1, vertex, p3)).toBeCloseTo(180, 5);
  });

  it('returns 90° for a right angle', () => {
    const p1: Pt = { x: 0, y: 0 };
    const vertex: Pt = { x: 0, y: 1 };
    const p3: Pt = { x: 1, y: 1 };
    expect(angle3(p1, vertex, p3)).toBeCloseTo(90, 5);
  });

  it('returns ~60° for an equilateral triangle', () => {
    const p1: Pt = { x: 0, y: 0 };
    const vertex: Pt = { x: 0.5, y: 0.866 }; // ~sin(60°)
    const p3: Pt = { x: 1, y: 0 };
    expect(angle3(p1, vertex, p3)).toBeCloseTo(60, 0);
  });

  it('angle is always between 0 and 180', () => {
    const p1: Pt = { x: 1, y: 0 };
    const vertex: Pt = { x: 0, y: 0 };
    const p3: Pt = { x: -1, y: 0 };
    const result = angle3(p1, vertex, p3);
    expect(result).toBeGreaterThanOrEqual(0);
    expect(result).toBeLessThanOrEqual(180);
  });
});

// ─── angleBetweenLines ────────────────────────────────────────────────────────

describe('angleBetweenLines', () => {
  it('returns 0 for parallel lines', () => {
    const l1a: Pt = { x: 0, y: 0 };
    const l1b: Pt = { x: 1, y: 0 };
    const l2a: Pt = { x: 0, y: 1 };
    const l2b: Pt = { x: 1, y: 1 };
    expect(angleBetweenLines(l1a, l1b, l2a, l2b)).toBeCloseTo(0, 5);
  });

  it('returns 90° for perpendicular lines', () => {
    const l1a: Pt = { x: 0, y: 0 };
    const l1b: Pt = { x: 1, y: 0 };
    const l2a: Pt = { x: 0, y: 0 };
    const l2b: Pt = { x: 0, y: 1 };
    expect(angleBetweenLines(l1a, l1b, l2a, l2b)).toBeCloseTo(90, 5);
  });

  it('returns 0 when a line has zero length', () => {
    const p: Pt = { x: 1, y: 1 };
    expect(angleBetweenLines(p, p, { x: 0, y: 0 }, { x: 1, y: 0 })).toBe(0);
  });

  it('is always between 0 and 90 (acute angle)', () => {
    const l1a: Pt = { x: 0, y: 0 };
    const l1b: Pt = { x: 1, y: 0 };
    const l2a: Pt = { x: 0, y: 0 };
    const l2b: Pt = { x: -1, y: 1 }; // 135°, but acute = 45°
    const result = angleBetweenLines(l1a, l1b, l2a, l2b);
    expect(result).toBeGreaterThanOrEqual(0);
    expect(result).toBeLessThanOrEqual(90);
  });
});

// ─── perpDist ─────────────────────────────────────────────────────────────────

describe('perpDist', () => {
  it('returns 0 when the point is on the line', () => {
    const pt: Pt = { x: 2, y: 2 };
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 4, y: 4 };
    expect(perpDist(pt, lp1, lp2)).toBeCloseTo(0, 6);
  });

  it('returns correct distance for a 3-4-5 triangle', () => {
    const pt: Pt = { x: 0, y: 3 };
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 4, y: 0 };
    expect(perpDist(pt, lp1, lp2)).toBeCloseTo(3, 6);
  });

  it('always returns non-negative', () => {
    const pt: Pt = { x: 1, y: 1 };
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 1, y: 0 };
    expect(perpDist(pt, lp1, lp2)).toBeGreaterThanOrEqual(0);
  });

  it('returns 0 for degenerate line (point)', () => {
    const pt: Pt = { x: 3, y: 5 };
    const lp: Pt = { x: 1, y: 1 };
    expect(perpDist(pt, lp, lp)).toBe(0);
  });
});

// ─── signedPerpDist ───────────────────────────────────────────────────────────

describe('signedPerpDist', () => {
  it('returns 0 when point is on the line', () => {
    const pt: Pt = { x: 2, y: 2 };
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 4, y: 4 };
    expect(signedPerpDist(pt, lp1, lp2)).toBeCloseTo(0, 6);
  });

  it('returns positive when point is to the right', () => {
    // Line goes left-to-right: (0,0)→(1,0). Point (0.5, -1) is below in screen coords = right.
    const pt: Pt = { x: 0.5, y: -1 };
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 1, y: 0 };
    expect(signedPerpDist(pt, lp1, lp2)).toBeGreaterThan(0);
  });

  it('negative is the opposite side of positive', () => {
    const lp1: Pt = { x: 0, y: 0 };
    const lp2: Pt = { x: 1, y: 0 };
    const above: Pt = { x: 0.5, y: 1 };
    const below: Pt = { x: 0.5, y: -1 };
    const d1 = signedPerpDist(above, lp1, lp2);
    const d2 = signedPerpDist(below, lp1, lp2);
    expect(d1).toBeCloseTo(-d2, 10);
  });

  it('returns 0 for degenerate line', () => {
    const pt: Pt = { x: 3, y: 5 };
    const lp: Pt = { x: 1, y: 1 };
    expect(signedPerpDist(pt, lp, lp)).toBe(0);
  });
});

// ─── lineAngle ────────────────────────────────────────────────────────────────

describe('lineAngle', () => {
  it('returns 0 for horizontal line going right', () => {
    const p1: Pt = { x: 0, y: 0 };
    const p2: Pt = { x: 1, y: 0 };
    expect(lineAngle(p1, p2)).toBeCloseTo(0, 5);
  });

  it('returns 90 for vertical line going down', () => {
    const p1: Pt = { x: 0, y: 0 };
    const p2: Pt = { x: 0, y: 1 };
    expect(lineAngle(p1, p2)).toBeCloseTo(90, 5);
  });

  it('returns ~45 for diagonal line', () => {
    const p1: Pt = { x: 0, y: 0 };
    const p2: Pt = { x: 1, y: 1 };
    expect(lineAngle(p1, p2)).toBeCloseTo(45, 5);
  });

  it('returns ~-90 for vertical line going up', () => {
    const p1: Pt = { x: 0, y: 0 };
    const p2: Pt = { x: 0, y: -1 };
    expect(lineAngle(p1, p2)).toBeCloseTo(-90, 5);
  });

  it('returns 180 for horizontal line going left', () => {
    const p1: Pt = { x: 1, y: 0 };
    const p2: Pt = { x: 0, y: 0 };
    expect(lineAngle(p1, p2)).toBeCloseTo(180, 5);
  });
});

// ─── computeJarabak ─────────────────────────────────────────────────────────

describe('computeJarabak', () => {
  // A plausible lateral-ceph landmark layout (screen coords: +y is down).
  const lm: Record<string, Pt> = {
    S:  { x: 100, y: 100 },
    N:  { x: 220, y: 90  },
    Ar: { x: 90,  y: 150 },
    Go: { x: 110, y: 270 },
    Me: { x: 185, y: 320 },
  };

  it('emits exactly the five Jarabak measurements in the jarabak group', () => {
    const r = computeJarabak(lm);
    expect(r.map(m => m.name)).toEqual([
      'Saddle-Angle', 'Articular-Angle', 'Gonial-Angle', 'Bjork-Sum', 'Jarabak-Ratio',
    ]);
    expect(r.every(m => m.analysisGroup === 'jarabak')).toBe(true);
  });

  it('matches the underlying angle geometry', () => {
    const r = computeJarabak(lm);
    const v = (n: string) => r.find(m => m.name === n)!.value!;
    expect(v('Saddle-Angle')).toBeCloseTo(angle3(lm.N, lm.S, lm.Ar), 0);
    expect(v('Articular-Angle')).toBeCloseTo(angle3(lm.S, lm.Ar, lm.Go), 0);
    expect(v('Gonial-Angle')).toBeCloseTo(angle3(lm.Ar, lm.Go, lm.Me), 0);
  });

  it('Björk sum equals the three component angles', () => {
    const r = computeJarabak(lm);
    const v = (n: string) => r.find(m => m.name === n)!.value!;
    expect(v('Bjork-Sum')).toBeCloseTo(
      v('Saddle-Angle') + v('Articular-Angle') + v('Gonial-Angle'), 0);
  });

  it('ratio is PFH/AFH as a percentage and needs no calibration', () => {
    const r = computeJarabak(lm);
    const ratio = (dist(lm.S, lm.Go) / dist(lm.N, lm.Me)) * 100;
    expect(r.find(m => m.name === 'Jarabak-Ratio')!.value).toBeCloseTo(ratio, 0);
    expect(r.find(m => m.name === 'Jarabak-Ratio')!.unit).toBe('%');
  });

  it('returns null values when required landmarks are missing', () => {
    const r = computeJarabak({ S: { x: 0, y: 0 } });
    expect(r.every(m => m.value === null)).toBe(true);
  });
});

// ─── similarityFromPairs / applySimilarity (superimposition) ─────────────────

describe('similarityFromPairs', () => {
  it('maps both source points exactly onto their targets', () => {
    const srcA: Pt = { x: 10, y: 20 };
    const srcB: Pt = { x: 50, y: 20 };
    const dstA: Pt = { x: 100, y: 100 };
    const dstB: Pt = { x: 100, y: 180 }; // rotated 90°, scaled 2×, translated
    const t = similarityFromPairs(srcA, srcB, dstA, dstB);
    const a = applySimilarity(t, srcA);
    const b = applySimilarity(t, srcB);
    expect(a.x).toBeCloseTo(dstA.x, 6);
    expect(a.y).toBeCloseTo(dstA.y, 6);
    expect(b.x).toBeCloseTo(dstB.x, 6);
    expect(b.y).toBeCloseTo(dstB.y, 6);
  });

  it('preserves shape — a third point keeps its position relative to the pair', () => {
    // Identical tracings: registering one onto the other is the identity-like
    // map, so every point lands on its counterpart.
    const srcA: Pt = { x: 0, y: 0 };
    const srcB: Pt = { x: 100, y: 0 };
    const third: Pt = { x: 40, y: 30 };
    // Target = source rotated 90° about origin then shifted by (200,50).
    const rot = (p: Pt): Pt => ({ x: -p.y + 200, y: p.x + 50 });
    const t = similarityFromPairs(srcA, srcB, rot(srcA), rot(srcB));
    const mapped = applySimilarity(t, third);
    expect(mapped.x).toBeCloseTo(rot(third).x, 4);
    expect(mapped.y).toBeCloseTo(rot(third).y, 4);
  });

  it('scales distances by the target/source segment ratio', () => {
    const srcA: Pt = { x: 0, y: 0 };
    const srcB: Pt = { x: 10, y: 0 };       // |src| = 10
    const dstA: Pt = { x: 0, y: 0 };
    const dstB: Pt = { x: 30, y: 0 };       // |dst| = 30 → 3× scale
    const t = similarityFromPairs(srcA, srcB, dstA, dstB);
    const p = applySimilarity(t, { x: 5, y: 5 });
    expect(p.x).toBeCloseTo(15, 6);
    expect(p.y).toBeCloseTo(15, 6);
  });

  it('degrades to pure translation when the source pair coincides', () => {
    const s: Pt = { x: 7, y: 7 };
    const t = similarityFromPairs(s, s, { x: 20, y: 30 }, { x: 99, y: 99 });
    const mapped = applySimilarity(t, s);
    expect(mapped.x).toBeCloseTo(20, 6);
    expect(mapped.y).toBeCloseTo(30, 6);
  });
});
