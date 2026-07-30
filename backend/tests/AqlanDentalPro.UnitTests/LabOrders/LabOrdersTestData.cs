using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// Shared seed + builder helpers for <see cref="LabOrdersController"/> access-control unit tests.
/// Mirrors the TEST-02 (Surgery) / TEST-13 (Documents) pattern: EF InMemory + mocked
/// <see cref="ICurrentUserService"/> + <see cref="IPatientAccessService"/> + <see cref="IAuditService"/>.
/// All dates are fixed (no DateTime.Now) so tests stay deterministic.
/// </summary>
internal static class LabOrdersTestData
{
    /// <summary>
    /// Fixed UTC timestamp used everywhere a "now" value is needed in tests.
    /// 2026-06-15T10:30:00Z — matches the TEST-02/TEST-13 fixtures to keep test
    /// date-format assertions consistent across the audit.
    /// </summary>
    public static readonly DateTime FixedNowUtc = new(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    public static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"lab-orders-tests-{Guid.NewGuid()}")
            .Options);

    public static Patient BuildPatient(string firstName = "سالم", string lastName = "المريض")
        => new()
        {
            PatientNumber = $"P-{Guid.NewGuid():N}".Substring(0, 12),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

    /// <summary>
    /// Builds a fully-valid <see cref="LabOrder"/> linked to a Patient. Caller controls the status
    /// (default "draft" so the Update-editable-status check passes) and appliance type.
    /// </summary>
    public static LabOrder BuildLabOrder(
        Guid patientId,
        string status = "draft",
        string applianceType = "تقويم معدني",
        Guid? doctorId = null,
        Guid? orthoCaseId = null)
        => new()
        {
            PatientId = patientId,
            OrthoCaseId = orthoCaseId,
            OrderNumber = $"LO-{Guid.NewGuid():N}".Substring(0, 14),
            ApplianceType = applianceType,
            Status = status,
            Priority = "normal",
            IsActive = true,
            CreatedAt = FixedNowUtc,
            UpdatedAt = FixedNowUtc,
            DoctorId = doctorId
        };

    /// <summary>
    /// Constructs the <see cref="LabOrdersController"/> with the standard mocked dependencies.
    /// <see cref="LabOrdersController"/> requires <see cref="IServiceScopeFactory"/> (used for
    /// fire-and-forget notification tasks); a Mock is provided — the 403 path under test never
    /// reaches the notification code.
    /// </summary>
    public static LabOrdersController BuildController(
        AppDbContext db,
        Mock<IPatientAccessService>? patientAccessMock = null,
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IAuditService>? auditMock = null)
    {
        patientAccessMock ??= new Mock<IPatientAccessService>();
        currentUserMock ??= new Mock<ICurrentUserService>();
        auditMock ??= new Mock<IAuditService>();

        // IServiceScopeFactory — never invoked on the 403 path, but required by the constructor.
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<LabOrdersController>>().Object;

        // Sprint 12 — LabOrderQueryService is now a constructor parameter. Build a
        // real instance against the same in-memory db + a query-service logger.
        // The 403 path under test never reaches the query service, but the param
        // is required by the constructor signature.
        var queryService = new LabOrderQueryService(db, new Mock<ILogger<LabOrderQueryService>>().Object);

        // CORE-LAB-001 — supplier-bill/payable/journal linkage is now a constructor
        // dependency. Built against the same in-memory db with no journal service, which
        // is the "journal disabled" configuration the service already tolerates.
        var financeSync = new LabOrderFinanceSyncService(db, journalEntryService: null);

        // CORE-FIN-LAB-ADJ — real instance against the same in-memory db, not a mock: the
        // commission resync now runs inside the same transaction as every lab-order write, so a
        // stub here would hide a regression in exactly the path these tests exercise.
        var commissionAdjustments = new CommissionAdjustmentService(
            db, new Mock<ILogger<CommissionAdjustmentService>>().Object);

        return new LabOrdersController(
            db,
            currentUserMock.Object,
            scopeFactoryMock.Object,
            logger,
            patientAccessMock.Object,
            auditMock.Object,
            queryService,
            financeSync,
            commissionAdjustments);
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
