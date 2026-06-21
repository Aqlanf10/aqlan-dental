using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Documents;

/// <summary>
/// Access-control + existence tests for <see cref="DocumentsController"/>.
///
/// Coverage map:
///   - GetDocument: 403 cross-patient doctor + audit LogAsync call on denial (CLIN-01 +
///     audit-trail enforcement), admin bypass, same-patient doctor 200.
///   - GetDocument: audit LogAsync IS called with action=View on a 403 (the
///     DenyIfDoctorCannotAccess helper logs the denial — critical for the security audit trail).
///
/// FINDING #1 (TEST-13, documented in PR description, NOT fixed per test-only scope):
///   DocumentsController.CreateDocument, UpdateDocument, and DeleteDocument do NOT
///   explicitly call <c>DenyIfDoctorCannotAccess</c>. They rely solely on the
///   <c>[ServiceFilter(typeof(PatientAccessFilter))]</c> attribute — but the filter only
///   inspects route + query values for <c>patientId</c>. For CreateDocument the patientId
///   is in the request BODY (not route/query), so the filter does NOT enforce per-patient
///   access for CreateDocument. A doctor (with PatientAccessService.CanAccessPatientAsync
///   returning false) can still create a document under any patient. GetDocument is the
///   ONLY action that explicitly calls DenyIfDoctorCannotAccess and is therefore the only
///   one safe against cross-patient access by doctor roles when invoked outside the
///   routing pipeline.
///
///   The test <c>CreateDocument_ByDoctorWithoutPatientAccess_SucceedsBecauseNoExplicitCheck</c>
///   documents this finding by asserting that the access mock is NEVER called (because
///   the controller never invokes it) and the document IS persisted.
/// </summary>
public class DocumentsControllerAccessTests : IDisposable
{
    private readonly AppDbContext _db;

    public DocumentsControllerAccessTests()
    {
        _db = DocumentsTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── GetDocument (GET /api/documents/{id}) — CLIN-01 access ───────────────────

    [Fact]
    public async Task GetDocument_CrossPatientDoctor_Returns403_ArabicMessage()
    {
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);
        var auditMock = new Mock<IAuditService>();

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.GetDocument(seeded.DocumentId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");
    }

    [Fact]
    public async Task GetDocument_CrossPatientDoctor_LogsDeniedAuditEntry()
    {
        // The audit trail is the security-critical piece: a 403 denial MUST be recorded so
        // that attempted cross-patient reads by doctors are visible to the admin. The
        // DenyIfDoctorCannotAccess helper calls IAuditService.LogAsync with action=View
        // and a status=denied payload.
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        var doctorUserId = Guid.NewGuid();
        currentUserMock.SetupGet(c => c.UserId).Returns(doctorUserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);
        var auditMock = new Mock<IAuditService>();

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        await controller.GetDocument(seeded.DocumentId);

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial must produce exactly one audit log entry with action=View + the denied patientId");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "the access check must run before the audit log entry is written");
    }

    [Fact]
    public async Task GetDocument_SamePatientDoctor_Returns200()
    {
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetDocument(seeded.DocumentId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDocument_AdminRole_BypassesAccessCheck()
    {
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.GetDocument(seeded.DocumentId);

        result.Should().BeOfType<OkObjectResult>();
        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never,
            "admin roles must short-circuit the access check (IsDoctor == false)");
    }

    // ── CreateDocument access (FINDING #1 — missing access check) ────────────────

    [Fact]
    public async Task CreateDocument_ByDoctorWithoutPatientAccess_SucceedsBecauseNoExplicitCheck_Finding()
    {
        // FINDING #1: CreateDocument does NOT call DenyIfDoctorCannotAccess. It relies on
        // the PatientAccessFilter — but patientId is in the request body, not route/query,
        // so the filter (which only checks route+query) does NOT enforce per-patient access.
        //
        // In a real HTTP pipeline, the filter would skip the check (patientId not in route/query).
        // In unit tests (no filter), the controller itself never calls CanAccessPatientAsync.
        //
        // Result: a doctor mock with NO access to the patient can still create a document.
        // This is a security finding documented in the PR description.
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock);

        var req = new CreateDocumentRequest
        {
            PatientId = seeded.PatientId,
            Title = "محاولة وصول متقاطع"
        };

        var result = await controller.CreateDocument(req);

        // The document IS persisted despite the doctor not having access — this is the bug.
        result.Should().BeOfType<OkObjectResult>(
            "FINDING #1: CreateDocument does not call DenyIfDoctorCannotAccess — the cross-patient " +
            "doctor succeeds. The PatientAccessFilter does not cover body-bound patientId.");
        (await _db.Documents.CountAsync(d => d.PatientId == seeded.PatientId)).Should().Be(2,
            "the seeded document + the new one (created despite no access) = 2");

        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never,
            "CreateDocument never calls CanAccessPatientAsync — the only access guard is the " +
            "PatientAccessFilter, which cannot see body-bound patientId.");
    }

    [Fact]
    public async Task UpdateDocument_ByDoctorWithoutPatientAccess_SucceedsBecauseNoExplicitCheck_Finding()
    {
        // FINDING #1 (Update variant): same root cause — UpdateDocument does not call
        // DenyIfDoctorCannotAccess. The patientId is loaded from the DB inside the action
        // but no access check is performed against it.
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.UpdateDocument(
            seeded.DocumentId,
            new UpdateDocumentRequest { Title = "محاولة تعديل غير مصرح بها" });

        result.Should().BeOfType<OkObjectResult>(
            "FINDING #1 (Update): UpdateDocument does not call DenyIfDoctorCannotAccess — " +
            "the cross-patient doctor can mutate the document.");
        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDocument_ByDoctorWithoutPatientAccess_SucceedsBecauseNoExplicitCheck_Finding()
    {
        // FINDING #1 (Delete variant): same root cause — DeleteDocument does not call
        // DenyIfDoctorCannotAccess.
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock);

        var result = await controller.DeleteDocument(seeded.DocumentId);

        result.Should().BeOfType<OkObjectResult>(
            "FINDING #1 (Delete): DeleteDocument does not call DenyIfDoctorCannotAccess — " +
            "the cross-patient doctor can soft-delete the document.");
        accessMock.Verify(p => p.CanAccessPatientAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid PatientId, Guid DocumentId)> SeedDocumentAsync()
    {
        var patient = DocumentsTestData.BuildPatient();
        _db.Patients.Add(patient);
        var document = DocumentsTestData.BuildDocument(patient.Id);
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();
        return (patient.Id, document.Id);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
