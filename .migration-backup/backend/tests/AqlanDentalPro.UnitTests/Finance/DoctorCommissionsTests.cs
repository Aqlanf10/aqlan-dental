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
        // FIN-PERM: the DoctorCommissions endpoints now enforce finance.commissions.view.
        // Non-admin (Accountant) tests must seed the RolePermission so they exercise their
        // real intent (branch isolation) and not the new authz gate. Admin bypasses anyway.
        if (currentUser.Role == UserRole.Accountant
            && !db.RolePermissions.Any(p => p.Role == "Accountant" && p.Resource == "finance.commissions"))
        {
            db.RolePermissions.Add(new RolePermission
            {
                Role = "Accountant", Resource = "finance.commissions",
                CanView = true, CanCreate = true, CanEdit = true,
                CanApprove = true, CanDelete = true, CanExport = true
            });
            db.SaveChanges();
        }
        var notifications = new Mock<INotificationService>().Object;
        var commissionService = new Mock<ICommissionService>().Object;
        var journalEntryService = new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object);
        var paymentService = new PaymentService(db, currentUser, notifications, new Mock<ILogger<PaymentService>>().Object, commissionService, journalEntryService);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<FinanceV3Controller>>().Object;
        return new FinanceV3Controller(db, currentUser, paymentService, new Mock<IInvoiceLedgerService>().Object, audit, journalEntryService, treasuryResolution, logger);
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
    public async Task GetDoctorCommissions_MissingLineDoctor_UsesVisitAppointmentDoctor()
    {
        await using var db = CreateDb();
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            Name = "Visit Doctor",
            DefaultCommissionPercentage = 25m,
            UserId = Guid.NewGuid()
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "Visit",
            LastName = "Patient"
        };
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "Treatment"
        };
        var visit = new Visit
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VisitId = visit.Id,
            AppointmentId = appointment.Id,
            InvoiceNumber = "INV-VISIT-DOCTOR",
            Status = InvoiceStatus.Issued,
            CreatedAt = DateTime.UtcNow
        };
        var cancelledInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VisitId = visit.Id,
            AppointmentId = appointment.Id,
            InvoiceNumber = "INV-CANCELLED",
            Status = InvoiceStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };

        db.AddRange(doctor, patient, appointment, visit, invoice, cancelledInvoice);
        db.InvoiceLineItems.AddRange(
            new InvoiceLineItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                RelatedVisitId = visit.Id,
                DoctorId = null,
                TotalPrice = 20_000m,
                DoctorCommissionPercentage = 25m,
                DoctorCommissionAmount = 5_000m,
                IsActive = true
            },
            new InvoiceLineItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = cancelledInvoice.Id,
                RelatedVisitId = visit.Id,
                DoctorId = null,
                TotalPrice = 99_000m,
                DoctorCommissionPercentage = 25m,
                DoctorCommissionAmount = 24_750m,
                IsActive = true
            });
        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);
        var result = await controller.GetDoctorCommissions(
            doctor.Id,
            DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"));

        var summary = ((List<DoctorCommissionSummaryDto>)((OkObjectResult)result).Value!).Single();
        summary.DoctorId.Should().Be(doctor.Id);
        summary.CasesCount.Should().Be(1);
        summary.TotalServiceValue.Should().Be(20_000m);
        summary.CommissionPercentage.Should().Be(25m);
        summary.CommissionDue.Should().Be(5_000m);
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
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
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
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    // ─── Earned-From-Collections Tests ──────────────────────────────────────

    [Fact]
    public async Task EarnedFromCollections_PartialPayment_ReturnsProportionalCommission()
    {
        // Invoice 100,000, paid 50,000, doctor 50%, no costs
        // Expected commission = (50,000 - 0 - 0) * 50% = 25,000
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. أحمد",
            DefaultCommissionPercentage = 50m,
            UserId = Guid.NewGuid()
        });

        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "سعيد", LastName = "علي" };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-PARTIAL",
            Status = InvoiceStatus.Issued,
            TotalAmount = 100_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);

        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 100_000m,
            LabCost = 0m,
            MaterialCost = 0m,
            OtherDirectCost = 0m,
            DoctorCommissionPercentage = 50m,
            DoctorCommissionAmount = 50_000m,
            IsActive = true
        });

        // Partial payment: 50,000 of 100,000
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PatientId = patient.Id,
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);
        var result = await controller.GetDoctorCommissionsEarnedFromCollections(
            doctorId: doctorId,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionEarnedFromCollectionsDto>)okResult.Value!;
        list.Should().ContainSingle();
        list[0].CommissionDue.Should().Be(25_000m);
    }

    [Fact]
    public async Task EarnedFromCollections_FullPayment_WithLabCost_ReturnsCorrectCommission()
    {
        // Invoice 100,000, paid 100,000, lab cost 20,000, doctor 50%
        // Expected commission = (100,000 - 20,000) * 50% = 40,000
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. خالد",
            DefaultCommissionPercentage = 50m,
            UserId = Guid.NewGuid()
        });

        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "محمد", LastName = "حسن" };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-FULL-LAB",
            Status = InvoiceStatus.Issued,
            TotalAmount = 100_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);

        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 100_000m,
            LabCost = 20_000m,
            MaterialCost = 0m,
            OtherDirectCost = 0m,
            DoctorCommissionPercentage = 50m,
            DoctorCommissionAmount = 40_000m,
            IsActive = true
        });

        // Full payment: 100,000
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PatientId = patient.Id,
            Amount = 100_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);
        var result = await controller.GetDoctorCommissionsEarnedFromCollections(
            doctorId: doctorId,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionEarnedFromCollectionsDto>)okResult.Value!;
        list.Should().ContainSingle();
        list[0].CommissionDue.Should().Be(40_000m);
    }

    [Fact]
    public async Task EarnedFromCollections_UnpaidInvoice_ReturnsZeroCommission()
    {
        // Invoice 100,000, paid 0
        // Expected commission = 0
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. سعيد",
            DefaultCommissionPercentage = 50m,
            UserId = Guid.NewGuid()
        });

        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "فاطمة", LastName = "عبدالله" };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-UNPAID",
            Status = InvoiceStatus.Issued,
            TotalAmount = 100_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);

        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 100_000m,
            LabCost = 0m,
            MaterialCost = 0m,
            OtherDirectCost = 0m,
            DoctorCommissionPercentage = 50m,
            DoctorCommissionAmount = 50_000m,
            IsActive = true
        });

        // No payments at all

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);
        var result = await controller.GetDoctorCommissionsEarnedFromCollections(
            doctorId: doctorId,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionEarnedFromCollectionsDto>)okResult.Value!;
        list.Should().ContainSingle();
        list[0].CommissionDue.Should().Be(0m);
    }

    [Fact]
    public async Task EarnedFromCollections_CancelledPayment_NotCounted()
    {
        // Invoice with a cancelled (inactive) payment should not count it
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        db.Doctors.Add(new Doctor
        {
            Id = doctorId,
            Name = "د. ناصر",
            DefaultCommissionPercentage = 50m,
            UserId = Guid.NewGuid()
        });

        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "عائشة", LastName = "سالم" };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-CANCELLED-PAY",
            Status = InvoiceStatus.Issued,
            TotalAmount = 100_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);

        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            DoctorId = doctorId,
            TotalPrice = 100_000m,
            LabCost = 0m,
            MaterialCost = 0m,
            OtherDirectCost = 0m,
            DoctorCommissionPercentage = 50m,
            DoctorCommissionAmount = 50_000m,
            IsActive = true
        });

        // Cancelled payment (IsActive = false) — should NOT be counted
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PatientId = patient.Id,
            Amount = 50_000m,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = false  // cancelled
        });

        await db.SaveChangesAsync();

        var controller = BuildFinanceV3Controller(db);
        var result = await controller.GetDoctorCommissionsEarnedFromCollections(
            doctorId: doctorId,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionEarnedFromCollectionsDto>)okResult.Value!;
        list.Should().ContainSingle();
        list[0].CommissionDue.Should().Be(0m);
    }

    [Fact]
    public async Task EarnedFromCollections_BranchIsolation_ReturnsOnlyBranchData()
    {
        // Non-admin user can only see their branch data
        await using var db = CreateDb();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();

        var doctorA = new Doctor { Id = Guid.NewGuid(), Name = "Doctor A", BranchId = branchAId, DefaultCommissionPercentage = 50m, UserId = Guid.NewGuid() };
        var doctorB = new Doctor { Id = Guid.NewGuid(), Name = "Doctor B", BranchId = branchBId, DefaultCommissionPercentage = 50m, UserId = Guid.NewGuid() };
        db.Doctors.AddRange(doctorA, doctorB);

        var patientA = new Patient { Id = Guid.NewGuid(), FirstName = "Patient", LastName = "A", BranchId = branchAId };
        var patientB = new Patient { Id = Guid.NewGuid(), FirstName = "Patient", LastName = "B", BranchId = branchBId };
        db.Patients.AddRange(patientA, patientB);

        var invoiceA = new Invoice { Id = Guid.NewGuid(), PatientId = patientA.Id, Status = InvoiceStatus.Issued, TotalAmount = 100_000m, CreatedAt = DateTime.UtcNow };
        var invoiceB = new Invoice { Id = Guid.NewGuid(), PatientId = patientB.Id, Status = InvoiceStatus.Issued, TotalAmount = 200_000m, CreatedAt = DateTime.UtcNow };
        db.Invoices.AddRange(invoiceA, invoiceB);

        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceA.Id, DoctorId = doctorA.Id,
            TotalPrice = 100_000m, DoctorCommissionPercentage = 50m, DoctorCommissionAmount = 50_000m, IsActive = true
        });
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceB.Id, DoctorId = doctorB.Id,
            TotalPrice = 200_000m, DoctorCommissionPercentage = 50m, DoctorCommissionAmount = 100_000m, IsActive = true
        });

        // Payments for both
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceA.Id, PatientId = patientA.Id,
            Amount = 100_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), InvoiceId = invoiceB.Id, PatientId = patientB.Id,
            Amount = 200_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true
        });

        await db.SaveChangesAsync();

        var accountantUser = CreateAccountantUser(branchAId);
        var controller = BuildFinanceV3Controller(db, accountantUser);
        var result = await controller.GetDoctorCommissionsEarnedFromCollections(
            doctorId: null,
            from: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            to: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")
        );

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = (List<DoctorCommissionEarnedFromCollectionsDto>)okResult.Value!;
        list.Should().ContainSingle();
        list[0].DoctorId.Should().Be(doctorA.Id);
        list[0].CommissionDue.Should().Be(50_000m);
    }
}
