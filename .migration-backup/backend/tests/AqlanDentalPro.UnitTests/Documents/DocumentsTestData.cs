using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AqlanDentalPro.UnitTests.Documents;

/// <summary>
/// Shared seed + builder helpers for <see cref="DocumentsController"/> unit tests.
/// Mirrors the TEST-02 (Surgery) / TEST-12 (Visits) pattern: EF InMemory + mocked
/// <see cref="ICurrentUserService"/> + <see cref="IPatientAccessService"/> +
/// <see cref="IAuditService"/>. All dates are fixed (no DateTime.Now) so tests stay
/// deterministic.
/// </summary>
internal static class DocumentsTestData
{
    /// <summary>
    /// Fixed UTC timestamp used everywhere a "now" value is needed in tests.
    /// 2026-06-15T10:30:00Z — matches the TEST-02/TEST-12 fixtures to keep test
    /// date-format assertions consistent across the audit.
    /// </summary>
    public static readonly DateTime FixedNowUtc = new(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    public static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"documents-tests-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// Builds a fully-valid <see cref="Document"/> (active, unsigned) linked to a Patient.
    /// Caller controls the IDs by passing pre-seeded entities.
    /// </summary>
    public static Document BuildDocument(
        Guid patientId,
        string title = "موافقة على علاج التقويم",
        string? documentType = "consent",
        string? fileUrl = "/uploads/abc.pdf",
        string? fileName = "consent.pdf",
        long? fileSize = 1024,
        string? mimeType = "application/pdf",
        Guid? uploadedBy = null,
        Guid? orthoCaseId = null)
        => new()
        {
            PatientId = patientId,
            Title = title,
            DocumentType = documentType,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            Notes = "ملاحظات المستند",
            UploadedBy = uploadedBy,
            OrthoCaseId = orthoCaseId,
            Signed = false,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    public static Patient BuildPatient(string firstName = "سالم", string lastName = "المريض")
        => new()
        {
            PatientNumber = $"P-{Guid.NewGuid():N}".Substring(0, 12),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

    /// <summary>
    /// Builds an OrthoCase linked to a patient — used for the "case must belong to the same
    /// patient" guard in CreateDocument.
    /// </summary>
    public static OrthoCase BuildOrthoCase(Guid patientId, Guid? doctorId = null)
        => new()
        {
            CaseNumber = $"OC-{Guid.NewGuid():N}".Substring(0, 16),
            PatientId = patientId,
            DoctorId = doctorId,
            Status = OrthoCaseStatus.Active,
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc
        };

    /// <summary>
    /// Constructs the <see cref="DocumentsController"/> with the standard mocked dependencies.
    /// PatientAccess mock is configured by the caller — the controller's
    /// <c>DenyIfDoctorCannotAccess</c> depends on <c>IPatientAccessService.IsDoctor</c> +
    /// <c>CanAccessPatientAsync</c>.
    /// </summary>
    public static DocumentsController BuildController(
        AppDbContext db,
        Mock<IPatientAccessService>? patientAccessMock = null,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IAuditService>? auditMock = null)
    {
        patientAccessMock ??= new Mock<IPatientAccessService>();
        currentUserMock ??= new Mock<ICurrentUserService>();
        auditMock ??= new Mock<IAuditService>();

        return new DocumentsController(
            db,
            currentUserMock.Object,
            patientAccessMock.Object,
            auditMock.Object);
    }

    /// <summary>
    /// Configures the patient-access mock as a non-doctor role (Admin/Reception) so
    /// <c>DenyIfDoctorCannotAccess</c> short-circuits and lets the action proceed.
    /// </summary>
    public static void SetupNonDoctor(Mock<IPatientAccessService> mock)
    {
        mock.SetupGet(p => p.IsDoctor).Returns(false);
        mock.SetupGet(p => p.HasFullAccess).Returns(true);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Configures the patient-access mock as a doctor role that is allowed to access
    /// only the given patient ids. Any other patient returns 403 from the controller.
    /// </summary>
    public static void SetupDoctorWithAccess(
        Mock<IPatientAccessService> mock,
        params Guid[] accessiblePatientIds)
    {
        var set = new HashSet<Guid>(accessiblePatientIds);
        mock.SetupGet(p => p.IsDoctor).Returns(true);
        mock.SetupGet(p => p.HasFullAccess).Returns(false);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid pid) => set.Contains(pid));
    }
}
