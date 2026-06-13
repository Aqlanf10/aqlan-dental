using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Validators;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Regression tests for the create-analysis validator. The frontend new-analysis
/// form offers "full" (شامل) and "wits", which the engine supports — the
/// validator must accept them so creating an analysis with those types does not
/// fail with the generic «نوع التحليل غير صالح».
/// </summary>
public class CephCreateValidatorTests
{
    private readonly CreateCephAnalysisRequestValidator _validator = new();

    private static CreateCephAnalysisRequest Req(string analysisType) => new()
    {
        OrthoCaseId = Guid.NewGuid(),
        AnalysisType = analysisType,
    };

    [Theory]
    [InlineData("full")]
    [InlineData("steiner")]
    [InlineData("tweed")]
    [InlineData("mcnamara")]
    [InlineData("ricketts")]
    [InlineData("downs")]
    [InlineData("jarabak")]
    [InlineData("wits")]
    public void AcceptsEveryAnalysisTypeOfferedByTheForm(string type)
    {
        var result = _validator.Validate(Req(type));
        result.IsValid.Should().BeTrue($"'{type}' is a valid analysis type offered by the UI");
    }

    [Theory]
    [InlineData("")]
    [InlineData("bogus")]
    public void RejectsUnknownAnalysisType(string type)
    {
        var result = _validator.Validate(Req(type));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCephAnalysisRequest.AnalysisType));
    }

    [Fact]
    public void RejectsEmptyOrthoCaseId()
    {
        var result = _validator.Validate(new CreateCephAnalysisRequest { OrthoCaseId = Guid.Empty, AnalysisType = "full" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCephAnalysisRequest.OrthoCaseId));
    }

    [Theory]
    [InlineData("/uploads/12345678-abcd.jpg")]
    [InlineData("https://pacs.example.org/images/ceph-1.png")]
    [InlineData("http://localhost:5000/uploads/ceph.webp")]
    public void AcceptsSafeXrayUrls(string url)
    {
        var request = Req("full");
        request.XrayFileUrl = url;

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/uploads/../secret.png")]
    [InlineData("/uploads/folder/image.png")]
    [InlineData("not-a-url")]
    public void RejectsUnsafeOrMalformedXrayUrls(string url)
    {
        var request = Req("full");
        request.XrayFileUrl = url;

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCephAnalysisRequest.XrayFileUrl));
    }

    [Fact]
    public void RejectsExcessivelyLongNotes()
    {
        var request = Req("full");
        request.Notes = new string('x', 2001);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCephAnalysisRequest.Notes));
    }
}
