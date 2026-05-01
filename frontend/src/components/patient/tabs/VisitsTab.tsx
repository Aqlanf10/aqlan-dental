"use client";

import { ClipboardList } from "lucide-react";
import { EmptyState } from "./EmptyState";

export function VisitsTab() {
  return (
    <EmptyState
      icon={ClipboardList}
      title="الزيارات"
      description="سيتم عرض جلسات العلاج والزيارات هنا"
      comingSoon
    />
  );
}
