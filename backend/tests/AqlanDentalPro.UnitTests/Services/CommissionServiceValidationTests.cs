using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Validation tests for CommissionService using an in-memory database.
/// Covers: service defaults input validation, payment cap enforcement.
/// </summary>
public class CommissionServiceValidationTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CommissionService CreateService(AppDbContext db) =>
        new(db, new JournalEntryService(db, NullLogger<JournalEntryService>.Instance),
            new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance),
            NullLogger<CommissionService>.Instance);

    // ── UpdateServiceDefaults: cost validation ────────────────────────────────

    [Fact]
    public async Task UpdateServiceDefaults_NegativeMaterialCost_Throws()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: -1m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 30m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        await svc.Invoking(s => s.UpdateServiceDefaultsAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*سالبة*");
    }

    [Fact]
    public async Task UpdateServiceDefaults_NegativeLabCost_Throws()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: -500m,
            DefaultDoctorCommissionPercentage: 30m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        await svc.Invoking(s => s.UpdateServiceDefaultsAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*سالبة*");
    }

    [Fact]
    public async Task UpdateServiceDefaults_NegativeCommissionPercentage_Throws()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: -5m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        await svc.Invoking(s => s.UpdateServiceDefaultsAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*0*100*");
    }

    [Fact]
    public async Task UpdateServiceDefaults_CommissionPercentageOver100_Throws()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 101m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        await svc.Invoking(s => s.UpdateServiceDefaultsAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*0*100*");
    }

    [Fact]
    public async Task UpdateServiceDefaults_BoundaryZeroPercentage_Succeeds()
    {
        await using var db = CreateDb();
        var clinicService = new ClinicService
        {
            ArabicName   = "فحص",
            EnglishName  = "Exam",
            Code         = "EXM",
            Category     = ServiceCategory.Consultation,
            DefaultPrice = 5_000m,
            IsActive     = true
        };
        db.ClinicServices.Add(clinicService);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 0m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = await svc.UpdateServiceDefaultsAsync(clinicService.Id, req);

        result.Should().NotBeNull();
        result!.DefaultDoctorCommissionPercentage.Should().Be(0m);
    }

    [Fact]
    public async Task UpdateServiceDefaults_Boundary100Percentage_Succeeds()
    {
        await using var db = CreateDb();
        var clinicService = new ClinicService
        {
            ArabicName   = "تنظيف",
            EnglishName  = "Cleaning",
            Code         = "CLN",
            Category     = ServiceCategory.Preventive,
            DefaultPrice = 3_000m,
            IsActive     = true
        };
        db.ClinicServices.Add(clinicService);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 100m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = await svc.UpdateServiceDefaultsAsync(clinicService.Id, req);

        result.Should().NotBeNull();
        result!.DefaultDoctorCommissionPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task UpdateServiceDefaults_NullPercentage_Succeeds()
    {
        await using var db = CreateDb();
        var clinicService = new ClinicService
        {
            ArabicName   = "حشو",
            EnglishName  = "Filling",
            Code         = "FIL",
            Category     = ServiceCategory.Restorative,
            DefaultPrice = 8_000m,
            IsActive     = true
        };
        db.ClinicServices.Add(clinicService);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: null,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = await svc.UpdateServiceDefaultsAsync(clinicService.Id, req);

        result.Should().NotBeNull();
        result!.DefaultDoctorCommissionPercentage.Should().BeNull();
    }

    [Fact]
    public async Task UpdateServiceDefaults_NonExistentService_ReturnsNull()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 0m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 0m,
            DefaultDoctorCommissionPercentage: 30m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = await svc.UpdateServiceDefaultsAsync(Guid.NewGuid(), req);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateServiceDefaults_ValidInput_SavesSuccessfully()
    {
        await using var db = CreateDb();
        var clinicService = new ClinicService
        {
            ArabicName   = "تركيب",
            EnglishName  = "Crown",
            Code         = "CRN",
            Category     = ServiceCategory.Prosthodontics,
            DefaultPrice = 20_000m,
            IsActive     = true
        };
        db.ClinicServices.Add(clinicService);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new UpdateServiceCommissionDefaultsRequest(
            DefaultMaterialCost: 2_000m,
            DefaultMaterialCostType: MaterialCostType.FixedAmount,
            DefaultLabCost: 3_000m,
            DefaultDoctorCommissionPercentage: 35m,
            CommissionBaseRule: CommissionBaseRule.AfterDiscountAndCosts);

        var result = await svc.UpdateServiceDefaultsAsync(clinicService.Id, req);

        result.Should().NotBeNull();
        result!.DefaultMaterialCost.Should().Be(2_000m);
        result.DefaultLabCost.Should().Be(3_000m);
        result.DefaultDoctorCommissionPercentage.Should().Be(35m);
    }

    // ── RecordPayment: overpayment cap ────────────────────────────────────────

    [Fact]
    public async Task RecordPayment_AmountExceedsRemaining_Throws()
    {
        await using var db = CreateDb();

        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع" });
        var vaultTreasury = new Treasury { Id = Guid.NewGuid(), Name = "درج كاشير", Type = TreasuryType.Vault, Balance = 500_000m, BranchId = branchId, IsActive = true };
        var bankTreasury = new Treasury { Id = Guid.NewGuid(), Name = "حساب بنكي", Type = TreasuryType.Bank, Balance = 1_000_000m, BranchId = branchId, IsActive = true };
        db.Treasuries.Add(vaultTreasury);
        db.Treasuries.Add(bankTreasury);

        var doctor = new Doctor { Name = "د. أحمد", IsActive = true, BranchId = branchId };
        db.Doctors.Add(doctor);

        // One approved line item worth 10,000 to the doctor
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-001",
            Status        = InvoiceStatus.Issued,
            IsActive      = true,
        };
        db.Invoices.Add(invoice);

        var lineItem = new InvoiceLineItem
        {
            Invoice                   = invoice,
            Description               = "تركيب",
            Quantity                  = 1,
            UnitPrice                 = 50_000m,
            TotalPrice                = 50_000m,
            DoctorId                  = doctor.Id,
            DoctorCommissionPercentage = 20m,
            DoctorCommissionAmount    = 10_000m,
            CommissionStatus          = CommissionStatus.Approved,
            IsActive                  = true
        };
        db.InvoiceLineItems.Add(lineItem);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new RecordCommissionPaymentRequest(
            DoctorId:    doctor.Id,
            Amount:      15_000m,   // exceeds 10,000 remaining
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "bank", // use bank to avoid cashier session requirement
            ReferenceNumber: null,
            Notes: null,
            LineItemIds: null);

        await svc.Invoking(s => s.RecordPaymentAsync(req, Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*يتجاوز*");
    }

    [Fact]
    public async Task RecordPayment_AmountWithinRemaining_Succeeds()
    {
        await using var db = CreateDb();

        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع" });
        var vaultTreasury = new Treasury { Id = Guid.NewGuid(), Name = "درج كاشير", Type = TreasuryType.Vault, Balance = 500_000m, BranchId = branchId, IsActive = true };
        var bankTreasury = new Treasury { Id = Guid.NewGuid(), Name = "حساب بنكي", Type = TreasuryType.Bank, Balance = 1_000_000m, BranchId = branchId, IsActive = true };
        db.Treasuries.Add(vaultTreasury);
        db.Treasuries.Add(bankTreasury);

        var doctor = new Doctor { Name = "د. سارة", IsActive = true, BranchId = branchId };
        db.Doctors.Add(doctor);

        var invoice = new Invoice
        {
            InvoiceNumber = "INV-002",
            Status        = InvoiceStatus.Issued,
            IsActive      = true,
        };
        db.Invoices.Add(invoice);

        var lineItem = new InvoiceLineItem
        {
            Invoice                   = invoice,
            Description               = "حشو",
            Quantity                  = 1,
            UnitPrice                 = 10_000m,
            TotalPrice                = 10_000m,
            DoctorId                  = doctor.Id,
            DoctorCommissionPercentage = 30m,
            DoctorCommissionAmount    = 3_000m,
            CommissionStatus          = CommissionStatus.Approved,
            IsActive                  = true
        };
        db.InvoiceLineItems.Add(lineItem);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var req = new RecordCommissionPaymentRequest(
            DoctorId:    doctor.Id,
            Amount:      2_000m,   // within 3,000 remaining
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod: "bank", // use bank to avoid cashier session requirement
            ReferenceNumber: null,
            Notes: null,
            LineItemIds: null);

        var result = await svc.RecordPaymentAsync(req, Guid.NewGuid());

        result.Should().NotBeNull();
        result.Amount.Should().Be(2_000m);
        result.DoctorId.Should().Be(doctor.Id);
    }

    [Fact]
    public async Task RecordPayment_DoesNotDebitTreasuryInDifferentCurrency()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patient = new Patient { FirstName = "Currency", LastName = "Guard", BranchId = branchId };
        var doctor = new Doctor { Name = "Doctor", IsActive = true, BranchId = branchId };
        db.Branches.Add(new Branch { Id = branchId, Name = "Branch" });
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        db.Treasuries.Add(new Treasury
        {
            Name = "YER bank", Type = TreasuryType.Bank, Balance = 1_000_000m,
            BranchId = branchId, Currency = "YER", IsActive = true
        });
        var invoice = new Invoice
        {
            Patient = patient, InvoiceNumber = "INV-SAR", Currency = "SAR",
            Status = InvoiceStatus.Issued, IsActive = true
        };
        db.Invoices.Add(invoice);
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Invoice = invoice, Description = "Service", Quantity = 1, UnitPrice = 1_000m,
            TotalPrice = 1_000m, DoctorId = doctor.Id, DoctorCommissionAmount = 300m,
            DoctorCommissionPercentage = 30m, CommissionStatus = CommissionStatus.Approved, IsActive = true
        });
        await db.SaveChangesAsync();

        var request = new RecordCommissionPaymentRequest(
            doctor.Id, 100m, DateOnly.FromDateTime(DateTime.UtcNow), "bank", null, null, null,
            branchId, "SAR");

        await CreateService(db).Invoking(service => service.RecordPaymentAsync(request, Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>();
        db.ChangeTracker.Clear();
        (await db.Treasuries.SingleAsync(treasury => treasury.Currency == "YER"))
            .Balance.Should().Be(1_000_000m);
    }

    // ── RecordPayment: correction lines (CORE-FIN-LAB-ADJ) ────────────────────

    /// <summary>
    /// Seeds a doctor with one Approved 3,000 commission and a single correction line of
    /// <paramref name="adjustmentAmount"/>, then returns the doctor.
    /// </summary>
    private static async Task<Doctor> SeedDoctorWithAdjustmentAsync(
        AppDbContext db, decimal adjustmentAmount)
    {
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع" });
        db.Treasuries.Add(new Treasury
        {
            Name = "حساب بنكي", Type = TreasuryType.Bank,
            Balance = 1_000_000m, BranchId = branchId, IsActive = true,
        });

        var doctor = new Doctor { Name = "د. أحمد", IsActive = true, BranchId = branchId };
        db.Doctors.Add(doctor);

        var invoice = new Invoice { InvoiceNumber = "INV-ADJ", Status = InvoiceStatus.Issued, IsActive = true };
        db.Invoices.Add(invoice);

        var lineItem = new InvoiceLineItem
        {
            Invoice                    = invoice,
            Description                = "حشو",
            Quantity                   = 1,
            UnitPrice                  = 10_000m,
            TotalPrice                 = 10_000m,
            DoctorId                   = doctor.Id,
            DoctorCommissionPercentage = 30m,
            DoctorCommissionAmount     = 3_000m,
            CommissionStatus           = CommissionStatus.Approved,
            IsActive                   = true,
        };
        db.InvoiceLineItems.Add(lineItem);

        db.DoctorCommissionAdjustments.Add(new DoctorCommissionAdjustment
        {
            DoctorId          = doctor.Id,
            InvoiceLineItemId = lineItem.Id,
            InvoiceId         = invoice.Id,
            AdjustmentAmount  = adjustmentAmount,
            Reason            = "تغيّرت تكلفة المعمل بعد الصرف",
            Status            = CommissionAdjustmentStatus.Pending,
            IsActive          = true,
        });

        await db.SaveChangesAsync();
        return doctor;
    }

    private static RecordCommissionPaymentRequest PayRequest(Guid doctorId, decimal amount)
        => new(
            DoctorId:        doctorId,
            Amount:          amount,
            PaymentDate:     new DateOnly(2026, 7, 15),
            PaymentMethod:   "bank",   // avoids the cashier-session requirement
            ReferenceNumber: null,
            Notes:           null,
            LineItemIds:     null);

    [Fact]
    public async Task RecordPayment_PositiveAdjustment_RaisesTheCeilingAboveTheLineItemsAlone()
    {
        // 3,000 accrued + a 1,500 correction the clinic still owes = 4,500 payable. Before the
        // correction was folded in, this payment would have been refused as an overpayment.
        await using var db = CreateDb();
        var doctor = await SeedDoctorWithAdjustmentAsync(db, adjustmentAmount: 1_500m);

        var result = await CreateService(db).RecordPaymentAsync(PayRequest(doctor.Id, 4_500m), Guid.NewGuid());

        result.Amount.Should().Be(4_500m);
    }

    [Fact]
    public async Task RecordPayment_NegativeAdjustment_LowersTheCeiling()
    {
        // 3,000 accrued − 1,000 overpaid earlier = 2,000 payable. Paying the full 3,000 would
        // hand the doctor money they have already received once.
        await using var db = CreateDb();
        var doctor = await SeedDoctorWithAdjustmentAsync(db, adjustmentAmount: -1_000m);

        await CreateService(db).Invoking(s => s.RecordPaymentAsync(PayRequest(doctor.Id, 3_000m), Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*يتجاوز*");
    }

    [Fact]
    public async Task RecordPayment_StampsOpenCorrectionsWithTheDisbursementThatCarriedThem()
    {
        await using var db = CreateDb();
        var doctor = await SeedDoctorWithAdjustmentAsync(db, adjustmentAmount: 1_500m);

        var payment = await CreateService(db).RecordPaymentAsync(PayRequest(doctor.Id, 4_500m), Guid.NewGuid());

        var adjustment = await db.DoctorCommissionAdjustments.SingleAsync();
        adjustment.Status.Should().Be(CommissionAdjustmentStatus.Settled);
        adjustment.SettledByPaymentId.Should().Be(payment.Id);
        adjustment.SettledOn.Should().Be(new DateOnly(2026, 7, 15));

        // Settled is a RECORD, not an arithmetic gate: the correction stays in the balance, so
        // a second payment of the same amount must still be refused.
        await CreateService(db).Invoking(s => s.RecordPaymentAsync(PayRequest(doctor.Id, 4_500m), Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*يتجاوز*");
    }

    [Fact]
    public async Task RecordPayment_CancelledAdjustment_DoesNotCountTowardTheBalance()
    {
        await using var db = CreateDb();
        var doctor = await SeedDoctorWithAdjustmentAsync(db, adjustmentAmount: 1_500m);

        var adjustment = await db.DoctorCommissionAdjustments.SingleAsync();
        adjustment.Status = CommissionAdjustmentStatus.Cancelled;
        await db.SaveChangesAsync();

        await CreateService(db).Invoking(s => s.RecordPaymentAsync(PayRequest(doctor.Id, 4_500m), Guid.NewGuid()))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*يتجاوز*");
    }
}
