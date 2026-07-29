"use client";

import { OrthoCaseWizard } from "@/components/ortho/OrthoCaseWizard";
import type { Tab } from "../_lib/types";

/**
 * AI clinical draft assistant tab — a thin wrapper around the shared
 * `OrthoCaseWizard` (`@/components/ortho/OrthoCaseWizard`). Co-located in the
 * ortho case tab components (FE-20) so the page shell renders a single
 * `OrthoXxxTab` per tab. No internal logic to extract; the heavy lifting lives
 * in the shared component.
 */
export function OrthoAiDraftPanel({
  caseId,
  patientId,
  onNavigate,
}: {
  caseId: string;
  patientId?: string;
  onNavigate: (tab: Tab) => void;
}) {
  return (
    <OrthoCaseWizard
      caseId={caseId}
      patientId={patientId}
      onNavigate={onNavigate}
    />
  );
}
