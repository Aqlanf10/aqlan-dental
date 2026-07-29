using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephBenchmarkManifestTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidManifest_IsReleaseReady_WithoutOptionalLandmarks()
    {
        var manifest = BuildValidManifest();

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeTrue();
        result.IsReleaseReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.GoldPointCount.Should().Be(24);
        result.AdjudicationQueue.Should().HaveCount(24);
        result.AdjudicationQueue.Should().OnlyContain(item => !item.RequiresAdjudication);
    }

    [Fact]
    public void SamePatientAcrossSplits_IsRejectedAsLeakage()
    {
        var manifest = BuildValidManifest();
        var second = BuildValidCase('b', Guid.Parse("22222222-2222-2222-2222-222222222222"));
        second.PatientGroupId = manifest.Cases[0].PatientGroupId;
        second.Split = CephBenchmarkSplit.ExternalClinicalTest;
        manifest.Cases.Add(second);

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeFalse();
        result.IsReleaseReady.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "patientGroupId.cross-split-leakage"
            && issue.Category == CephBenchmarkIssueCategory.Leakage);
    }

    [Fact]
    public void DisagreementAboveThreshold_RequiresExplicitAdjudication()
    {
        var manifest = BuildValidManifest();
        var item = manifest.Cases[0];
        var firstS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0001");
        var secondS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0002");
        secondS.X = firstS.X + 20; // 2.0 mm at 0.1 mm/px.
        secondS.Y = firstS.Y;

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsReleaseReady.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "goldStandard.adjudication-method-required");
        result.AdjudicationQueue.Single(queue => queue.LandmarkKey == "S")
            .Should().Match<CephAdjudicationQueueItemDto>(queue =>
                queue.RequiresAdjudication && queue.MaximumDisagreementMm == 2.0);
    }

    [Fact]
    public void ThirdReviewer_ResolvesLargeDisagreement_WithoutAutomaticAveraging()
    {
        var manifest = BuildValidManifest();
        var item = manifest.Cases[0];
        var firstS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0001");
        var secondS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0002");
        secondS.X = firstS.X + 20;
        secondS.Y = firstS.Y;
        var adjudicatedX = firstS.X + 3;
        var adjudicatedY = firstS.Y;
        item.Annotations.Add(new CephReviewerAnnotationDto
        {
            LandmarkKey = "S",
            ReviewerAlias = "REV-0003",
            Visibility = CephLandmarkVisibility.Visible,
            X = adjudicatedX,
            Y = adjudicatedY,
            DoubleContourDecision = CephDoubleContourDecision.NotApplicable,
            AnnotatedAt = FixedUtc,
        });
        var gold = item.GoldStandard.Single(point => point.LandmarkKey == "S");
        gold.X = adjudicatedX;
        gold.Y = adjudicatedY;
        gold.Method = CephGoldStandardMethod.ThirdReviewer;
        gold.DecisionCodes = [CephGoldStandardDecisionCode.CoordinateDisagreementResolved];
        gold.ApprovedByAlias = "REV-0003";

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsReleaseReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        item.GoldStandard.Single(point => point.LandmarkKey == "S").X.Should().Be(adjudicatedX,
            "the validator must preserve the explicit adjudicated coordinate rather than calculate a mean");
    }

    [Fact]
    public void MissingAnatomy_CanBeExplicitlyAdjudicatedAsNotVisible()
    {
        var manifest = BuildValidManifest();
        var item = manifest.Cases[0];
        foreach (var annotation in item.Annotations.Where(annotation => annotation.LandmarkKey == "Co"))
        {
            annotation.Visibility = CephLandmarkVisibility.NotVisible;
            annotation.X = null;
            annotation.Y = null;
        }
        var gold = item.GoldStandard.Single(point => point.LandmarkKey == "Co");
        gold.Visibility = CephLandmarkVisibility.NotVisible;
        gold.X = null;
        gold.Y = null;
        gold.DecisionCodes = [CephGoldStandardDecisionCode.AnatomyNotVisible];
        item.QualityFlags.Add(CephImageQualityFlag.MissingAnatomy);

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsReleaseReady.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true, CephPixelInspectionStatus.Passed, false, "metadataSanitized.required")]
    [InlineData(true, false, CephPixelInspectionStatus.Passed, false, "privateTagsRemoved.required")]
    [InlineData(true, true, CephPixelInspectionStatus.Pending, false, "pixelInspectionStatus.passed-required")]
    [InlineData(true, true, CephPixelInspectionStatus.Passed, true, "burnedInIdentifierDetected.must-be-false")]
    public void IncompleteDeidentification_BlocksRelease(
        bool metadataSanitized,
        bool privateTagsRemoved,
        CephPixelInspectionStatus pixelStatus,
        bool burnedInIdentifierDetected,
        string expectedCode)
    {
        var manifest = BuildValidManifest();
        var evidence = manifest.Cases[0].Deidentification;
        evidence.MetadataSanitized = metadataSanitized;
        evidence.PrivateTagsRemoved = privateTagsRemoved;
        evidence.PixelInspectionStatus = pixelStatus;
        evidence.BurnedInIdentifierDetected = burnedInIdentifierDetected;

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeTrue();
        result.IsReleaseReady.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue => issue.Code == expectedCode);
    }

    [Fact]
    public void PatientGroupId_MustBeOpaqueSha256_NotAClinicalIdentifier()
    {
        var manifest = BuildValidManifest();
        manifest.Cases[0].PatientGroupId = "PATIENT-12345";

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "patientGroupId.must-be-salted-sha256"
            && issue.Category == CephBenchmarkIssueCategory.Privacy);
    }

    [Fact]
    public void ExactAdjudicationThreshold_DoesNotTriggerLargeDisagreementRule()
    {
        var manifest = BuildValidManifest();
        var firstS = manifest.Cases[0].Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0001");
        var secondS = manifest.Cases[0].Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0002");
        secondS.X = firstS.X + 15;
        secondS.Y = firstS.Y;

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsReleaseReady.Should().BeTrue();
        result.AdjudicationQueue.Single(queue => queue.LandmarkKey == "S")
            .RequiresAdjudication.Should().BeFalse();
    }

    [Fact]
    public void CombinedConflicts_RequireEveryApplicableResolutionCode()
    {
        var manifest = BuildValidManifest();
        var item = manifest.Cases[0];
        var firstS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0001");
        var secondS = item.Annotations.Single(annotation =>
            annotation.LandmarkKey == "S" && annotation.ReviewerAlias == "REV-0002");
        secondS.X = firstS.X + 20;
        secondS.Y = firstS.Y;
        secondS.DoubleContourDecision = CephDoubleContourDecision.ReceptorSide;
        item.Annotations.Add(new CephReviewerAnnotationDto
        {
            LandmarkKey = "S",
            ReviewerAlias = "REV-0003",
            Visibility = CephLandmarkVisibility.Uncertain,
            X = firstS.X,
            Y = firstS.Y,
            DoubleContourDecision = CephDoubleContourDecision.NotApplicable,
            AnnotatedAt = FixedUtc,
        });
        var gold = item.GoldStandard.Single(point => point.LandmarkKey == "S");
        gold.Method = CephGoldStandardMethod.ConsensusPanel;
        gold.DecisionCodes = [CephGoldStandardDecisionCode.CoordinateDisagreementResolved];

        var incomplete = CephBenchmarkManifestValidator.Validate(manifest);

        incomplete.IsReleaseReady.Should().BeFalse();
        incomplete.Issues.Should().Contain(issue =>
            issue.Code == "goldStandard.visibility-resolution-required");
        incomplete.Issues.Should().Contain(issue =>
            issue.Code == "goldStandard.double-contour-resolution-required");

        gold.DecisionCodes.Add(CephGoldStandardDecisionCode.VisibilityResolved);
        gold.DecisionCodes.Add(CephGoldStandardDecisionCode.DoubleContourResolved);

        CephBenchmarkManifestValidator.Validate(manifest).IsReleaseReady.Should().BeTrue();
    }

    [Fact]
    public void Contract_DoesNotExposePatientOrImageLocationFields()
    {
        var contract = typeof(CephBenchmarkCaseDto).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        contract.Should().NotContain("PatientName");
        contract.Should().NotContain("PatientId");
        contract.Should().NotContain("DateOfBirth");
        contract.Should().NotContain("FilePath");
        contract.Should().NotContain("ImageUrl");

        var json = JsonSerializer.Serialize(BuildValidManifest());
        json.Should().NotContain("patientName");
        json.Should().NotContain("dateOfBirth");
        json.Should().NotContain("filePath");
        json.Should().NotContain("imageUrl");
    }

    [Fact]
    public void Controller_IsAdminOnly_AndValidationIsStateless()
    {
        var authorize = typeof(CephBenchmarkController)
            .GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be("AdminOnly");

        var controller = new CephBenchmarkController();
        var response = controller.Validate(ToJsonElement(BuildValidManifest()))
            .Should().BeOfType<OkObjectResult>().Subject;
        response.Value.Should().BeOfType<CephBenchmarkValidationResultDto>()
            .Which.IsReleaseReady.Should().BeTrue();
    }

    [Fact]
    public void NullNestedItems_ReturnStructuredErrors_InsteadOfThrowing()
    {
        var manifest = BuildValidManifest();
        manifest.Cases.Add(null!);
        manifest.Cases[0].Annotations.Add(null!);
        manifest.Cases[0].GoldStandard.Add(null!);

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "case.required");
        result.Issues.Should().Contain(issue => issue.Code == "annotation.required");
        result.Issues.Should().Contain(issue => issue.Code == "goldStandard.required");
    }

    [Fact]
    public void UndefinedEnumValues_AreRejectedAsStructuralErrors()
    {
        var manifest = BuildValidManifest();
        manifest.Cases[0].AgeBand = (CephAgeBand)999;
        manifest.Cases[0].Annotations[0].Visibility = (CephLandmarkVisibility)999;

        var result = CephBenchmarkManifestValidator.Validate(manifest);

        result.IsStructurallyValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "ageBand.invalid");
        result.Issues.Should().Contain(issue => issue.Code == "visibility.invalid");
    }

    [Fact]
    public void JsonContract_RejectsUndeclaredPropertiesAndNumericEnums()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(BuildValidManifest(), options);

        var withUndeclaredProperty = json.Replace(
            "\"schemaVersion\":",
            "\"patientName\":\"synthetic-only\",\"schemaVersion\":",
            StringComparison.Ordinal);
        var withNumericEnum = json.Replace(
            "\"split\":\"InternalTest\"",
            "\"split\":1",
            StringComparison.Ordinal);
        var withCompositeEnum = json.Replace(
            "\"split\":\"InternalTest\"",
            "\"split\":\"Training, Validation\"",
            StringComparison.Ordinal);

        withUndeclaredProperty.Should().NotBe(json);
        withNumericEnum.Should().NotBe(json);
        withCompositeEnum.Should().NotBe(json);

        var deserializeUndeclared = () =>
            JsonSerializer.Deserialize<CephBenchmarkManifestDto>(withUndeclaredProperty, options);
        var deserializeNumeric = () =>
            JsonSerializer.Deserialize<CephBenchmarkManifestDto>(withNumericEnum, options);
        var deserializeComposite = () =>
            JsonSerializer.Deserialize<CephBenchmarkManifestDto>(withCompositeEnum, options);

        deserializeUndeclared.Should().Throw<JsonException>();
        deserializeNumeric.Should().Throw<JsonException>();
        deserializeComposite.Should().Throw<JsonException>();
    }

    [Fact]
    public void Controller_UsesStrictJsonContract_DespiteGlobalMvcEnumConverter()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(BuildValidManifest(), options);
        var numeric = json.Replace("\"split\":\"InternalTest\"", "\"split\":3", StringComparison.Ordinal);
        var composite = json.Replace(
            "\"split\":\"InternalTest\"",
            "\"split\":\"Training, Validation\"",
            StringComparison.Ordinal);
        var undeclared = json.Replace(
            "\"schemaVersion\":",
            "\"patientName\":\"synthetic-only\",\"schemaVersion\":",
            StringComparison.Ordinal);
        var controller = new CephBenchmarkController();

        controller.Validate(ParseJsonElement(numeric)).Should().BeOfType<BadRequestObjectResult>();
        controller.Validate(ParseJsonElement(composite)).Should().BeOfType<BadRequestObjectResult>();
        controller.Validate(ParseJsonElement(undeclared)).Should().BeOfType<BadRequestObjectResult>();
    }

    private static JsonElement ToJsonElement(CephBenchmarkManifestDto manifest) =>
        JsonSerializer.SerializeToElement(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static JsonElement ParseJsonElement(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static CephBenchmarkManifestDto BuildValidManifest() => new()
    {
        SchemaVersion = CephBenchmarkManifestValidator.SchemaVersion,
        DatasetVersion = "pilot-001",
        LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
        CreatedAt = FixedUtc,
        Cases = [BuildValidCase('a', Guid.Parse("11111111-1111-1111-1111-111111111111"))],
    };

    private static CephBenchmarkCaseDto BuildValidCase(char hashCharacter, Guid imageId)
    {
        var item = new CephBenchmarkCaseDto
        {
            ImageId = imageId,
            PatientGroupId = new string(hashCharacter, 64),
            ImageSha256 = new string(hashCharacter, 64),
            ImageWidth = 2000,
            ImageHeight = 2000,
            MillimetresPerPixel = 0.1,
            SourceSiteCode = "SITE-001",
            DeviceCode = "DEV-001",
            Split = CephBenchmarkSplit.InternalTest,
            Orientation = CephImageOrientation.RightFacing,
            AgeBand = CephAgeBand.Adult,
            SkeletalClass = CephSkeletalClass.ClassI,
            AnglePattern = CephAnglePattern.Average,
            QualityFlags = [CephImageQualityFlag.Normal],
            Deidentification = new CephDeidentificationEvidenceDto
            {
                MetadataProfileVersion = "DICOM-PS3.15-v1",
                SanitizedAt = FixedUtc,
                StewardAlias = "STW-0001",
                DataUseApprovalId = "APP-CEPH-0001",
                MetadataSanitized = true,
                PrivateTagsRemoved = true,
                PixelInspectionStatus = CephPixelInspectionStatus.Passed,
                BurnedInIdentifierDetected = false,
            },
        };

        var index = 0;
        foreach (var key in CephBenchmarkManifestValidator.CoreLandmarkKeys.OrderBy(key => key))
        {
            var x = 100 + index * 10;
            var y = 100 + index * 8;
            item.Annotations.Add(Annotation(key, "REV-0001", x, y));
            item.Annotations.Add(Annotation(key, "REV-0002", x + 1, y + 1));
            item.GoldStandard.Add(new CephGoldStandardPointDto
            {
                LandmarkKey = key,
                Visibility = CephLandmarkVisibility.Visible,
                X = x + 0.5,
                Y = y + 0.5,
                Method = CephGoldStandardMethod.ConsensusWithinThreshold,
                DecisionCodes = [CephGoldStandardDecisionCode.CoordinateAgreement],
                ApprovedByAlias = "REV-0001",
                ApprovedAt = FixedUtc,
            });
            index++;
        }
        return item;
    }

    private static CephReviewerAnnotationDto Annotation(
        string key,
        string reviewerAlias,
        double x,
        double y) => new()
        {
            LandmarkKey = key,
            ReviewerAlias = reviewerAlias,
            Visibility = CephLandmarkVisibility.Visible,
            X = x,
            Y = y,
            DoubleContourDecision = CephDoubleContourDecision.NotApplicable,
            AnnotatedAt = FixedUtc,
        };
}
