using System.Text.RegularExpressions;
using AqlanDentalPro.Application.DTOs.Ceph;

namespace AqlanDentalPro.Application.Services;

public static partial class CephBenchmarkManifestValidator
{
    public const string SchemaVersion = "1.0";
    public const string LandmarkDefinitionVersion = "ADP-LM-LAT-v1";
    public const double AdjudicationThresholdMm = 1.5;

    public static readonly HashSet<string> CoreLandmarkKeys =
    [
        "S", "N", "Or", "Po", "ANS", "PNS", "A", "B", "Pog", "Gn", "Me", "Go",
        "Co", "Ar", "D", "Pm", "U1T", "U1A", "L1T", "L1A", "LS", "LI", "Pn", "Cm",
    ];

    public static readonly HashSet<string> AllowedLandmarkKeys =
        [.. CoreLandmarkKeys, "SPog", "U6", "L6"];

    public static CephBenchmarkValidationResultDto Validate(CephBenchmarkManifestDto? manifest)
    {
        var result = new CephBenchmarkValidationResultDto();
        if (manifest is null)
        {
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "manifest.required", "$");
            return FinalizeResult(result);
        }

        ValidateManifestHeader(manifest, result);
        result.CaseCount = manifest.Cases?.Count ?? 0;
        result.GoldPointCount = manifest.Cases?.Sum(item => item?.GoldStandard?.Count ?? 0) ?? 0;

