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
