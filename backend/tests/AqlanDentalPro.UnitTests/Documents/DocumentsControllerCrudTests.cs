using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Documents;

/// <summary>
/// Unit tests for <see cref="DocumentsController"/> CRUD happy-paths.
///
/// Coverage map:
///   - CreateDocument: 200 + persists with UploadedBy = currentUser.UserId (CLIN-01 + audit-trail)
///   - CreateDocument: 400 Arabic for empty PatientId / unknown Patient / missing Title /
///     OrthoCase belonging to a different patient (PR #357 cross-patient guard)
///   - GetDocument: 200 + projection shape (date formatting, signed null)
///   - GetDocuments: list filtering (patientId, documentType, orthoCaseId) + pagination
///     + pageSize clamping (1..100)
///   - UpdateDocument: 200 + PATCH-like null-skip + Signed transition sets SignedAt
///   - DeleteDocument: 200 + soft-delete sets IsActive=false, DeletedBy=currentUser.UserId
///
/// SEC-DOCS note: DocumentsController.CreateDocument / UpdateDocument / DeleteDocument now
/// explicitly call <c>DenyIfDoctorCannotAccess</c> (body-bound patientId for Create; resolved
/// from the fetched doc for Update/Delete — the class-level PatientAccessFilter only inspects
/// route + query for "patientId" so it cannot enforce these on its own). The cross-patient
/// denial paths are covered in <c>DocumentsControllerAccessTests</c>. These CRUD happy-path
/// tests run as Admin (IsDoctor == false) so the access check short-circuits.
/// </summary>
public class DocumentsControllerCrudTests : IDisposable
{
    private readonly AppDbContext _db;

