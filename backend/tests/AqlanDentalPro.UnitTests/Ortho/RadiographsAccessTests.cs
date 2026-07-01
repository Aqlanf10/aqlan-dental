using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Security regression: RadiographsController must enforce per-patient access
/// control via <c>[ServiceFilter(typeof(PatientAccessFilter))]</c> at the class
/// level (route-bound patientId) and via <c>DenyIfDoctorCannotAccess</c> inside
/// the body-bound <c>AddRadiograph</c> action.
///
/// These tests exercise the body-bound guard directly because the action filter
/// pipeline does not run when the controller is instantiated by hand. The
/// route-bound guard is covered by the integration suite
/// (<c>PatientAccessControlTests</c>) and by source inspection
/// (<c>[ServiceFilter(typeof(PatientAccessFilter))]</c> on the controller).
///
/// Three contracts pinned:
///   * A doctor (non-admin) gets HTTP 403 when adding a radiograph for a
///     patient they are not linked to.
///   * A doctor (non-admin) gets HTTP 200 when adding a radiograph for a
///     patient they ARE linked to.
///   * An admin (non-doctor) bypasses the per-patient check and gets HTTP 200
///     for any patient.
/// </summary>
public class RadiographsAccessTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly RadiographsController _controller;

    private static readonly Guid PatientAId = Guid.NewGuid();
    private static readonly Guid PatientBId = Guid.NewGuid();
    private static readonly Guid DoctorUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();

    public RadiographsAccessTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser.SetupGet(x => x.UserId).Returns(DoctorUserId);
        _currentUser.SetupGet(x => x.Role).Returns(UserRole.GeneralDentist);

        _controller = new RadiographsController(
            _db,
            _currentUser.Object,
            _patientAccess.Object,
            _audit.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Doctor_AddingRadiographForUnassignedPatient_Returns_403()
    {
        // CLIN-01: a doctor (non-admin) attempts to attach a radiograph to a
        // patient they have no link to. DenyIfDoctorCannotAccess must return
        // StatusCode(403) with the Arabic "not authorized" message before any
        // DB write occurs. No Radiograph row should be persisted.
        _patientAccess.SetupGet(x => x.IsDoctor).Returns(true);
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(PatientBId))
            .ReturnsAsync(false);

        var req = NewRadiographRequest(PatientBId);

        var result = await _controller.AddRadiograph(req);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _db.Radiographs.Should().BeEmpty(
            "the radiograph must not be persisted when access is denied");
    }

    [Fact]
    public async Task Doctor_AddingRadiographForOwnPatient_Returns_200()
    {
        // Mirror image of the 403 test: the doctor IS linked to the patient
        // (PatientAccessService returns true), so the radiograph is persisted
        // and the controller returns 200 OK with the new id.
        _patientAccess.SetupGet(x => x.IsDoctor).Returns(true);
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(PatientAId))
            .ReturnsAsync(true);

        var req = NewRadiographRequest(PatientAId);

        var result = await _controller.AddRadiograph(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        _db.Radiographs.Should().ContainSingle(
            "the radiograph must be persisted when access is granted");
        var saved = _db.Radiographs.Single();
        saved.PatientId.Should().Be(PatientAId);
        saved.UploadedBy.Should().Be(DoctorUserId,
            "UploadedBy must reflect the acting doctor user");
    }

    [Fact]
    public async Task Admin_AddingRadiographForAnyPatient_BypassesAccessCheck()
    {
        // Admin is not a "doctor" role, so DenyIfDoctorCannotAccess
        // short-circuits (returns null) and the radiograph is persisted
        // regardless of which patient it targets.
        _patientAccess.SetupGet(x => x.IsDoctor).Returns(false);
        // CanAccessPatientAsync must NOT be called for admin — if it is,
        // the mock returns false by default and the test would fail loudly.
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException(
                "admin must bypass CanAccessPatientAsync entirely"));

        // Switch the current user to admin for this test.
        _currentUser.SetupGet(x => x.UserId).Returns(AdminUserId);
        _currentUser.SetupGet(x => x.Role).Returns(UserRole.Admin);

        var req = NewRadiographRequest(PatientBId);

        var result = await _controller.AddRadiograph(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        _db.Radiographs.Should().ContainSingle(
            "the radiograph must be persisted for admin without per-patient check");
        _db.Radiographs.Single().PatientId.Should().Be(PatientBId);
    }

    private static AddRadiographRequest NewRadiographRequest(Guid patientId) =>
        new()
        {
            PatientId = patientId,
            FileUrl = "/uploads/test-xray.jpg",
            XrayType = "OPG",
            FileName = "test-xray.jpg",
            FileSize = 1024,
            MimeType = "image/jpeg",
        };
}
