"use client";

import type { LucideIcon } from "lucide-react";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  comingSoon?: boolean;
}

export function EmptyState({ icon: Icon, title, description, comingSoon }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center" dir="rtl">
      <div className="w-16 h-16 rounded-full bg-[#f1f5f9] flex items-center justify-center mb-4">
        <Icon className="w-8 h-8 text-[#cbd5e1]" />
      </div>
      <h3 className="text-base font-semibold text-[#0d2137] mb-1">{title}</h3>
      <p className="text-sm text-[#94a3b8] max-w-xs">{description}</p>
      {comingSoon && (
        <button
          disabled
          className="mt-4 px-4 py-2 text-sm font-medium rounded-lg bg-[#f1f5f9] text-[#94a3b8] cursor-not-allowed"
        >
          هذه الميزة قيد التطوير
        </button>
      )}
    </div>
  );
}
