using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Surgery;

/// <summary>
/// Unit tests for <see cref="SurgeryController.Create"/> (POST /api/surgery-cases) and its
/// <see cref="CreateSurgeryCaseRequestValidator"/>.
///
/// IMPORTANT — InMemory + PostgreSQL advisory-lock limitation:
///   The controller's Create action calls <c>db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", ...)</c>
///   to serialize case-number generation under concurrency. EF Core's InMemory provider is
///   non-relational and throws <see cref="InvalidOperationException"/> when a relational-only
///   extension method is invoked. Therefore the happy-path "Create → 201 Created" can NOT be
///   exercised under EF InMemory; it is covered instead by the integration tests in
///   <c>AqlanDentalPro.IntegrationTests.SurgeryStatusTransitionTests</c> (PostgreSQL + Testcontainers).
///
///   What we CAN test under InMemory:
///     - The CLIN-01 per-patient access check runs BEFORE the SQL is invoked (so a doctor
///       without access gets a 403 without ever touching the advisory lock).
///     - The audit log entry for a denied access attempt.
///     - The FluentValidation rules on <see cref="CreateSurgeryCaseRequestValidator"/> (Arabic messages).
///
///   What we CANNOT test under InMemory (documented as findings in the PR description):
///     - Persisted SurgeryCase.DoctorId == req.DoctorId (the audit's "DoctorId = Doctor.Id, not User.Id"
///       concern). The controller stores <c>req.DoctorId</c> as-is — there is no User→Doctor
///       resolution at all (unlike the prescription path the audit was hinting at). Since the
///       controller doesn't substitute User.Id for DoctorId, the audit's hinted bug does NOT
///       exist here; this is verified by reading the source. A full persistence test would
///       require Testcontainers (PostgreSQL).
///     - 404 when PatientId doesn't exist. The Create action does NOT validate that the patient
///       exists — it relies on the FK constraint in PostgreSQL to reject the insert. Under
///       InMemory there's no FK enforcement. SEE FINDING #1 in the PR description.
/// </summary>
public class SurgeryControllerCreateTests
{
    // ── Access-check tests (run BEFORE the SQL layer) ──────────────────────────────

