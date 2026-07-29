/**
 * Shared types and constants extracted from the ortho case detail page
 * (FE-20: split ortho/[id] page into 11 tab components).
 *
 * These are used by the page shell and the individual tab components under
 * `_components/`. They are deliberately kept here so each tab file can stay
 * focused on its own JSX/logic without re-declaring shared bits.
 */

/** All tab keys available on the ortho case detail page. */
export type Tab =
  | "overview"
  | "records"
  | "compare"
  | "exam"
  | "cast"
  | "ceph"
  | "facial"
  | "problems"
  | "diagnosis"
  | "plan"
  | "stages"
  | "visits"
  | "extraction"
  | "retention"
  | "lab"
  | "finance"
  | "surgical"
  | "wizard"
  | "reports";

/** Shared input class string used across every tab form. */
export const inputCls =
  "w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue";

/** Treatment-plan label map (A/B/C) used by the treatment plans tab. */
export const PLAN_LABELS: Record<string, string> = {
  A: "خطة A",
  B: "خطة B",
  C: "خطة C",
};

/** Default empty photo form used by the records/photos tab. */
export const EMPTY_PHOTO_FORM = {
  photoUrl: "",
  photoType: "Intraoral",
  caption: "",
  category: "",
  subtype: "",
  treatmentPhase: "",
  isSelectedForReport: false,
};

/** Phase badge background classes used by the photos gallery. */
export const PHASE_BADGE_CLS: Record<string, string> = {
  Initial: "bg-green-600/90",
  Progress: "bg-blue-600/90",
  Final: "bg-violet-600/90",
};

/** Habit flag keys used by the clinical exam tab. */
export const HABIT_FLAG_KEYS = [
  "thumbSucking",
  "mouthBreathing",
  "tongueThrust",
  "lipBiting",
  "nailBiting",
  "bruxism",
] as const;

/** Photo-type labels used by the records/photos tab. */
export const PHOTO_TYPE_LABELS: Record<string, string> = {
  Intraoral: "داخل الفم",
  Extraoral: "خارج الفم",
  Progress: "متابعة",
  Radiograph: "أشعة",
};
