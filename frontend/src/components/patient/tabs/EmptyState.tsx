"use client";

import type { LucideIcon } from "lucide-react";
import { ArrowLeft } from "lucide-react";
import Link from "next/link";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description?: string;
  actionLabel?: string;
  actionHref?: string;
  actionOnClick?: () => void;
  actionVariant?: "primary" | "secondary" | "outline";
}

const variantClasses: Record<NonNullable<EmptyStateProps["actionVariant"]>, string> = {
  primary:
    "bg-[#3d7ab5] text-white hover:bg-[#2d5e8e]",
  secondary:
    "bg-[#22c55e] text-white hover:bg-[#16a34a]",
  outline:
    "border border-[#3d7ab5] text-[#3d7ab5] bg-white hover:bg-[#eef3f9]",
};

export function EmptyState({
  icon: Icon,
  title,
  description,
  actionLabel,
  actionHref,
  actionOnClick,
  actionVariant = "primary",
}: EmptyStateProps) {
  return (
    <div className="text-center py-12" dir="rtl">
      <Icon className="w-12 h-12 text-[#dce8f5] mx-auto mb-3" />
      <h3 className="text-sm font-medium text-[#94a3b8]">{title}</h3>
      {description && (
        <p className="text-xs text-[#dce8f5] mt-1">{description}</p>
      )}
      {actionLabel &&
        (actionHref ? (
          <Link
            href={actionHref}
            className={`inline-flex items-center gap-2 mt-4 rounded-lg px-4 py-2 text-xs font-semibold transition-colors ${variantClasses[actionVariant]}`}
          >
            {actionLabel}
            <ArrowLeft className="w-3.5 h-3.5" />
          </Link>
        ) : actionOnClick ? (
          <button
            onClick={actionOnClick}
            className={`inline-flex items-center gap-2 mt-4 rounded-lg px-4 py-2 text-xs font-semibold transition-colors ${variantClasses[actionVariant]}`}
          >
            {actionLabel}
            <ArrowLeft className="w-3.5 h-3.5" />
          </button>
        ) : null)}
    </div>
  );
}
