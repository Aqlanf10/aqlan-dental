using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-PERM: verifies that PaymentsController enforces the granular finance.*
/// permissions (RolePermissions, owner-configurable from Settings) on top of the
/// coarse FinanceAccess policy. Admin bypasses; a non-admin role is allowed only
/// for the exact (resource, action) pairs granted in the database.
/// </summary>
public class PaymentsPermissionEnforcementTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService User(UserRole role)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(u => u.Role).Returns(role);
        mock.SetupGet(u => u.IsAdmin).Returns(role == UserRole.Admin);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.BranchId).Returns((Guid?)null);
        return mock.Object;
    }

    private static void Grant(AppDbContext db, string role, string resource,
        bool view = false, bool create = false, bool edit = false,
        bool delete = false, bool export = false, bool approve = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = resource,
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanDelete = delete,
            CanExport = export,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static (PaymentsController controller, Mock<IFinanceService> finance, Mock<IFinanceReadService> financeRead, Mock<IPdfService> pdf)
        Build(AppDbContext db, ICurrentUserService user)
    {
        var finance = new Mock<IFinanceService>();
        var financeRead = new Mock<IFinanceReadService>();
        var pdf = new Mock<IPdfService>();
        var audit = new Mock<IAuditService>();
        var push = new Mock<IRealTimePushService>();
        var controller = new PaymentsController(
            finance.Object, financeRead.Object, pdf.Object, audit.Object, user, db, push.Object,
            NullLogger<PaymentsController>.Instance);
        return (controller, finance, financeRead, pdf);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    [Fact]
    public async Task Reception_WithCreateGrant_CanCreatePayment()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", "finance.payments", view: true, create: true); // edit NOT granted
        var (controller, finance, financeRead, _) = Build(db, User(UserRole.Reception));
        finance.Setup(f => f.CreatePaymentAsync(It.IsAny<CreatePaymentRequest>()))
            .ReturnsAsync(new PaymentDto { Id = Guid.NewGuid(), Amount = 1000m, PaymentMethod = "cash" });

        var result = await controller.CreatePayment(new CreatePaymentRequest
        {
            PatientId = Guid.NewGuid(),
            Amount = 1000m,
            PaymentMethod = "cash",
        });

        result.Should().BeOfType<OkObjectResult>("Reception is granted finance.payments.create");
    }

    [Fact]
    public async Task Reception_WithoutEditGrant_CannotUpdatePayment()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", "finance.payments", view: true, create: true); // edit = false
        var (controller, finance, financeRead, _) = Build(db, User(UserRole.Reception));

        var result = await controller.UpdatePayment(Guid.NewGuid(), new UpdatePaymentRequest());

        StatusOf(result).Should().Be(403, "Reception is not granted finance.payments.edit");
        finance.Verify(f => f.UpdatePaymentAsync(It.IsAny<Guid>(), It.IsAny<UpdatePaymentRequest>()), Times.Never,
            "the guard must short-circuit before any write");
    }

    [Fact]
    public async Task Reception_WithoutPatientBalanceGrant_CannotViewFinanceSummary()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", "finance.payments", view: true, create: true); // no patient_balance row at all
        var (controller, finance, financeRead, _) = Build(db, User(UserRole.Reception));

        var result = await controller.GetPatientFinanceSummary(Guid.NewGuid());

        StatusOf(result).Should().Be(403, "Reception is not granted finance.patient_balance.view");
        financeRead.Verify(f => f.GetPatientFinanceSummaryAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Reception_WithReceiptGrant_CanPrintReceipt()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", "finance.receipts", view: true, create: true);
        var (controller, _, _, pdf) = Build(db, User(UserRole.Reception));
        pdf.Setup(p => p.GeneratePaymentReceiptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var result = await controller.GetPaymentPdf(Guid.NewGuid());

        result.Should().BeOfType<FileContentResult>("Reception is granted finance.receipts.view");
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        await using var db = CreateDb(); // no RolePermissions seeded
        var (controller, finance, financeRead, _) = Build(db, User(UserRole.Admin));
        finance.Setup(f => f.GetPaymentsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new List<PaymentDto>());

        var result = await controller.GetPayments();

        result.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard");
    }
}
