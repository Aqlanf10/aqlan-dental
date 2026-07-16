"use client";

import { useState } from "react";
import { ClipboardCheck, Upload } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import CephPilotImportWorkspace from "./CephPilotImportWorkspace";
import CephPilotReviewWorkspace from "./CephPilotReviewWorkspace";

export default function CephPilotWorkspace() {
  const role = useAuthStore(state => state.user?.role);
  const isAdmin = role === "Admin";
  const [mode, setMode] = useState<"import" | "review">(isAdmin ? "import" : "review");

  return (
    <div className="space-y-4">
      {isAdmin ? (
        <div className="mx-auto flex max-w-7xl border-b border-gray-200" role="tablist" aria-label="مساحة Pilot">
          <button type="button" role="tab" aria-selected={mode === "import"} onClick={() => setMode("import")} className={`inline-flex h-10 items-center gap-2 border-b-2 px-4 text-sm font-bold ${mode === "import" ? "border-clinic-blue text-clinic-blue" : "border-transparent text-gray-500"}`}><Upload className="h-4 w-4" />الإدخال والتحضير</button>
          <button type="button" role="tab" aria-selected={mode === "review"} onClick={() => setMode("review")} className={`inline-flex h-10 items-center gap-2 border-b-2 px-4 text-sm font-bold ${mode === "review" ? "border-clinic-blue text-clinic-blue" : "border-transparent text-gray-500"}`}><ClipboardCheck className="h-4 w-4" />المراجعة المعمّاة</button>
        </div>
      ) : null}
      {mode === "import" ? <CephPilotImportWorkspace /> : <CephPilotReviewWorkspace />}
    </div>
  );
}
