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
/// SEC-DOCS (fix for TEST-13 Finding #1):
///   DocumentsController.CreateDocument, UpdateDocument, and DeleteDocument previously
///   relied solely on the class-level <c>[ServiceFilter(typeof(PatientAccessFilter))]</c>
///   attribute — but that filter only inspects route + query values for <c>patientId</c>.
///   For CreateDocument the patientId is in the request BODY, and for Update/Delete the
///   route only carries the document id, so the filter never saw a patientId and a doctor
///   could create/update/delete documents for ANY patient. The fix added an explicit
///   <c>DenyIfDoctorCannotAccess</c> call at the top of each body-bound / route-only-id
///   action (mirroring PrescriptionsController). The three <c>..._Returns403_WhenNoAccess</c>
///   tests below pin the FIXED behavior: 403 + Arabic message + audit LogAsync called +
///   no mutation persisted.
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

    // ── CreateDocument access (SEC-DOCS — fixed: explicit DenyIfDoctorCannotAccess) ─

    [Fact]
    public async Task CreateDocument_ByDoctorWithoutPatientAccess_Returns403_WhenNoAccess()
    {
        // SEC-DOCS: CreateDocument now calls DenyIfDoctorCannotAccess(req.PatientId) at the
        // top of the action — the body-bound patientId is enforced even though the
        // class-level PatientAccessFilter cannot see it. A doctor with no access to the
        // patient gets 403 + Arabic message + audit log + NO document persisted.
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var doctorUserId = Guid.NewGuid();
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(doctorUserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);
        var auditMock = new Mock<IAuditService>();

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var req = new CreateDocumentRequest
        {
            PatientId = seeded.PatientId,
            Title = "محاولة وصول متقاطع"
        };

        var result = await controller.CreateDocument(req);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        // No document was persisted (the seeded one remains the only row).
        (await _db.Documents.CountAsync(d => d.PatientId == seeded.PatientId)).Should().Be(1,
            "a 403 denial must not persist the cross-patient document");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "CreateDocument must call CanAccessPatientAsync with the body-bound patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on CreateDocument must produce an audit log entry");
    }

    [Fact]
    public async Task UpdateDocument_ByDoctorWithoutPatientAccess_Returns403_WhenNoAccess()
    {
        // SEC-DOCS (Update variant): UpdateDocument now resolves PatientId from the fetched
        // document and calls DenyIfDoctorCannotAccess(doc.PatientId). A cross-patient doctor
        // gets 403 + Arabic message + audit log + NO mutation. The fetched doc is returned
        // unchanged from the DB.
        var seeded = await SeedDocumentAsync();
        var originalTitle = (await _db.Documents.FindAsync(seeded.DocumentId))!.Title;

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var doctorUserId = Guid.NewGuid();
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(doctorUserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);
        var auditMock = new Mock<IAuditService>();

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.UpdateDocument(
            seeded.DocumentId,
            new UpdateDocumentRequest { Title = "محاولة تعديل غير مصرح بها" });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "UpdateDocument must call CanAccessPatientAsync with the doc's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on UpdateDocument must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.FindAsync(seeded.DocumentId);
        stored!.Title.Should().Be(originalTitle,
            "a 403 denial must NOT mutate the document (no title update, no UpdatedAt refresh)");
    }

    [Fact]
    public async Task DeleteDocument_ByDoctorWithoutPatientAccess_Returns403_WhenNoAccess()
    {
        // SEC-DOCS (Delete variant): DeleteDocument now resolves PatientId from the fetched
        // document and calls DenyIfDoctorCannotAccess(doc.PatientId). A cross-patient doctor
        // gets 403 + Arabic message + audit log + NO soft-delete (IsActive stays true).
        var seeded = await SeedDocumentAsync();

        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var doctorUserId = Guid.NewGuid();
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(doctorUserId);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.GeneralDentist);
        var auditMock = new Mock<IAuditService>();

        var controller = DocumentsTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.DeleteDocument(seeded.DocumentId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "DeleteDocument must call CanAccessPatientAsync with the doc's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on DeleteDocument must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == seeded.DocumentId);
        stored!.IsActive.Should().BeTrue(
            "a 403 denial must NOT soft-delete the document (IsActive stays true, no DeletedBy/DeletedAt)");
        stored.DeletedBy.Should().BeNull();
        stored.DeletedAt.Should().BeNull();
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
