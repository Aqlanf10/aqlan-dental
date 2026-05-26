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
        new(db, new JournalEntryService(db, NullLogger<JournalEntryService>.Instance), NullLogger<CommissionService>.Instance);

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
        var treasury = new Treasury { Id = Guid.NewGuid(), Name = "الخزنة", Type = TreasuryType.Vault, Balance = 500_000m, BranchId = branchId, IsActive = true };
        db.Treasuries.Add(treasury);

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
            PaymentMethod: null,
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
        var treasury = new Treasury { Id = Guid.NewGuid(), Name = "الخزنة", Type = TreasuryType.Vault, Balance = 500_000m, BranchId = branchId, IsActive = true };
        db.Treasuries.Add(treasury);

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
            PaymentMethod: "cash",
            ReferenceNumber: null,
            Notes: null,
            LineItemIds: null);

        var result = await svc.RecordPaymentAsync(req, Guid.NewGuid());

        result.Should().NotBeNull();
        result.Amount.Should().Be(2_000m);
        result.DoctorId.Should().Be(doctor.Id);
    }
}
