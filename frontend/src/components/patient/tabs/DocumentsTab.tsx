"use client";

import { FolderOpen } from "lucide-react";
import { EmptyState } from "./EmptyState";

export function DocumentsTab() {
  return (
    <EmptyState
      icon={FolderOpen}
      title="المستندات"
      description="سيتم عرض المستندات والملفات المرفقة هنا"
      comingSoon
    />
  );
}