    [Fact]
    public async Task Create_ByDoctorWithoutPatientAccess_Returns403_ArabicMessage()
    {
        // Arrange — doctor exists, patient exists, but the doctor is NOT linked to the patient.
        await using var db = SurgeryTestData.CreateDb();
        var patient = SurgeryTestData.BuildPatient();
        var (user, doctor) = SurgeryTestData.BuildDoctor();
        db.Patients.Add(patient);
        db.Users.Add(user);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock); // NO accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(user.Id);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);
        currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);

        var controller = SurgeryTestData.BuildController(db, accessMock, currentUserMock);

        var req = new CreateSurgeryCaseRequest
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            SurgeryType = "خلع ضرس"
        };

        // Act
        var result = await controller.Create(req);

        // Assert — 403 with the Arabic denial message
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);

        var messageProp = statusResult.Value!.GetType().GetProperty("message");
        messageProp.Should().NotBeNull("the 403 response must carry a 'message' field");
        messageProp!.GetValue(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        // No SurgeryCase should have been added
        (await db.SurgeryCases.CountAsync()).Should().Be(0,
            "the access check must short-circuit BEFORE any persistence attempt");
    }

    [Fact]
    public async Task Create_ByDoctorWithPatientAccess_AccessCheckPasses_AndReachesTransactionLayer()
    {
        // Arrange — doctor WITH access. Under EF InMemory the transaction layer throws
        // (InMemory does not support BeginTransactionAsync), which proves the access check
        // PASSED (otherwise we'd have gotten a 403 ObjectResult before reaching the tx).
        await using var db = SurgeryTestData.CreateDb();
        var patient = SurgeryTestData.BuildPatient();
        var (user, doctor) = SurgeryTestData.BuildDoctor();
        db.Patients.Add(patient);
        db.Users.Add(user);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock, patient.Id); // patient IS accessible
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(user.Id);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var controller = SurgeryTestData.BuildController(db, accessMock, currentUserMock);

        var req = new CreateSurgeryCaseRequest
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            SurgeryType = "خلع ضرس",
            TeethInvolved = "36"
        };

        // Act — under EF InMemory, BeginTransactionAsync throws (TransactionIgnoredWarning).
        // Reaching this exception proves the access check returned null (pass) and the
        // controller proceeded into the retry/transaction layer.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Create(req));

        // Assert — access check ran and PASSED (so no denial audit entry was logged)
        accessMock.Verify(p => p.CanAccessPatientAsync(patient.Id), Times.Once,
            "the access check must run before the SQL layer for an authorized doctor");

        // The exception comes from EF InMemory refusing to open a transaction.
        ex.Message.Should().Contain("in-memory",
            "EF InMemory must throw when BeginTransactionAsync is invoked — " +
            "this guards that the access check ran BEFORE the SQL/advisory-lock layer");
    }

    [Fact]
    public async Task Create_ByNonDoctorRole_AccessCheckBypassed_CanAccessPatientAsyncNeverCalled()
    {
        // Arrange — Admin role. PatientAccessService.IsDoctor returns false → DenyIfDoctorCannotAccess
        // short-circuits and returns null without ever calling CanAccessPatientAsync.
        await using var db = SurgeryTestData.CreateDb();
        var patient = SurgeryTestData.BuildPatient();
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupNonDoctor(accessMock); // IsDoctor = false
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);

        var controller = SurgeryTestData.BuildController(db, accessMock, currentUserMock);

        var req = new CreateSurgeryCaseRequest
        {
            PatientId = patient.Id,
            SurgeryType = "خلع ضرس"
        };

        // Act — bypasses access check (non-doctor short-circuit), then InMemory throws
        // on BeginTransactionAsync (TransactionIgnoredWarning).
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Create(req));

        // Assert — the per-patient check was NEVER invoked
        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never,
            "non-doctor roles must short-circuit the access check (per DenyIfDoctorCannotAccess)");
    }

    [Fact]
    public async Task Create_ByDoctorWithoutAccess_LogsDenialAuditEntry()
    {
        // Arrange
        await using var db = SurgeryTestData.CreateDb();
        var patient = SurgeryTestData.BuildPatient();
        var (user, doctor) = SurgeryTestData.BuildDoctor();
        db.Patients.Add(patient);
        db.Users.Add(user);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var accessMock = new Mock<IPatientAccessService>();
        SurgeryTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(user.Id);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.OralSurgeon);

        var auditMock = new Mock<IAuditService>();
        var controller = SurgeryTestData.BuildController(db, accessMock, currentUserMock, auditMock);

        var req = new CreateSurgeryCaseRequest
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            SurgeryType = "خلع ضرس"
        };

        // Act
        await controller.Create(req);

        // Assert — the denial is audited with status="denied" + resource="SurgeryCase"
        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                patient.Id,
                It.IsAny<object?>(),
                It.Is<object?>(o => o != null && AuditDataHasDenialMarker(o)),
                It.IsAny<string?>()),
            Times.Once,
            "an access denial on Create must be recorded in the audit log");
    }

    // Verify by reflection that the audit's newData payload carries the expected fields.
    private static bool AuditDataHasDenialMarker(object data)
    {
        var type = data.GetType();
        var statusProp = type.GetProperty("status");
        var resourceProp = type.GetProperty("resource");
        if (statusProp?.GetValue(data)?.ToString() != "denied") return false;
        if (resourceProp?.GetValue(data)?.ToString() != "SurgeryCase") return false;
        return true;
    }

    // ── FluentValidation tests for CreateSurgeryCaseRequestValidator ──────────────
    // These exercise the validation rules in isolation (no controller, no SQL).
    // In production, AddFluentValidationAutoValidation wires these rules into the MVC
    // pipeline so a 400 with the Arabic message is returned before the action runs.

    [Fact]
    public void Validator_MissingPatientId_ReturnsArabicError()
    {
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.Empty,
            SurgeryType = "خلع ضرس"
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.PatientId));
        result.Errors.First(e => e.PropertyName == nameof(req.PatientId))
            .ErrorMessage.Should().Be("المريض مطلوب");
    }

    [Fact]
    public void Validator_MissingSurgeryType_ReturnsArabicError()
    {
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.NewGuid(),
            SurgeryType = ""
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.SurgeryType));
        result.Errors.First(e => e.PropertyName == nameof(req.SurgeryType))
            .ErrorMessage.Should().Be("نوع الجراحة مطلوب");
    }

    [Fact]
    public void Validator_SurgeryTypeTooLong_ReturnsArabicError()
    {
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.NewGuid(),
            SurgeryType = new string('x', 201)
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.SurgeryType));
        result.Errors.First(e => e.PropertyName == nameof(req.SurgeryType))
            .ErrorMessage.Should().Be("نوع الجراحة يجب ألا يتجاوز 200 حرف");
    }

    [Theory]
    [InlineData("15/06/2026")]   // dd/MM/yyyy — not DateOnly.Parse compatible in invariant culture
    [InlineData("not-a-date")]
    [InlineData("2026-13-45")]   // invalid month/day
    public void Validator_InvalidScheduledDateFormat_ReturnsArabicError(string badDate)
    {
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.NewGuid(),
            SurgeryType = "خلع ضرس",
            ScheduledDate = badDate
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.ScheduledDate));
        result.Errors.First(e => e.PropertyName == nameof(req.ScheduledDate))
            .ErrorMessage.Should().Be("تنسيق تاريخ الجراحة غير صالح");
    }

    [Fact]
    public void Validator_ValidPayload_Passes()
    {
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.NewGuid(),
            SurgeryType = "خلع ضرس عقل",
            TeethInvolved = "18,28,38,48",
            ScheduledDate = "2026-06-15"
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeTrue("a complete, well-formed payload must pass validation");
    }

    [Fact]
    public void Validator_OmitsScheduledDate_Passes_WhenFieldIsNull()
    {
        // ScheduledDate is optional — only validated when present.
        var validator = new CreateSurgeryCaseRequestValidator();
        var req = new CreateSurgeryCaseRequest
        {
            PatientId = Guid.NewGuid(),
            SurgeryType = "خلع ضرس",
            ScheduledDate = null
        };

        var result = validator.Validate(req);

        result.IsValid.Should().BeTrue("ScheduledDate is optional and should not be validated when null");
    }
}
