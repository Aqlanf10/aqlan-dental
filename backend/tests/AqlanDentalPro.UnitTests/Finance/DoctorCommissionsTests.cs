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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public class DoctorCommissionsTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateAdminUser(Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Admin);
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        return mock.Object;
    }

    private static FinanceV3Controller BuildFinanceV3Controller(AppDbContext db, ICurrentUserService? currentUser = null)
    {
        currentUser ??= CreateAdminUser();
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var financeService = new FinanceService(db, currentUser, notifications, new Mock<ILogger<FinanceService>>().Object, commissionService, journalEntryService);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;
        return new FinanceV3Controller(db, currentUser, financeService, audit, journalEntryService, treasuryResolution, logger);
    }

    [Fact]
    public async Task GetDoctorCommissions_ValidRange_AggregatesCorrectly()
    {
        // Arrange
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        var doctor = new Doctor
        {
            Id = doctorId,
            Name = "د. علي العظام",
            DefaultCommissionPercentage = 40m,
            UserId = Guid.NewGuid()
        };
        db.Doctors.Add(doctor);

        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "أحمد", LastName = "سعيد" };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-100",
            Status = InvoiceStatus.Issued,
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);

        // 2 line items for this doctor
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 10_000m,
            DoctorCommissionAmount = 4_000m,
            IsActive = true
        });
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 20_000m,
            DoctorCommissionAmount = 8_000m,
            IsActive = true
        });

        // 1 commission payment
        db.DoctorCommissionPayments.Add(new DoctorCommissionPayment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            Amount = 5_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);

        // Act
        var result = await controller.GetDoctorCommissions(
            doctorId: doctorId,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionSummaryDto>)okResult.Value!;

        list.Should().ContainSingle();
        var summary = list[0];
        summary.DoctorId.Should().Be(doctorId);
        summary.DoctorName.Should().Be("د. علي العظام");
        summary.CasesCount.Should().Be(2);
        summary.TotalServiceValue.Should().Be(30_000m);
        summary.CommissionPercentage.Should().Be(40m);
        summary.CommissionDue.Should().Be(12_000m);
        summary.CommissionPaid.Should().Be(5_000m);
        summary.CommissionRemaining.Should().Be(7_000m);
    }

    [Fact]
    public async Task GetDoctorCommissions_InvalidDates_ReturnsBadRequest()
    {
        // Arrange
        await using var db = CreateDb();
        var controller = BuildFinanceV3Controller(db);

        // Act
        var result = await controller.GetDoctorCommissions(
            doctorId: null,
            from: "invalid-date",
            to: "2026-06-02"
        );

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static ICurrentUserService CreateAccountantUser(Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        return mock.Object;
    }

    [Fact]
    public async Task GetDoctorCommissions_NonAdmin_FiltersByBranch()
    {
        // Arrange
        await using var db = CreateDb();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();

        var doctorA = new Doctor { Id = Guid.NewGuid(), Name = "Doctor A", BranchId = branchAId, UserId = Guid.NewGuid() };
        var doctorB = new Doctor { Id = Guid.NewGuid(), Name = "Doctor B", BranchId = branchBId, UserId = Guid.NewGuid() };
        db.Doctors.AddRange(doctorA, doctorB);

        var patientA = new Patient { Id = Guid.NewGuid(), FirstName = "Patient", LastName = "A", BranchId = branchAId };
        var patientB = new Patient { Id = Guid.NewGuid(), FirstName = "Patient", LastName = "B", BranchId = branchBId };
        db.Patients.AddRange(patientA, patientB);

        var invoiceA = new Invoice { Id = Guid.NewGuid(), PatientId = patientA.Id, Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow };
        var invoiceB = new Invoice { Id = Guid.NewGuid(), PatientId = patientB.Id, Status = InvoiceStatus.Issued, CreatedAt = DateTime.UtcNow };
        db.Invoices.AddRange(invoiceA, invoiceB);

        db.InvoiceLineItems.Add(new InvoiceLineItem { Id = Guid.NewGuid(), InvoiceId = invoiceA.Id, DoctorId = doctorA.Id, TotalPrice = 1000m, DoctorCommissionAmount = 400m, IsActive = true });
        db.InvoiceLineItems.Add(new InvoiceLineItem { Id = Guid.NewGuid(), InvoiceId = invoiceB.Id, DoctorId = doctorB.Id, TotalPrice = 2000m, DoctorCommissionAmount = 800m, IsActive = true });

        await db.SaveChangesAsync();

        var accountantUser = CreateAccountantUser(branchAId);
        var controller = BuildFinanceV3Controller(db, accountantUser);

        // Act
        var result = await controller.GetDoctorCommissions(
            doctorId: null,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionSummaryDto>)okResult.Value!;

        list.Should().ContainSingle();
        list[0].DoctorId.Should().Be(doctorA.Id);
        list[0].DoctorName.Should().Be("Doctor A");
        list[0].TotalServiceValue.Should().Be(1000m);
    }

    [Fact]
    public async Task GetDoctorCommissions_NonAdminNoBranch_ReturnsForbid()
    {
        // Arrange
        await using var db = CreateDb();
        var accountantUser = CreateAccountantUser(branchId: null);
        var controller = BuildFinanceV3Controller(db, accountantUser);

        // Act
        var result = await controller.GetDoctorCommissions(
            doctorId: null,
            from: "2026-06-01",
            to: "2026-06-30"
        );

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetDoctorCommissions_RequestOtherBranchDoctor_ReturnsForbid()
    {
        // Arrange
        await using var db = CreateDb();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();

        var doctorB = new Doctor { Id = Guid.NewGuid(), Name = "Doctor B", BranchId = branchBId, UserId = Guid.NewGuid() };
        db.Doctors.Add(doctorB);
        await db.SaveChangesAsync();

        var accountantUser = CreateAccountantUser(branchAId);
        var controller = BuildFinanceV3Controller(db, accountantUser);

        // Act
        var result = await controller.GetDoctorCommissions(
            doctorId: doctorB.Id,
            from: "2026-06-01",
            to: "2026-06-30"
        );

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }
}
