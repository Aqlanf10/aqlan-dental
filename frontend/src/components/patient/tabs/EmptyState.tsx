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
      <div className="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mb-4">
        <Icon className="w-8 h-8 text-gray-300" />
      </div>
      <h3 className="text-base font-semibold text-gray-700 mb-1">{title}</h3>
      <p className="text-sm text-gray-400 max-w-xs">{description}</p>
      {comingSoon && (
        <button
          disabled
          className="mt-4 px-4 py-2 text-sm font-medium rounded-lg bg-gray-100 text-gray-400 cursor-not-allowed"
        >
          هذه الميزة قيد التطوير
        </button>
      )}
    </div>
  );
}
