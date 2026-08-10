using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

public static class CephPilotStateTransitions
{
    private static readonly IReadOnlyDictionary<CephPilotProjectStatus, HashSet<CephPilotProjectStatus>> ProjectTransitions =
        new Dictionary<CephPilotProjectStatus, HashSet<CephPilotProjectStatus>>
        {
            [CephPilotProjectStatus.Draft] = [CephPilotProjectStatus.ReadyForAnnotation, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.ReadyForAnnotation] = [CephPilotProjectStatus.AnnotationInProgress, CephPilotProjectStatus.Draft, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.AnnotationInProgress] = [CephPilotProjectStatus.AwaitingAdjudication, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.AwaitingAdjudication] = [CephPilotProjectStatus.AdjudicationInProgress, CephPilotProjectStatus.AnnotationInProgress, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.AdjudicationInProgress] = [CephPilotProjectStatus.GoldStandardComplete, CephPilotProjectStatus.AnnotationInProgress, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.GoldStandardComplete] = [CephPilotProjectStatus.ReleaseReady, CephPilotProjectStatus.AdjudicationInProgress, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.ReleaseReady] = [CephPilotProjectStatus.Evaluated, CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.Evaluated] = [CephPilotProjectStatus.Archived],
            [CephPilotProjectStatus.Archived] = [],
        };

    private static readonly IReadOnlyDictionary<CephPilotCaseStatus, HashSet<CephPilotCaseStatus>> CaseTransitions =
        new Dictionary<CephPilotCaseStatus, HashSet<CephPilotCaseStatus>>
        {
            [CephPilotCaseStatus.Uploaded] = [CephPilotCaseStatus.Ready],
            [CephPilotCaseStatus.Ready] = [CephPilotCaseStatus.ReviewerAInProgress, CephPilotCaseStatus.ReviewerBInProgress],
            [CephPilotCaseStatus.ReviewerAInProgress] = [CephPilotCaseStatus.ReviewerASubmitted],
            [CephPilotCaseStatus.ReviewerASubmitted] = [CephPilotCaseStatus.ReviewerBInProgress, CephPilotCaseStatus.ComparisonReady],
            [CephPilotCaseStatus.ReviewerBInProgress] = [CephPilotCaseStatus.ReviewerBSubmitted],
            [CephPilotCaseStatus.ReviewerBSubmitted] = [CephPilotCaseStatus.ReviewerAInProgress, CephPilotCaseStatus.ComparisonReady],
            [CephPilotCaseStatus.ComparisonReady] = [CephPilotCaseStatus.NeedsAdjudication, CephPilotCaseStatus.Adjudicated],
            [CephPilotCaseStatus.NeedsAdjudication] = [CephPilotCaseStatus.Adjudicated],
            [CephPilotCaseStatus.Adjudicated] = [CephPilotCaseStatus.ReleaseReady],
            [CephPilotCaseStatus.ReleaseReady] = [],
        };

    public static bool CanTransition(CephPilotProjectStatus current, CephPilotProjectStatus next) =>
        current == next || ProjectTransitions[current].Contains(next);

    public static bool CanTransition(CephPilotCaseStatus current, CephPilotCaseStatus next) =>
        current == next || CaseTransitions[current].Contains(next);
}
