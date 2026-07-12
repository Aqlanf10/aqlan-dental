using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Validators;

/// <summary>
/// Spec 010 (RX-REQ-001/006): the radiology-order create request must carry a
/// patient and a study type from the allowed set (aligned with
/// Radiograph.XrayType), with Arabic validation messages.
/// </summary>
public class RadiologyOrderValidatorTests
{
    private readonly CreateRadiologyOrderRequestValidator _validator = new();

    [Fact]
    public void EmptyPatient_ShouldFail()
    {
        var result = _validator.Validate(new CreateRadiologyOrderRequest
        {
            PatientId = Guid.Empty,
            StudyType = "panoramic"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PatientId");
    }

    [Theory]
    [InlineData("panoramic")]
    [InlineData("cbct_3d")]
    [InlineData("lateral_ceph")]
    [InlineData("pa_ceph")]
    [InlineData("bitewing")]
    [InlineData("periapical")]
    [InlineData("other")]
    public void AllowedStudyTypes_ShouldPass(string studyType)
    {
        var result = _validator.Validate(new CreateRadiologyOrderRequest
        {
            PatientId = Guid.NewGuid(),
            StudyType = studyType
        });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("mri")]
    [InlineData("PANORAMIC")]
    public void UnknownStudyType_ShouldFailWithArabicMessage(string studyType)
    {
        var result = _validator.Validate(new CreateRadiologyOrderRequest
        {
            PatientId = Guid.NewGuid(),
            StudyType = studyType
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StudyType");
        result.Errors.Should().OnlyContain(e => System.Text.RegularExpressions.Regex.IsMatch(e.ErrorMessage, "[؀-ۿ]"));
    }

    [Fact]
    public void OverlongRegionAndNotes_ShouldFail()
    {
        var result = _validator.Validate(new CreateRadiologyOrderRequest
        {
            PatientId = Guid.NewGuid(),
            StudyType = "panoramic",
            Region = new string('x', 201),
            ClinicalNotes = new string('x', 1001)
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Region");
        result.Errors.Should().Contain(e => e.PropertyName == "ClinicalNotes");
    }
}
