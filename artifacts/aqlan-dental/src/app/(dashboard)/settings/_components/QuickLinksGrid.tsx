// YOLO-S3 (Settings Hub) — renders a responsive grid of quick-link cards
// inside each settings tab. Replaces the old hard-coded "Quick links" block
// at the bottom of page.tsx; the same card markup is now reused across all
// category tabs so each group surfaces its related /settings/* sub-routes
// in place. Supports a `comingSoon` state for placeholder cards that point
// at features not yet routed (templates, ceph norms).

import Link from "@/lib/nextLinkCompat";
import { cn } from "@/lib/utils";
import {
  QUICK_LINK_COLOR_CLASSES,
  type QuickLink,
} from "./_shared";

interface QuickLinksGridProps {
  links: QuickLink[];
  /** Optional heading shown above the grid (e.g. "روابط ذات صلة"). */
  title?: string;
}

export function QuickLinksGrid({ links, title }: QuickLinksGridProps) {
  if (!links || links.length === 0) return null;

  return (
    <div className="space-y-3">
      {title && (
        <h3 className="text-sm font-semibold text-gray-700">{title}</h3>
      )}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {links.map((link) => {
          const Icon = link.icon;
          const colorCls = QUICK_LINK_COLOR_CLASSES[link.color];

          // Coming-soon cards render as a non-interactive tile with a small
          // "قريبًا" badge so users know the feature is planned, not broken.
          if (link.comingSoon) {
            return (
              <div
                key={link.label}
                className="flex items-center gap-4 bg-white rounded-xl border border-dashed border-gray-200 p-4 opacity-70"
                aria-disabled="true"
              >
                <div
                  className={cn(
                    "w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0",
                    colorCls.tile
                  )}
                >
                  <Icon className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="font-semibold text-gray-900 truncate">
                      {link.label}
                    </p>
                    <span className="text-[10px] font-bold text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded">
                      قريبًا
                    </span>
                  </div>
                  <p className="text-sm text-gray-500 line-clamp-2">
                    {link.description}
                  </p>
                </div>
              </div>
            );
          }

          return (
            <Link
              key={link.href}
              href={link.href}
              className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-4 hover:border-clinic-blue hover:shadow-md transition group"
            >
              <div
                className={cn(
                  "w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0 transition",
                  colorCls.tile,
                  colorCls.hoverTile
                )}
              >
                <Icon className="w-5 h-5" />
              </div>
              <div className="min-w-0">
                <p className="font-semibold text-gray-900 truncate">
                  {link.label}
                </p>
                <p className="text-sm text-gray-500 line-clamp-2">
                  {link.description}
                </p>
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
