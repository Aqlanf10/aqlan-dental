using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// اختبارات وحدة لمنطق خطط التقسيط - إنشاء وجدولة الأقساط الشهرية.
/// تشمل: التحقق من العقد، حساب المبالغ، فروق التقريب، منع التكرار.
/// </summary>
public class InstallmentPlanTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (FinanceService service, Guid branchId, Guid cashierId) CreateService(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId });
        db.SaveChanges();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();
        var journalEntryService = new Mock<IJournalEntryService>();

        // Mock CreateEntryAsync for payment-related journal entries
        journalEntryService.Setup(s => s.CreateEntryAsync(
            It.IsAny<FinancialDocumentType>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateOnly>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, CancellationToken ct) =>
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-TEST-001",
                    FinancialDocumentId = docId,
                    FinancialDocumentType = docType,
                    Description = desc,
                    EntryDate = date,
                    BranchId = branch,
                    PerformedBy = performedBy,
                    CashierSessionId = sessionId,
                    TreasuryId = treasuryId,
                    IsPosted = false,
                    IsReversal = false,
                };
                foreach (var (accountType, accountId, debit, credit, lineDesc) in lines)
                {
                    entry.Lines.Add(new JournalLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = entry.Id,
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc
                    });
                }
                return entry;
            });

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);
        return (service, branchId, cashierId);
    }

    private static Guid SeedPatient(AppDbContext db, Guid branchId)
    {
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            FirstName = "أحمد",
            LastName = "محمد",
            BranchId = branchId
        });
        db.SaveChanges();
        return patientId;
    }

    private static Guid SeedContract(AppDbContext db, Guid patientId, decimal totalAmount = 100_000m)
    {
        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            TotalAmount = totalAmount,
            DownPayment = 0,
            InstallmentsCount = 0,
            Status = ContractStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.Today)
        });
        db.SaveChanges();
        return contractId;
    }

    // ─── Basic Creation Tests ────────────────────────────────────────────

    [Fact]
    public async Task GenerateInstallmentPlan_CreatesPlan_WithCorrectNumberOfInstallments()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 120_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 20_000m,
            NumberOfMonths = 10,
            StartDate = new DateTime(2026, 6, 1)
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        result.Should().NotBeNull();
        result.ContractId.Should().Be(contractId);
        result.NumberOfMonths.Should().Be(10);
        result.DownPayment.Should().Be(20_000m);
        result.TotalAmount.Should().Be(120_000m);
        result.MonthlyAmount.Should().Be(10_000m); // (120000-20000)/10
        result.Installments.Should().HaveCount(10);
        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateInstallmentPlan_InstallmentsSum_EqualsRemainingAmount()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 100_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 10_000m,
            NumberOfMonths = 7,
            StartDate = DateTime.Today
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        var installmentsSum = result.Installments.Sum(i => i.Amount);
        installmentsSum.Should().Be(90_000m); // 100000 - 10000
    }

    [Fact]
    public async Task GenerateInstallmentPlan_RoundsCorrectly_LastInstallmentAbsorbsRounding()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        // 100,000 / 3 = 33,333.33... per month, but last installment absorbs the difference
        var contractId = SeedContract(db, patientId, 100_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 3,
            StartDate = DateTime.Today
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        // Total should still be exact
        var installmentsSum = result.Installments.Sum(i => i.Amount);
        installmentsSum.Should().Be(100_000m);

        // First two installments should be the rounded monthly amount
        var monthlyRounded = Math.Round(100_000m / 3, 2);
        result.Installments[0].Amount.Should().Be(monthlyRounded);
        result.Installments[1].Amount.Should().Be(monthlyRounded);

        // Last installment absorbs the rounding difference
        var lastInstallment = result.Installments[^1];
        lastInstallment.Amount.Should().Be(100_000m - (monthlyRounded * 2));
    }

    [Fact]
    public async Task GenerateInstallmentPlan_DueDates_AreMonthlySequential()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 60_000m);

        var startDate = new DateTime(2026, 6, 15);
        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 6,
            StartDate = startDate
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        for (int i = 0; i < result.Installments.Count; i++)
        {
            result.Installments[i].DueDate.Should().Be(startDate.AddMonths(i));
        }
    }

    [Fact]
    public async Task GenerateInstallmentPlan_AllInstallments_StartAsPending()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 5,
            StartDate = DateTime.Today
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        result.Installments.Should().OnlyContain(i => i.Status == InstallmentStatus.Pending);
    }

    // ─── Validation Tests ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenContractNotFound()
    {
        await using var db = CreateContext();
        var (service, _, _) = CreateService(db);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = Guid.NewGuid(),
            DownPayment = 0,
            NumberOfMonths = 6,
            StartDate = DateTime.Today
        };

        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*غير موجود*");
    }

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenContractNotActive()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);

        var contractId = Guid.NewGuid();
        db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            TotalAmount = 50_000m,
            Status = ContractStatus.Cancelled
        });
        await db.SaveChangesAsync();

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 5,
            StartDate = DateTime.Today
        };

        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*غير نشط*");
    }

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenPlanAlreadyExists()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 60_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 6,
            StartDate = DateTime.Today
        };

        // First creation succeeds
        await service.GenerateInstallmentPlanAsync(request);

        // Second creation should fail
        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*خطة تقسيط مسبقة*");
    }

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenDownPaymentExceedsTotal()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 50_000m, // Equal to total
            NumberOfMonths = 6,
            StartDate = DateTime.Today
        };

        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*الدفعة المقدمة*");
    }

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenDownPaymentNegative()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = -1_000m,
            NumberOfMonths = 6,
            StartDate = DateTime.Today
        };

        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*سالبة*");
    }

    [Fact]
    public async Task GenerateInstallmentPlan_Throws_WhenNumberOfMonthsZero()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 0,
            StartDate = DateTime.Today
        };

        var act = () => service.GenerateInstallmentPlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*أكبر من صفر*");
    }

    // ─── Retrieval Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GetInstallmentPlanByContractId_ReturnsPlan_WithOrderedInstallments()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 60_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 6,
            StartDate = new DateTime(2026, 1, 15)
        };

        await service.GenerateInstallmentPlanAsync(request);

        var result = await service.GetInstallmentPlanByContractIdAsync(contractId);

        result.Should().NotBeNull();
        result.ContractId.Should().Be(contractId);
        result.Installments.Should().HaveCount(6);
        result.Installments.Should().BeInAscendingOrder(i => i.DueDate);
    }

    [Fact]
    public async Task GetInstallmentPlanByContractId_Throws_WhenNoPlanExists()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var act = () => service.GetInstallmentPlanByContractIdAsync(contractId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*لا توجد خطة تقسيط*");
    }

    // ─── Edge Case Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInstallmentPlan_WithZeroDownPayment_DistributesFullAmount()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 300_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 0,
            NumberOfMonths = 24,
            StartDate = DateTime.Today
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        result.DownPayment.Should().Be(0);
        result.MonthlyAmount.Should().Be(12_500m); // 300000 / 24
        result.Installments.Sum(i => i.Amount).Should().Be(300_000m);
    }

    [Fact]
    public async Task GenerateInstallmentPlan_SingleMonth_CreatesOneInstallment()
    {
        await using var db = CreateContext();
        var (service, branchId, _) = CreateService(db);
        var patientId = SeedPatient(db, branchId);
        var contractId = SeedContract(db, patientId, 50_000m);

        var request = new CreateInstallmentPlanRequest
        {
            ContractId = contractId,
            DownPayment = 10_000m,
            NumberOfMonths = 1,
            StartDate = DateTime.Today
        };

        var result = await service.GenerateInstallmentPlanAsync(request);

        result.Installments.Should().HaveCount(1);
        result.Installments[0].Amount.Should().Be(40_000m); // 50000 - 10000
    }
}