        if (manifest.Cases is null || manifest.Cases.Count == 0)
        {
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "cases.required", "$.cases");
            return FinalizeResult(result);
        }
        if (manifest.Cases.Count > 10_000)
        {
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "cases.limit", "$.cases");
        }

        var imageIds = new HashSet<Guid>();
        var imageHashes = new HashSet<string>(StringComparer.Ordinal);
        var patientSplits = new Dictionary<string, CephBenchmarkSplit>(StringComparer.Ordinal);

        for (var index = 0; index < manifest.Cases.Count; index++)
        {
            var item = manifest.Cases[index];
            var path = $"$.cases[{index}]";

            if (item is null)
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "case.required", path);
                continue;
            }

            if (item.ImageId == Guid.Empty || !imageIds.Add(item.ImageId))
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "imageId.invalid-or-duplicate", $"{path}.imageId");
            }
            if (!Sha256Regex().IsMatch(item.ImageSha256) || !imageHashes.Add(item.ImageSha256))
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "imageSha256.invalid-or-duplicate", $"{path}.imageSha256");
            }
            if (!Sha256Regex().IsMatch(item.PatientGroupId))
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                    "patientGroupId.must-be-salted-sha256", $"{path}.patientGroupId");
            }
            else if (patientSplits.TryGetValue(item.PatientGroupId, out var existingSplit)
                     && existingSplit != item.Split)
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Leakage,
                    "patientGroupId.cross-split-leakage", $"{path}.split");
            }
            else
            {
                patientSplits[item.PatientGroupId] = item.Split;
            }

            ValidateCase(item, path, result);
        }

        return FinalizeResult(result);
    }

    private static void ValidateManifestHeader(
        CephBenchmarkManifestDto manifest,
        CephBenchmarkValidationResultDto result)
    {
        if (!string.Equals(manifest.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "schemaVersion.unsupported", "$.schemaVersion");
        if (!VersionTokenRegex().IsMatch(manifest.DatasetVersion))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "datasetVersion.invalid", "$.datasetVersion");
        if (!string.Equals(manifest.LandmarkDefinitionVersion, LandmarkDefinitionVersion, StringComparison.Ordinal))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "landmarkDefinitionVersion.unsupported", "$.landmarkDefinitionVersion");
        if (!IsUtcTimestamp(manifest.CreatedAt))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "createdAt.utc-required", "$.createdAt");
    }

    private static void ValidateCase(
        CephBenchmarkCaseDto item,
        string path,
        CephBenchmarkValidationResultDto result)
    {
        if (item.ImageWidth <= 0 || item.ImageWidth > 100_000)
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "imageWidth.invalid", $"{path}.imageWidth");
        if (item.ImageHeight <= 0 || item.ImageHeight > 100_000)
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "imageHeight.invalid", $"{path}.imageHeight");
        if (!double.IsFinite(item.MillimetresPerPixel) || item.MillimetresPerPixel <= 0 || item.MillimetresPerPixel > 10)
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "millimetresPerPixel.invalid", $"{path}.millimetresPerPixel");
        if (!OpaqueCodeRegex().IsMatch(item.SourceSiteCode))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "sourceSiteCode.opaque-code-required", $"{path}.sourceSiteCode");
        if (!OpaqueCodeRegex().IsMatch(item.DeviceCode))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "deviceCode.opaque-code-required", $"{path}.deviceCode");
        if (item.Split == CephBenchmarkSplit.Unspecified || !Enum.IsDefined(item.Split))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "split.required", $"{path}.split");
        if (item.Orientation != CephImageOrientation.RightFacing)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Annotation,
                "orientation.right-facing-required", $"{path}.orientation");
        if (item.QualityFlags is null || item.QualityFlags.Count == 0)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Annotation,
                "qualityFlags.required", $"{path}.qualityFlags");
        else if (item.QualityFlags.Any(flag => !Enum.IsDefined(flag)))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "qualityFlags.invalid", $"{path}.qualityFlags");
        if (!Enum.IsDefined(item.AgeBand))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "ageBand.invalid", $"{path}.ageBand");
        if (!Enum.IsDefined(item.SkeletalClass))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "skeletalClass.invalid", $"{path}.skeletalClass");
        if (!Enum.IsDefined(item.AnglePattern))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                "anglePattern.invalid", $"{path}.anglePattern");

        ValidateDeidentification(item.Deidentification, $"{path}.deidentification", result);
        ValidateAnnotations(item, path, result);
    }

    private static void ValidateDeidentification(
        CephDeidentificationEvidenceDto? evidence,
        string path,
        CephBenchmarkValidationResultDto result)
    {
        if (evidence is null)
        {
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Privacy,
                "deidentification.required", path);
            return;
        }

        if (!VersionTokenRegex().IsMatch(evidence.MetadataProfileVersion))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "metadataProfileVersion.invalid", $"{path}.metadataProfileVersion");
        if (!IsUtcTimestamp(evidence.SanitizedAt))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "sanitizedAt.utc-required", $"{path}.sanitizedAt");
        if (!StewardAliasRegex().IsMatch(evidence.StewardAlias))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "stewardAlias.invalid", $"{path}.stewardAlias");
        if (!ApprovalIdRegex().IsMatch(evidence.DataUseApprovalId))
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                "dataUseApprovalId.invalid", $"{path}.dataUseApprovalId");
        if (!evidence.MetadataSanitized)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Privacy,
                "metadataSanitized.required", $"{path}.metadataSanitized");
        if (!evidence.PrivateTagsRemoved)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Privacy,
                "privateTagsRemoved.required", $"{path}.privateTagsRemoved");
        if (evidence.PixelInspectionStatus != CephPixelInspectionStatus.Passed)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Privacy,
                "pixelInspectionStatus.passed-required", $"{path}.pixelInspectionStatus");
        if (evidence.BurnedInIdentifierDetected)
            AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Privacy,
                "burnedInIdentifierDetected.must-be-false", $"{path}.burnedInIdentifierDetected");
    }

    private static void ValidateAnnotations(
        CephBenchmarkCaseDto item,
        string path,
        CephBenchmarkValidationResultDto result)
    {
        item.Annotations ??= [];
        item.GoldStandard ??= [];

        var duplicateAnnotations = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < item.Annotations.Count; index++)
        {
            var annotation = item.Annotations[index];
            var annotationPath = $"{path}.annotations[{index}]";
            if (annotation is null)
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "annotation.required", annotationPath);
                continue;
            }
            if (!AllowedLandmarkKeys.Contains(annotation.LandmarkKey))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Annotation,
                    "landmarkKey.unsupported", $"{annotationPath}.landmarkKey");
            if (!ReviewerAliasRegex().IsMatch(annotation.ReviewerAlias))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                    "reviewerAlias.invalid", $"{annotationPath}.reviewerAlias");
            if (!duplicateAnnotations.Add($"{annotation.LandmarkKey}\u001f{annotation.ReviewerAlias}"))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Annotation,
                    "annotation.duplicate-reviewer-landmark", annotationPath);
            if (!IsUtcTimestamp(annotation.AnnotatedAt))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Annotation,
                    "annotatedAt.utc-required", $"{annotationPath}.annotatedAt");
            ValidateCoordinates(annotation.Visibility, annotation.X, annotation.Y,
                item.ImageWidth, item.ImageHeight, annotationPath, result);
            if (!Enum.IsDefined(annotation.Visibility))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "visibility.invalid", $"{annotationPath}.visibility");
            if (!Enum.IsDefined(annotation.DoubleContourDecision))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "doubleContourDecision.invalid", $"{annotationPath}.doubleContourDecision");
            if (annotation.DoubleContourDecision == CephDoubleContourDecision.Unresolved)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "doubleContourDecision.unresolved", $"{annotationPath}.doubleContourDecision");
        }

        var goldByKey = new Dictionary<string, CephGoldStandardPointDto>(StringComparer.Ordinal);
        for (var index = 0; index < item.GoldStandard.Count; index++)
        {
            var gold = item.GoldStandard[index];
            var goldPath = $"{path}.goldStandard[{index}]";
            if (gold is null)
            {
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "goldStandard.required", goldPath);
                continue;
            }
            if (!AllowedLandmarkKeys.Contains(gold.LandmarkKey) || !goldByKey.TryAdd(gold.LandmarkKey, gold))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.unsupported-or-duplicate-key", $"{goldPath}.landmarkKey");
            if (!ReviewerAliasRegex().IsMatch(gold.ApprovedByAlias))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Privacy,
                    "approvedByAlias.invalid", $"{goldPath}.approvedByAlias");
            if (!IsUtcTimestamp(gold.ApprovedAt))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Adjudication,
                    "approvedAt.utc-required", $"{goldPath}.approvedAt");
            if (gold.Visibility == CephLandmarkVisibility.Uncertain)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.visibility-cannot-be-uncertain", $"{goldPath}.visibility");
            ValidateCoordinates(gold.Visibility, gold.X, gold.Y,
                item.ImageWidth, item.ImageHeight, goldPath, result);
            if (!Enum.IsDefined(gold.Visibility))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "visibility.invalid", $"{goldPath}.visibility");
            if (!Enum.IsDefined(gold.Method))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "goldStandard.method.invalid", $"{goldPath}.method");
            if (gold.DecisionCodes is null || gold.DecisionCodes.Count == 0)
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "goldStandard.decisionCodes.required", $"{goldPath}.decisionCodes");
            else if (gold.DecisionCodes.Any(code => !Enum.IsDefined(code)))
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Structure,
                    "goldStandard.decisionCodes.invalid", $"{goldPath}.decisionCodes");
            if (gold.Visibility == CephLandmarkVisibility.NotVisible
                && !(gold.DecisionCodes?.Contains(CephGoldStandardDecisionCode.AnatomyNotVisible) ?? false))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.not-visible-decision-code", $"{goldPath}.decisionCodes");
            if (gold.Visibility == CephLandmarkVisibility.Visible
                && (gold.DecisionCodes?.Contains(CephGoldStandardDecisionCode.AnatomyNotVisible) ?? false))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.visible-anatomy-not-visible-conflict", $"{goldPath}.decisionCodes");
        }

        var annotationsByKey = item.Annotations
            .Where(annotation => annotation is not null && AllowedLandmarkKeys.Contains(annotation.LandmarkKey))
            .GroupBy(annotation => annotation.LandmarkKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var evaluatedKeys = CoreLandmarkKeys
            .Concat(annotationsByKey.Keys)
            .Concat(goldByKey.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in evaluatedKeys)
        {
            var annotations = annotationsByKey.GetValueOrDefault(key) ?? [];
            var reviewers = annotations.Select(annotation => annotation.ReviewerAlias)
                .Where(alias => ReviewerAliasRegex().IsMatch(alias))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            var visibilityConflict = annotations.Select(annotation => annotation.Visibility).Distinct().Count() > 1;
            var doubleContourConflict = annotations.Select(annotation => annotation.DoubleContourDecision).Distinct().Count() > 1;
            var maximumDisagreementMm = CalculateMaximumDisagreementMm(annotations, item.MillimetresPerPixel);
            var coordinateConflict = maximumDisagreementMm > AdjudicationThresholdMm;
            var requiresAdjudication = visibilityConflict || doubleContourConflict || coordinateConflict;
            var hasGold = goldByKey.TryGetValue(key, out var gold);

            result.AdjudicationQueue.Add(new CephAdjudicationQueueItemDto
            {
                ImageId = item.ImageId,
                LandmarkKey = key,
                ReviewerCount = reviewers.Count,
                MaximumDisagreementMm = maximumDisagreementMm,
                VisibilityConflict = visibilityConflict,
                DoubleContourConflict = doubleContourConflict,
                RequiresAdjudication = requiresAdjudication,
                HasGoldStandard = hasGold,
            });

            if (CoreLandmarkKeys.Contains(key) && reviewers.Count < 2)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Annotation,
                    "core-landmark.two-independent-reviewers-required", $"{path}.landmarks.{key}");
            if (CoreLandmarkKeys.Contains(key) && !hasGold)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "core-landmark.explicit-gold-standard-required", $"{path}.goldStandard.{key}");

            if (!hasGold || gold is null) continue;

            if (requiresAdjudication && gold.Method == CephGoldStandardMethod.ConsensusWithinThreshold)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.adjudication-method-required", $"{path}.goldStandard.{key}.method");
            if (coordinateConflict
                && !(gold.DecisionCodes?.Contains(CephGoldStandardDecisionCode.CoordinateDisagreementResolved) ?? false))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.coordinate-resolution-required", $"{path}.goldStandard.{key}.decisionCodes");
            if (visibilityConflict
                && !(gold.DecisionCodes?.Contains(CephGoldStandardDecisionCode.VisibilityResolved) ?? false))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.visibility-resolution-required", $"{path}.goldStandard.{key}.decisionCodes");
            if (doubleContourConflict
                && !(gold.DecisionCodes?.Contains(CephGoldStandardDecisionCode.DoubleContourResolved) ?? false))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.double-contour-resolution-required", $"{path}.goldStandard.{key}.decisionCodes");
            if (gold.Method == CephGoldStandardMethod.ThirdReviewer && reviewers.Count < 3)
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.third-reviewer-annotation-required", $"{path}.goldStandard.{key}.method");
            if (gold.Method == CephGoldStandardMethod.ThirdReviewer
                && !reviewers.Contains(gold.ApprovedByAlias))
                AddIssue(result, CephBenchmarkIssueSeverity.Blocker, CephBenchmarkIssueCategory.Adjudication,
                    "goldStandard.third-reviewer-must-approve", $"{path}.goldStandard.{key}.approvedByAlias");
        }
    }

    private static void ValidateCoordinates(
        CephLandmarkVisibility visibility,
        double? x,
        double? y,
        int imageWidth,
        int imageHeight,
        string path,
        CephBenchmarkValidationResultDto result)
    {
        if (visibility == CephLandmarkVisibility.NotVisible)
        {
            if (x.HasValue || y.HasValue)
                AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Annotation,
                    "coordinates.must-be-null-when-not-visible", path);
            return;
        }

        if (!x.HasValue || !y.HasValue || !double.IsFinite(x.Value) || !double.IsFinite(y.Value)
            || x.Value < 0 || y.Value < 0 || x.Value > imageWidth || y.Value > imageHeight)
        {
            AddIssue(result, CephBenchmarkIssueSeverity.Error, CephBenchmarkIssueCategory.Annotation,
                "coordinates.invalid-or-out-of-bounds", path);
        }
    }

    private static double? CalculateMaximumDisagreementMm(
        IReadOnlyList<CephReviewerAnnotationDto> annotations,
        double millimetresPerPixel)
    {
        if (!double.IsFinite(millimetresPerPixel) || millimetresPerPixel <= 0) return null;
        var points = annotations
            .Where(annotation => annotation.Visibility == CephLandmarkVisibility.Visible
                && annotation.X.HasValue && annotation.Y.HasValue
                && double.IsFinite(annotation.X.Value) && double.IsFinite(annotation.Y.Value))
            .ToList();
        if (points.Count < 2) return null;

        var maximum = 0d;
        for (var left = 0; left < points.Count - 1; left++)
        {
            for (var right = left + 1; right < points.Count; right++)
            {
                var dx = points[left].X!.Value - points[right].X!.Value;
                var dy = points[left].Y!.Value - points[right].Y!.Value;
                maximum = Math.Max(maximum, Math.Sqrt(dx * dx + dy * dy) * millimetresPerPixel);
            }
        }
        return Math.Round(maximum, 4, MidpointRounding.AwayFromZero);
    }

    private static bool IsUtcTimestamp(DateTimeOffset value) =>
        value != default && value.Offset == TimeSpan.Zero;

    private static void AddIssue(
        CephBenchmarkValidationResultDto result,
        CephBenchmarkIssueSeverity severity,
        CephBenchmarkIssueCategory category,
        string code,
        string path) =>
        result.Issues.Add(new CephBenchmarkValidationIssueDto
        {
            Severity = severity,
            Category = category,
            Code = code,
            Path = path,
        });

    private static CephBenchmarkValidationResultDto FinalizeResult(CephBenchmarkValidationResultDto result)
    {
        result.Issues = result.Issues
            .OrderBy(issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();
        result.AdjudicationQueue = result.AdjudicationQueue
            .OrderBy(item => item.ImageId)
            .ThenBy(item => item.LandmarkKey, StringComparer.Ordinal)
            .ToList();
        result.IsStructurallyValid = result.Issues.All(issue => issue.Severity != CephBenchmarkIssueSeverity.Error);
        result.IsReleaseReady = result.IsStructurallyValid
            && result.Issues.All(issue => issue.Severity != CephBenchmarkIssueSeverity.Blocker);
        return result;
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTokenRegex();

    [GeneratedRegex("^[A-Z][A-Z0-9-]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex OpaqueCodeRegex();

    [GeneratedRegex("^REV-[A-Z0-9]{4,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ReviewerAliasRegex();

    [GeneratedRegex("^STW-[A-Z0-9]{4,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex StewardAliasRegex();

    [GeneratedRegex("^APP-[A-Z0-9-]{4,48}$", RegexOptions.CultureInvariant)]
    private static partial Regex ApprovalIdRegex();
}