    public DocumentsControllerCrudTests()
    {
        _db = DocumentsTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── CreateDocument (POST /api/documents) ─────────────────────────────────────

    [Fact]
    public async Task CreateDocument_ValidPayload_AsAdmin_Returns200AndPersists_WithUploadedBy()
    {
        var seeded = await SeedPatientAsync();
        var adminId = Guid.NewGuid();
        var controller = BuildControllerAsAdmin(adminId);

        var req = new CreateDocumentRequest
        {
            PatientId = seeded,
            Title = "موافقة على علاج التقويم",
            DocumentType = "consent",
            FileUrl = "/uploads/abc.pdf",
            FileName = "consent.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            Notes = "ملاحظات"
        };

        var result = await controller.CreateDocument(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var newId = (Guid)ok.Value!.GetType().GetProperty("Id")!.GetValue(ok.Value)!;
        newId.Should().NotBeEmpty();
        var messageProp = ok.Value.GetType().GetProperty("message");
        messageProp!.GetValue(ok.Value).Should().Be("تم إضافة المستند بنجاح");

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.SingleAsync(d => d.Id == newId);
        stored.PatientId.Should().Be(seeded);
        stored.Title.Should().Be("موافقة على علاج التقويم");
        stored.DocumentType.Should().Be("consent");
        stored.UploadedBy.Should().Be(adminId,
            "UploadedBy must be captured from ICurrentUserService for the audit trail (CLIN-23 ownership)");
        stored.Signed.Should().BeFalse("a new document is unsigned by default");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDocument_EmptyPatientId_Returns400_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var req = new CreateDocumentRequest
        {
            PatientId = Guid.Empty,
            Title = "x"
        };

        var result = await controller.CreateDocument(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("معرّف المريض مطلوب");
    }

    [Fact]
    public async Task CreateDocument_UnknownPatient_Returns400_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var req = new CreateDocumentRequest
        {
            PatientId = Guid.NewGuid(), // not seeded
            Title = "x"
        };

        var result = await controller.CreateDocument(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("المريض غير موجود");
    }

    [Fact]
    public async Task CreateDocument_MissingTitle_Returns400_ArabicMessage()
    {
        var seeded = await SeedPatientAsync();
        var controller = BuildControllerAsAdmin();

        var req = new CreateDocumentRequest
        {
            PatientId = seeded,
            Title = "" // empty title
        };

        var result = await controller.CreateDocument(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("عنوان المستند مطلوب");
    }

    [Fact]
    public async Task CreateDocument_OrthoCaseBelongingToDifferentPatient_Returns400_ArabicMessage()
    {
        // PR #357 Codex fix: OrthoCaseId existence alone is not enough — must belong to the
        // SAME patient as the request. Cross-patient link attempts must be rejected.
        var patientA = DocumentsTestData.BuildPatient();
        var patientB = DocumentsTestData.BuildPatient("خالد", "العولقي");
        var orthoCaseForB = DocumentsTestData.BuildOrthoCase(patientB.Id);
        _db.Patients.AddRange(patientA, patientB);
        _db.OrthoCases.Add(orthoCaseForB);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateDocumentRequest
        {
            PatientId = patientA.Id,
            Title = "x",
            OrthoCaseId = orthoCaseForB.Id // belongs to B, not A
        };

        var result = await controller.CreateDocument(req);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("الحالة التقويمية غير موجودة أو لا تخص هذا المريض");

        (await _db.Documents.CountAsync()).Should().Be(0,
            "a rejected cross-patient link must not persist any document");
    }

    [Fact]
    public async Task CreateDocument_OrthoCaseBelongingToSamePatient_PersistsWithLink()
    {
        var patient = DocumentsTestData.BuildPatient();
        var orthoCase = DocumentsTestData.BuildOrthoCase(patient.Id);
        _db.Patients.Add(patient);
        _db.OrthoCases.Add(orthoCase);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var req = new CreateDocumentRequest
        {
            PatientId = patient.Id,
            Title = "سجل تصوير",
            OrthoCaseId = orthoCase.Id
        };

        var result = await controller.CreateDocument(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var newId = (Guid)ok.Value!.GetType().GetProperty("Id")!.GetValue(ok.Value)!;

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.SingleAsync(d => d.Id == newId);
        stored.OrthoCaseId.Should().Be(orthoCase.Id,
            "same-patient OrthoCase link must be persisted");
    }

    // ── GetDocument (GET /api/documents/{id}) ────────────────────────────────────

    [Fact]
    public async Task GetDocument_UnknownId_Returns404_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocument(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("المستند غير موجود");
    }

    [Fact]
    public async Task GetDocument_ExistingId_AsAdmin_Returns200WithProjection()
    {
        var seeded = await SeedDocumentAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocument(seeded.DocumentId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((Guid)payload.Id).Should().Be(seeded.DocumentId);
        ((string?)payload.Title).Should().Be("موافقة على علاج التقويم");
        ((string?)payload.DocumentType).Should().Be("consent");
        ((string?)payload.CreatedAt).Should().Match(s => ((string)s).Length == 10,
            "CreatedAt is projected as yyyy-MM-dd (date-only, 10 chars)");
        ((object?)payload.SignedAt).Should().BeNull(
            "an unsigned document's SignedAt projection must be null");
    }

    // ── GetDocuments (GET /api/documents) — list/filter/pagination ───────────────

    [Fact]
    public async Task GetDocuments_ByPatientIdFilter_ReturnsOnlyMatching()
    {
        var patientA = DocumentsTestData.BuildPatient();
        var patientB = DocumentsTestData.BuildPatient("خالد", "العولقي");
        _db.Patients.AddRange(patientA, patientB);
        _db.Documents.AddRange(
            DocumentsTestData.BuildDocument(patientA.Id),
            DocumentsTestData.BuildDocument(patientA.Id, title: "doc 2"),
            DocumentsTestData.BuildDocument(patientB.Id, title: "doc for B"));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocuments(patientId: patientA.Id, documentType: null, orthoCaseId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(2, "only patient A's documents match the filter");
    }

    [Fact]
    public async Task GetDocuments_PageSizeClampedTo100()
    {
        var patient = DocumentsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocuments(patientId: patient.Id, documentType: null, orthoCaseId: null, pageSize: 500);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(100,
            "pageSize must be clamped to a max of 100 (same guard as VisitsController)");
    }

    [Fact]
    public async Task GetDocuments_PageSizeBelow1_ClampedTo1()
    {
        var patient = DocumentsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocuments(patientId: patient.Id, documentType: null, orthoCaseId: null, pageSize: 0);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.pageSize).Should().Be(1, "pageSize < 1 must be clamped up to 1");
    }

    [Fact]
    public async Task GetDocuments_ByDocumentTypeFilter_ReturnsOnlyMatching()
    {
        var patient = DocumentsTestData.BuildPatient();
        _db.Patients.Add(patient);
        _db.Documents.AddRange(
            DocumentsTestData.BuildDocument(patient.Id, documentType: "consent"),
            DocumentsTestData.BuildDocument(patient.Id, title: "x-ray", documentType: "xray"));
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.GetDocuments(patientId: patient.Id, documentType: "xray", orthoCaseId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.total).Should().Be(1, "documentType filter must narrow results");
    }

    // ── UpdateDocument (PUT /api/documents/{id}) ─────────────────────────────────

    [Fact]
    public async Task UpdateDocument_UnknownId_Returns404_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateDocument(Guid.NewGuid(), new UpdateDocumentRequest { Title = "x" });

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("المستند غير موجود");
    }

    [Fact]
    public async Task UpdateDocument_OnSoftDeletedDoc_Returns400_ArabicMessage()
    {
        var seeded = await SeedDocumentAsync();
        // Mark as already soft-deleted
        var doc = await _db.Documents.FindAsync(seeded.DocumentId);
        doc!.IsActive = false;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateDocument(seeded.DocumentId, new UpdateDocumentRequest { Title = "x" });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("لا يمكن تعديل مستند محذوف");
    }

    [Fact]
    public async Task UpdateDocument_NullFieldsAreSkipped_PatchLikeSemantics()
    {
        var seeded = await SeedDocumentAsync();

        var controller = BuildControllerAsAdmin();

        // Pass null for Title/DocumentType/Notes — controller's `if (req.X != null)` skips them.
        var result = await controller.UpdateDocument(
            seeded.DocumentId,
            new UpdateDocumentRequest { Title = null, DocumentType = null, Notes = null, Signed = null });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.FindAsync(seeded.DocumentId);
        stored!.Title.Should().Be("موافقة على علاج التقويم",
            "null fields must NOT clear existing values (PATCH-like semantics, same as VisitsController)");
    }

    [Fact]
    public async Task UpdateDocument_SignedTrue_OnUnsignedDoc_SetsSignedAndSignedAt()
    {
        var seeded = await SeedDocumentAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.UpdateDocument(
            seeded.DocumentId,
            new UpdateDocumentRequest { Signed = true });

        result.Should().BeOfType<OkObjectResult>();
        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.FindAsync(seeded.DocumentId);
        stored!.Signed.Should().BeTrue();
        stored.SignedAt.Should().NotBeNull(
            "signing must stamp SignedAt — the audit-trail timestamp for the signature");
    }

    [Fact]
    public async Task UpdateDocument_SignedTrue_OnAlreadySignedDoc_DoesNotResetSignedAt()
    {
        var seeded = await SeedDocumentAsync();
        var originalSignedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var doc = await _db.Documents.FindAsync(seeded.DocumentId);
        doc!.Signed = true;
        doc.SignedAt = originalSignedAt;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        await controller.UpdateDocument(seeded.DocumentId, new UpdateDocumentRequest { Signed = true });

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.FindAsync(seeded.DocumentId);
        stored!.SignedAt.Should().Be(originalSignedAt,
            "re-signing an already-signed doc must NOT reset the original SignedAt timestamp (idempotent)");
    }

    // ── DeleteDocument (DELETE /api/documents/{id}) ──────────────────────────────

    [Fact]
    public async Task DeleteDocument_UnknownId_Returns404_ArabicMessage()
    {
        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteDocument(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("المستند غير موجود");
    }

    [Fact]
    public async Task DeleteDocument_OnSoftDeletedDoc_Returns400_ArabicMessage()
    {
        var seeded = await SeedDocumentAsync();
        var doc = await _db.Documents.FindAsync(seeded.DocumentId);
        doc!.IsActive = false;
        await _db.SaveChangesAsync();

        var controller = BuildControllerAsAdmin();

        var result = await controller.DeleteDocument(seeded.DocumentId);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("المستند محذوف بالفعل");
    }

    [Fact]
    public async Task DeleteDocument_Valid_SoftDeletes_SetsDeletedBy_ArabicSuccessMessage()
    {
        var seeded = await SeedDocumentAsync();
        var adminId = Guid.NewGuid();
        var controller = BuildControllerAsAdmin(adminId);

        var result = await controller.DeleteDocument(seeded.DocumentId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم حذف المستند بنجاح");

        _db.ChangeTracker.Clear();
        var stored = await _db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == seeded.DocumentId);
        stored!.IsActive.Should().BeFalse("soft-delete sets IsActive = false");
        stored.DeletedBy.Should().Be(adminId,
            "DeletedBy must be captured from ICurrentUserService for the audit trail (matches DeleteVisit pattern)");
        stored.DeletedAt.Should().NotBeNull();
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

    private async Task<Guid> SeedPatientAsync()
    {
        var patient = DocumentsTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        return patient.Id;
    }

    private DocumentsController BuildControllerAsAdmin(Guid? userId = null)
    {
        var accessMock = new Mock<IPatientAccessService>();
        DocumentsTestData.SetupNonDoctor(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.UserId).Returns(userId ?? Guid.NewGuid());
        return DocumentsTestData.BuildController(_db, accessMock, currentUserMock);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
