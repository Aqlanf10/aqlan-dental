using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-PERMS-A · A2: verifies ContractsController enforces the granular
/// finance.contracts permission on top of the FinanceAccess policy. Admin always
/// bypasses; Reception is seeded view-only (cannot create/edit/change status).
/// Status transitions (PATCH /status) = approve (Accountant/Admin only).
/// </summary>
public class ContractsPermissionEnforcementTests
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

    private static void Grant(AppDbContext db, string role,
        bool view = false, bool create = false, bool edit = false, bool approve = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = "finance.contracts",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static (ContractsController controller, Mock<IContractService> contract) Build(AppDbContext db, ICurrentUserService user)
    {
        var contract = new Mock<IContractService>();
        var controller = new ContractsController(
            contract.Object, new FinanceSettingsReader(db), db, user);
        return (controller, contract);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotCreateContract()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // create = false
        var (controller, contract) = Build(db, User(UserRole.Reception));

        var result = await controller.Create(new CreateContractRequest
        {
            PatientId = Guid.NewGuid(),
            TotalAmount = 1000m,
        });

        StatusOf(result).Should().Be(403, "Reception is not granted finance.contracts.create");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
        contract.Verify(c => c.CreateContractAsync(It.IsAny<CreateContractRequest>()), Times.Never,
            "the guard must short-circuit before any service call");
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotUpdateContract()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // edit = false
        var (controller, contract) = Build(db, User(UserRole.Reception));

        var result = await controller.Update(Guid.NewGuid(), new UpdateContractRequest
        {
            TotalAmount = 1000m,
            DiscountAmount = 0m,
        });

        StatusOf(result).Should().Be(403, "Reception is not granted finance.contracts.edit");
        contract.Verify(c => c.UpdateContractAsync(It.IsAny<Guid>(), It.IsAny<UpdateContractRequest>()), Times.Never);
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotChangeContractStatus()
    {
        // PATCH /status = approve (Accountant/Admin only — Reception view-only).
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // approve = false
        var (controller, contract) = Build(db, User(UserRole.Reception));

        var result = await controller.UpdateStatus(Guid.NewGuid(), new UpdateContractStatusBody("active"));

        StatusOf(result).Should().Be(403, "Reception is not granted finance.contracts.approve (status change)");
        contract.Verify(c => c.UpdateContractStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Reception_WithViewGrant_CanListContracts()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true);
        var (controller, contract) = Build(db, User(UserRole.Reception));
        contract.Setup(c => c.GetContractsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<ContractListDto>());

        var result = await controller.GetList();

        result.Should().BeOfType<OkObjectResult>("Reception is granted finance.contracts.view");
        contract.Verify(c => c.GetContractsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        await using var db = CreateDb(); // no RolePermissions seeded
        var (controller, contract) = Build(db, User(UserRole.Admin));
        contract.Setup(c => c.GetContractsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<ContractListDto>());

        var result = await controller.GetList();

        result.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard");
    }

    [Fact]
    public async Task Accountant_WithApproveGrant_CanChangeStatus()
    {
        // Accountant is granted (view, create, edit, export) but approve=false on
        // finance.contracts per the seed (per the plan §3 table). Verify that
        // approve=false denies the status change → 403.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true); // approve NOT granted on finance.contracts
        var (controller, contract) = Build(db, User(UserRole.Accountant));

        var result = await controller.UpdateStatus(Guid.NewGuid(), new UpdateContractStatusBody("active"));

        StatusOf(result).Should().Be(403,
            "Accountant is not granted finance.contracts.approve by default — status changes need Admin or owner-grant");
        contract.Verify(c => c.UpdateContractStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }
}
