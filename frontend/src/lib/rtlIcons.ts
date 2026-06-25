/**
 * RTL-aware directional icon helpers (Sprint 17).
 *
 * The audit (Sprint 17) said: "Lucide icons do not always reflect RTL
 * direction (back arrow points left instead of right in RTL)".
 *
 * The Aqlan Dental Pro UI is Arabic RTL. In RTL:
 *   - "back / previous" should point RIGHT (ChevronRight / ArrowRight)
 *   - "next / forward" should point LEFT  (ChevronLeft  / ArrowLeft)
 *
 * Lucide's default `ChevronLeft` / `ArrowLeft` are LTR-oriented (they point
 * left = back). When a developer hardcodes `ChevronLeft` for a "back" button
 * in this RTL app, the icon points the wrong way.
 *
 * Use these helpers instead of hardcoding `ChevronLeft` / `ArrowLeft` for
 * navigation:
 *   - `rtlBackIcon`     — chevron for "back / previous" (points right)
 *   - `rtlNextIcon`     — chevron for "next / forward"  (points left)
 *   - `rtlArrowBack`    — arrow    for "back / previous" (points right)
 *   - `rtlArrowForward` — arrow    for "next / forward"  (points left)
 *
 * Do NOT use these for semantic icons (trash, edit, save, etc.) — only for
 * directional navigation (chevrons / arrows in pagination, breadcrumbs-as-
 * back-button, wizard prev/next, slide-over close-back-to-list, etc.).
 *
 * Why a constant rather than a hook? Directionality in this app is fixed
 * (Arabic RTL — `dir="rtl"` is set at the html root and never toggled to LTR).
 * A static export is simpler, faster, and tree-shakes cleanly.
 */
import {
  ArrowLeft,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

/** RTL "back / previous" chevron — points RIGHT. */
export const rtlBackIcon = ChevronRight;

/** RTL "next / forward" chevron — points LEFT. */
export const rtlNextIcon = ChevronLeft;

/** RTL "back / previous" arrow — points RIGHT. */
export const rtlArrowBack = ArrowRight;

/** RTL "next / forward" arrow — points LEFT. */
export const rtlArrowForward = ArrowLeft;
