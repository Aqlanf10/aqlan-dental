/**
 * FE-08: Single source of truth for lab-order status labels and colors.
 * Previously these were re-declared in 3 lab pages (lab/page.tsx, lab/dashboard/page.tsx,
 * lab/overdue/page.tsx) with identical values — any future status change required editing
 * all three. Now they import from here.
 *
 * Values verified identical across the 3 copies before centralization.
 */

/** Arabic labels for LabOrderStatus. */
export const LAB_STATUS_LABELS: Record<string, string> = {
  draft: "مسودة",
  sent: "تم الإرسال",
  manufacturing: "قيد الصنع",
  tryIn: "تجربة",
  ready: "جاهز",
  received: "تم الاستلام",
  delivered: "تم التسليم",
  returned: "مرتجع",
  remake: "إعادة صناعة",
  cancelled: "ملغى",
};

/** Tailwind bg+text color classes for each status badge. */
export const LAB_STATUS_COLORS: Record<string, string> = {
  draft: "bg-gray-100 text-gray-500",
  sent: "bg-blue-100 text-blue-700",
  manufacturing: "bg-amber-100 text-amber-700",
  tryIn: "bg-teal-100 text-teal-700",
  ready: "bg-green-100 text-green-700",
  received: "bg-indigo-100 text-indigo-700",
  delivered: "bg-emerald-100 text-emerald-700",
  returned: "bg-orange-100 text-orange-700",
  remake: "bg-purple-100 text-purple-700",
  cancelled: "bg-red-100 text-red-700",
};
