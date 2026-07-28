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

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// SEC-ROUTE access-control tests for <see cref="LabOrdersController"/> route-id-bound mutations.
///
/// Coverage map:
///   - Update_ByDoctorWithoutAccess_Returns403:
///       PUT /api/lab-orders/{id} — fetches by {id} route, never checked the caller owns the patient.
///       SEC-ROUTE fix: explicit DenyIfDoctorCannotAccess(order.PatientId) after fetch, before
///       mutation. 403 + Arabic message + audit LogAsync + no mutation persisted.
///   - MarkReceived_ByDoctorWithoutAccess_Returns403:
///       POST /api/lab-orders/{id}/mark-received — same pattern.
///   - UpdateStatus_ByDoctorWithoutAccess_Returns403:
///       PUT /api/lab-orders/{id}/status — same pattern.
///   - Return_ByDoctorWithoutAccess_Returns403:
///       POST /api/lab-orders/{id}/return — same pattern.
///   - Remake_ByDoctorWithoutAccess_Returns403:
///       POST /api/lab-orders/{id}/remake — same pattern.
///
/// Mirrors TEST-02/TEST-13 access-test patterns (DocumentsControllerAccessTests,
/// SurgeryControllerAccessTests). Found by SEC-DOCS (PR #506) out-of-scope audit.
/// </summary>
public class LabOrdersControllerRouteAccessTests : IDisposable
{
    private readonly AppDbContext _db;

    public LabOrdersControllerRouteAccessTests()
    {
        _db = LabOrdersTestData.CreateDb();
    }

    public void Dispose() => _db.Dispose();

    // ── Update (PUT /api/lab-orders/{id}) — SEC-ROUTE access ─────────────────────

    [Fact]
    public async Task Update_ByDoctorWithoutAccess_Returns403()
    {
        // SEC-ROUTE: Update now resolves PatientId from the fetched order and calls
        // DenyIfDoctorCannotAccess(order.PatientId). A cross-patient doctor gets 403 +
        // Arabic message + audit log + NO mutation. The fetched order is returned
        // unchanged from the DB.
        var seeded = await SeedLabOrderAsync(status: "draft");
        var originalAppliance = (await _db.LabOrders.FindAsync(seeded.LabOrderId))!.ApplianceType;

        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin); // admin bypasses PermissionGuard
        var auditMock = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.Update(seeded.LabOrderId, new UpdateLabOrderRequest
        {
            ApplianceType = "محاولة تعديل غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "Update must call CanAccessPatientAsync with the order's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on Update must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.ApplianceType.Should().Be(originalAppliance,
            "a 403 denial must NOT mutate the order (no appliance update)");
    }

    // ── MarkReceived (POST /api/lab-orders/{id}/mark-received) — SEC-ROUTE access ─

