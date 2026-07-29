
import { CastAnalysisPanel } from "@/components/ortho/CastAnalysisPanel";

/**
 * Model-analysis tab — a thin wrapper around the shared
 * `CastAnalysisPanel` (`@/components/ortho/CastAnalysisPanel`). Co-located in
 * the ortho case tab components (FE-20) so the page shell renders a single
 * `OrthoXxxTab` per tab. No internal logic to extract; the heavy lifting lives
 * in the shared component.
 */
export function OrthoModelAnalysisTab({ caseId }: { caseId: string }) {
  return <CastAnalysisPanel caseId={caseId} />;
}
