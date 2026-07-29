import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CephImageSliderCompare } from '@/components/ceph/CephImageSliderCompare';

// ─── CephImageSliderCompare (CEPH-EPIC C-B) ─────────────────────────────────
//
// Verifies the three behaviours that matter most for the before/after radiograph
// wipe slider:
//   1. The friendly Arabic fallback renders when either image URL is missing.
//   2. The slider mounts with BOTH images + the "قبل / بعد" labels when both
//      URLs are present.
//   3. Dragging the handle updates the BEFORE image's clip-path (i.e. the
//      position state actually moves the wipe line).

afterEach(() => {
  vi.restoreAllMocks();
});

describe('CephImageSliderCompare', () => {
  it('shows the Arabic "image not available" fallback when baseImageUrl is empty', () => {
    render(
      <CephImageSliderCompare
        baseImageUrl=""
        targetImageUrl="https://example.com/after.jpg"
        baseDate="2024-01-01"
        targetDate="2024-06-01"
      />,
    );
    expect(
      screen.getByText(/صورة الأشعة غير متوفرة لهذا التحليل/),
    ).toBeInTheDocument();
    // No slider handle/labels rendered.
    expect(screen.queryByText('قبل')).not.toBeInTheDocument();
    expect(screen.queryByText('بعد')).not.toBeInTheDocument();
  });

  it('shows the Arabic fallback when targetImageUrl is empty', () => {
    render(
      <CephImageSliderCompare
        baseImageUrl="https://example.com/before.jpg"
        targetImageUrl=""
        baseDate="2024-01-01"
        targetDate="2024-06-01"
      />,
    );
    expect(
      screen.getByText(/صورة الأشعة غير متوفرة لهذا التحليل/),
    ).toBeInTheDocument();
  });

  it('renders both labels and the slider handle when both URLs are present', () => {
    render(
      <CephImageSliderCompare
        baseImageUrl="https://example.com/before.jpg"
        targetImageUrl="https://example.com/after.jpg"
        baseDate="2024-01-01"
        targetDate="2024-06-01"
      />,
    );
    // Both Arabic labels render.
    expect(screen.getByText('قبل')).toBeInTheDocument();
    expect(screen.getByText('بعد')).toBeInTheDocument();
    // Both <img> tags rendered with the correct src (no crossOrigin attribute).
    const imgs = screen.getAllByRole('img') as HTMLImageElement[];
    expect(imgs).toHaveLength(2);
    const srcs = imgs.map(i => i.getAttribute('src'));
    expect(srcs).toContain('https://example.com/before.jpg');
    expect(srcs).toContain('https://example.com/after.jpg');
    for (const img of imgs) {
      expect(img.getAttribute('crossorigin')).toBeNull();
    }
    // Slider role + initial value 50.
    const slider = screen.getByRole('slider');
    expect(slider.getAttribute('aria-valuenow')).toBe('50');
  });

  it('moves the wipe position when the handle is dragged via pointer events', () => {
    render(
      <CephImageSliderCompare
        baseImageUrl="https://example.com/before.jpg"
        targetImageUrl="https://example.com/after.jpg"
        baseDate="2024-01-01"
        targetDate="2024-06-01"
      />,
    );

    const slider = screen.getByRole('slider');
    // getBoundingClientRect is normally 0 in jsdom — stub a 400px-wide track.
    vi.spyOn(slider, 'getBoundingClientRect').mockReturnValue({
      left: 0, top: 0, right: 400, bottom: 300,
      width: 400, height: 300, x: 0, y: 0, toJSON: () => '',
    } as DOMRect);

    // Pointer down at x=300 (75% of 400) → position should become 75.
    fireEvent.pointerDown(slider, { clientX: 300, pointerId: 1 });
    expect(slider.getAttribute('aria-valuenow')).toBe('75');

    // Drag to x=100 (25%) → position becomes 25.
    fireEvent.pointerMove(slider, { clientX: 100, pointerId: 1 });
    expect(slider.getAttribute('aria-valuenow')).toBe('25');

    fireEvent.pointerUp(slider, { pointerId: 1 });
  });
});