    [Fact]
    public async Task MarkReceived_ByDoctorWithoutAccess_Returns403()
    {
        // SEC-ROUTE: MarkReceived now resolves PatientId from the fetched order and calls
        // DenyIfDoctorCannotAccess(order.PatientId). A cross-patient doctor gets 403 +
        // Arabic message + audit log + NO mutation. The fetched order status stays "ready".
        var seeded = await SeedLabOrderAsync(status: "ready");

        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var auditMock = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.MarkReceived(seeded.LabOrderId, null);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once,
            "MarkReceived must call CanAccessPatientAsync with the order's patientId");

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.View,
                "Patient",
                seeded.PatientId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "a 403 denial on MarkReceived must produce an audit log entry");

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.Status.Should().Be("ready",
            "a 403 denial must NOT mutate the order status (no transition to 'received')");
        stored.ReceivedDate.Should().BeNull("a 403 denial must NOT set ReceivedDate");
    }

    // ── UpdateStatus (PUT /api/lab-orders/{id}/status) — SEC-ROUTE access ────────

    [Fact]
    public async Task UpdateStatus_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedLabOrderAsync(status: "draft");

        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var auditMock = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.UpdateStatus(seeded.LabOrderId, new UpdateLabOrderStatusRequest
        {
            Status = "sent"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.Status.Should().Be("draft", "a 403 denial must NOT transition the status");
    }

    // ── Return (POST /api/lab-orders/{id}/return) — SEC-ROUTE access ─────────────

    [Fact]
    public async Task Return_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedLabOrderAsync(status: "ready");

        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var auditMock = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.Return(seeded.LabOrderId, new LabOrdersController.ReturnLabOrderRequest
        {
            Reason = "محاولة غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.Status.Should().Be("ready", "a 403 denial must NOT transition the status to 'returned'");
        stored.ReturnReason.Should().BeNull();
    }

    // ── Remake (POST /api/lab-orders/{id}/remake) — SEC-ROUTE access ─────────────

    [Fact]
    public async Task Remake_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedLabOrderAsync(status: "returned");

        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var auditMock = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, auditMock);

        var result = await controller.Remake(seeded.LabOrderId, new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "محاولة غير مصرح بها"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");

        accessMock.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.Status.Should().Be("returned", "a 403 denial must NOT transition the status to 'remake'");
        stored.RemakeCount.Should().Be(0, "a 403 denial must NOT increment RemakeCount");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────


    // ── CORE-LAB-006: history + attachments were NOT gated at all ────────────────
    // These routes carry only {id:guid}, so the class-level PatientAccessFilter (which
    // matches a route/query value literally named "patientId") never fires on them.
    // Unlike Update/UpdateStatus/Cancel they also had no explicit check, so a doctor
    // with no access to the patient could read another patient's lab history and
    // DOWNLOAD their attachments — clinical images and scans, i.e. PHI.

    private (Mock<IPatientAccessService> access, Mock<ICurrentUserService> user, Mock<IAuditService> audit)
        BuildDeniedDoctor()
    {
        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock); // no accessible patients
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin); // bypasses PermissionGuard only
        return (accessMock, currentUserMock, new Mock<IAuditService>());
    }

    [Fact]
    public async Task GetHistory_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedLabOrderAsync();
        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.GetHistory(seeded.LabOrderId);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        ExtractMessage(status.Value).Should().Be("غير مصرح لك بعرض بيانات هذا المريض");
        access.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);
    }

    [Fact]
    public async Task GetAttachments_ByDoctorWithoutAccess_Returns403()
    {
        var seeded = await SeedLabOrderAsync();
        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.GetAttachments(seeded.LabOrderId);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        access.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);
    }

    [Fact]
    public async Task DownloadAttachment_ByDoctorWithoutAccess_Returns403_AndNeverStreamsTheFile()
    {
        var seeded = await SeedLabOrderAsync();
        var attachment = new LabOrderAttachment
        {
            LabOrderId = seeded.LabOrderId,
            FileName = "scan.stl",
            StoragePath = "/does/not/exist/scan.stl",
            ContentType = "application/octet-stream",
            Category = "stl",
        };
        _db.LabOrderAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.DownloadAttachment(seeded.LabOrderId, attachment.Id);

        // Must be 403 — NOT a FileResult, and not the 404 the missing file would give.
        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        result.Should().NotBeOfType<FileStreamResult>();
    }

    [Fact]
    public async Task DeleteAttachment_ByDoctorWithoutAccess_Returns403_AndKeepsTheRow()
    {
        var seeded = await SeedLabOrderAsync();
        var attachment = new LabOrderAttachment
        {
            LabOrderId = seeded.LabOrderId,
            FileName = "photo.jpg",
            StoragePath = "/does/not/exist/photo.jpg",
            ContentType = "image/jpeg",
            Category = "photo",
        };
        _db.LabOrderAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.DeleteAttachment(seeded.LabOrderId, attachment.Id);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);

        _db.ChangeTracker.Clear();
        (await _db.LabOrderAttachments.CountAsync(a => a.Id == attachment.Id))
            .Should().Be(1, "a 403 denial must not delete the attachment");
    }

    [Fact]
    public async Task GetHistory_ByDoctorWithAccess_IsAllowed()
    {
        var seeded = await SeedLabOrderAsync();
        var accessMock = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupDoctorWithAccess(accessMock, seeded.PatientId);
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var controller = LabOrdersTestData.BuildController(_db, accessMock, currentUserMock, new Mock<IAuditService>());

        var result = await controller.GetHistory(seeded.LabOrderId);

        // The guard must not block the doctor who DOES own the patient.
        result.Should().BeOfType<OkObjectResult>();
    }


    [Fact]
    public async Task PrintPdf_ByDoctorWithoutAccess_Returns403_AndNeverGeneratesTheFile()
    {
        // The PDF carries the patient's name, file number, treating doctor and clinical
        // details — exporting it is a read of that patient's record.
        var seeded = await SeedLabOrderAsync();
        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.PrintPdf(seeded.LabOrderId);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        result.Should().NotBeOfType<FileContentResult>("a denied export must not produce a PDF");
        access.Verify(p => p.CanAccessPatientAsync(seeded.PatientId), Times.Once);
    }

    [Fact]
    public async Task Delete_ByDoctorWithoutAccess_Returns403_AndKeepsTheOrderActive()
    {
        var seeded = await SeedLabOrderAsync();
        var (access, user, audit) = BuildDeniedDoctor();
        var controller = LabOrdersTestData.BuildController(_db, access, user, audit);

        var result = await controller.Delete(seeded.LabOrderId);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);

        _db.ChangeTracker.Clear();
        var stored = await _db.LabOrders.FindAsync(seeded.LabOrderId);
        stored!.IsActive.Should().BeTrue("a 403 denial must not soft-delete the order");
    }

    private async Task<(Guid LabOrderId, Guid PatientId)> SeedLabOrderAsync(string status = "draft")
    {
        var patient = LabOrdersTestData.BuildPatient();
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        var order = LabOrdersTestData.BuildLabOrder(patient.Id, status: status);
        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync();

        return (order.Id, patient.Id);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
