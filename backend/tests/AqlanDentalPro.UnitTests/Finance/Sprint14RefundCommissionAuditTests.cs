using System.Text.Json;
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
/// Sprint 14: Refund → commission audit + idempotency tests.
///
/// Audit finding: "Refund does not automatically reverse commission or clearly audit it."
///
/// Sprint 14 chose the SAFE approach: do NOT auto-reverse commission on refund
/// (auto-reversal would break accounting for already-Approved/Paid accrual-mode
/// commissions — those have already been disbursed via DoctorCommissionPayment +
/// Treasury outflow + JournalEntry, and reversing them requires reversal entries,
/// treasury top-ups, and possibly already-closed cashier sessions).
///
/// Instead, refund now writes an explicit Arabic audit-log warning
/// (Resource = "PaymentRefund.CommissionAdjustment") flagging that the
/// owner/accountant must manually review commission line items for the affected
/// invoice/contract. OnPaymentCollection commissions ARE still re-proportioned
/// automatically (unchanged behavior) — that path is not the audit gap.
///
/// Idempotency was also strengthened: the original Phase 0B guard only blocked
/// an exact-duplicate FULL refund. It left two real holes — partial refunds
/// could be issued repeatedly, and a full refund after a partial refund was
/// allowed (cumulative could exceed the original payment). The new guard sums
/// ALL prior refunds against the same payment and rejects when cumulative would
/// exceed the original amount.
///
/// Uses InMemory provider (matches FinanceV2Phase0BTests).
/// </summary>
public class Sprint14RefundCommissionAuditTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId });
        db.SaveChanges();

        return (branchId, cashierId);
    }

    private static CashierSession CreateOpenSession(AppDbContext db, Guid cashierId, Guid branchId, decimal openingBalance = 100_000m)
    {
        var session = new CashierSession
        {
            SessionNumber = $"CS-{DateTime.UtcNow:yyyyMMdd}-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-2),
            OpeningBalance = openingBalance,
            ExpectedClosingCash = openingBalance,
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private static (FinanceService service, Guid branchId, Guid cashierId) CreateService(AppDbContext db)
    {
        var (branchId, cashierId) = SeedBranchAndUser(db);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(cashierId);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var notifications = new Mock<INotificationService>();
        var logger = new Mock<ILogger<FinanceService>>();
        var commissionService = new Mock<ICommissionService>();
        var journalEntryService = new Mock<IJournalEntryService>();

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
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialDocumentType docType, Guid docId, string desc, DateOnly date, Guid branch, Guid performedBy, Guid? sessionId, Guid? treasuryId, IEnumerable<(JournalAccountType, Guid, decimal, decimal, string?)> lines, bool autoSave, CancellationToken ct) =>
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
                        AccountType = accountType,
                        AccountId = accountId,
                        Debit = debit,
                        Credit = credit,
                        Description = lineDesc,
                        BranchId = branch,
                    });
                }
                return entry;
            });
        journalEntryService.Setup(s => s.CreateReversalEntryAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid originalId, string reason, Guid performedBy, CancellationToken ct) =>
            {
                return new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    EntryNumber = "JE-REV-001",
                    FinancialDocumentId = Guid.NewGuid(),
                    FinancialDocumentType = FinancialDocumentType.PaymentDeletion,
                    Description = $"Reversal: {reason}",
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    BranchId = Guid.Empty,
                    PerformedBy = performedBy,
                    IsPosted = false,
                    IsReversal = true,
                    ReversalOfEntryId = originalId,
                };
            });

        var service = new FinanceService(db, currentUser.Object, notifications.Object, logger.Object, commissionService.Object, journalEntryService.Object);

        return (service, branchId, cashierId);
    }

    private static Patient SeedPatient(AppDbContext db, Guid branchId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "تجريبي",
            PatientNumber = $"P-{Guid.NewGuid().ToString()[..8]}",
            BranchId = branchId
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient;
    }

    // ─── Audit entry tests ───────────────────────────────────────────────

    [Fact]
    public async Task Refund_CreatesAuditEntry_ForCommissionAdjustment()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خلع ضرس"
        });

        // Act: refund the payment
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد لأغراض اختبارية");

        // Assert: a commission-adjustment warning audit log entry was created.
        // Resource name is distinct ("PaymentRefund.CommissionAdjustment") so finance
        // managers can filter refund-driven commission warnings separately from the
        // primary refund audit (Resource = "PaymentRefund", written by the controller).
        var auditEntry = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "PaymentRefund.CommissionAdjustment"
                                   && a.ResourceId != null
                                   && a.Action == AuditAction.Refund);

        auditEntry.Should().NotBeNull(
            "refund must create a commission-adjustment audit warning so the audit " +
            "trail explicitly flags that commission may need manual review");

        // The warning payload must be persisted in NewData and contain an Arabic message
        // (CLAUDE.md: all user-facing audit/error content in Arabic).
        auditEntry!.NewData.Should().NotBeNull();
        var newDataJson = auditEntry.NewData!.RootElement;
        newDataJson.TryGetProperty("warning", out var warningProp).Should().BeTrue();
        var warningText = warningProp.GetString();
        warningText.Should().NotBeNullOrEmpty();
        warningText.Should().Contain("استرداد", "warning message must be Arabic");
        warningText.Should().Contain("العمولات", "warning must mention commissions");

        // Payload must carry the linkage fields finance managers need to investigate.
        newDataJson.TryGetProperty("originalPaymentId", out var origIdProp).Should().BeTrue();
        origIdProp.GetGuid().Should().Be(paymentDto.Id);

        newDataJson.TryGetProperty("refundAmount", out var amtProp).Should().BeTrue();
        amtProp.GetDecimal().Should().Be(10_000m);

        newDataJson.TryGetProperty("isPartialRefund", out var partialProp).Should().BeTrue();
        partialProp.GetBoolean().Should().BeFalse("this was a full refund");

        newDataJson.TryGetProperty("patientId", out var pidProp).Should().BeTrue();
        pidProp.GetGuid().Should().Be(patient.Id);
    }

    [Fact]
    public async Task Refund_PartialRefund_CreatesAuditEntryWithPartialFlag()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "حشو عصب"
        });

        // Partial refund: 4,000 out of 10,000
        var result = await service.RefundPaymentAsync(paymentDto.Id, "استرداد جزئي", partialAmount: 4_000m);

        result!.Amount.Should().Be(-4_000m, "partial refund amount is negative of the refunded portion");
        result.ServiceDescription.Should().StartWith("استرداد جزئي");

        var auditEntry = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "PaymentRefund.CommissionAdjustment");

        auditEntry.Should().NotBeNull();
        var newDataJson = auditEntry!.NewData!.RootElement;
        newDataJson.TryGetProperty("isPartialRefund", out var partialProp).Should().BeTrue();
        partialProp.GetBoolean().Should().BeTrue("this was a partial refund");

        newDataJson.TryGetProperty("refundAmount", out var amtProp).Should().BeTrue();
        amtProp.GetDecimal().Should().Be(4_000m);
    }

    // ─── Idempotency tests ───────────────────────────────────────────────

    [Fact]
    public async Task Refund_IsIdempotent_FullRefundTwice_RejectsSecond()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خلع ضرس"
        });

        // First refund — should succeed
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد أول");

        // Second refund of the same payment — must throw (Phase 0B-compatible behavior).
        // The cumulative refund would be 20,000 on a 10,000 payment — rejected.
        var act = () => service.RefundPaymentAsync(paymentDto.Id, "استرداد ثاني");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*استرداد هذه الدفعة مسبقاً*");
    }

    [Fact]
    public async Task Refund_IsIdempotent_PartialRefundExceedingOriginal_Rejects()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "تركيب جسر"
        });

        // First partial refund — 7,000 of 10,000. Should succeed.
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد جزئي ٧٠٪", partialAmount: 7_000m);

        // Second partial refund of 5,000 — cumulative would be 12,000 > 10,000. Must reject.
        // (This is the hole the Sprint 14 guard closes: the original Phase 0B guard only
        // matched full refunds (`Amount == -payment.Amount`), so a partial refund was never
        // detected and the user could issue partial refunds repeatedly.)
        var act = () => service.RefundPaymentAsync(paymentDto.Id, "استرداد جزئي ثاني", partialAmount: 5_000m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*استرداد هذه الدفعة مسبقاً*");
    }

    [Fact]
    public async Task Refund_IsIdempotent_FullAfterPartial_RejectsWhenCumulativeExceeds()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "زراعة"
        });

        // First partial refund — 5,000 of 10,000. Should succeed.
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد نصف المبلغ", partialAmount: 5_000m);

        // Full refund of 10,000 after a 5,000 partial — cumulative would be 15,000 > 10,000.
        // Must reject. (Original Phase 0B guard missed this: the prior refund had
        // Amount == -5,000 (not -10,000) and ServiceDescription "استرداد جزئي..." (not
        // "استرداد:"), so the existing-refund check found nothing and the full refund
        // went through — refunding 15,000 total on a 10,000 payment.)
        var act = () => service.RefundPaymentAsync(paymentDto.Id, "استرداد كامل بعد جزئي");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*استرداد هذه الدفعة مسبقاً*");
    }

    [Fact]
    public async Task Refund_IsIdempotent_PartialUpToOriginal_SucceedsThenBlocks()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "تقويم"
        });

        // Partial refund 1: 5,000 — succeeds.
        await service.RefundPaymentAsync(paymentDto.Id, "أول دفعة استرداد", partialAmount: 5_000m);

        // Partial refund 2: 5,000 — cumulative = 10,000 == original. Should succeed
        // (refund up to but not exceeding the original amount is valid).
        var second = await service.RefundPaymentAsync(paymentDto.Id, "ثاني دفعة استرداد", partialAmount: 5_000m);
        second!.Amount.Should().Be(-5_000m);

        // Partial refund 3: any further amount — cumulative would exceed 10,000. Must reject.
        var act = () => service.RefundPaymentAsync(paymentDto.Id, "محاولة ثالثة", partialAmount: 1_000m);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*استرداد هذه الدفعة مسبقاً*");
    }

    // ─── Non-regression: existing Phase 0B behavior still works ──────────

    [Fact]
    public async Task Refund_FirstRefund_Succeeds_And_CreatesAuditEntry()
    {
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "خلع ضرس"
        });

        var result = await service.RefundPaymentAsync(paymentDto.Id, "استرداد أول");

        result.Should().NotBeNull();
        result!.Amount.Should().Be(-10_000m);
        result.ServiceDescription.Should().StartWith("استرداد:");

        // Audit entry must also be created (Sprint 14 addition).
        var auditCount = await db.AuditLogs
            .CountAsync(a => a.Resource == "PaymentRefund.CommissionAdjustment");
        auditCount.Should().Be(1, "exactly one commission-adjustment warning per refund");
    }

    // ─── Audit payload richness: high-risk flag when Approved/Paid commissions exist ──

    [Fact]
    public async Task Refund_WithApprovedOnPaymentCommission_EmitsHighRiskWarning()
    {
        // Scenario: payment is linked to an invoice that has an OnPaymentCollection
        // commission line item already in Approved status. The audit warning must
        // escalate to the high-risk variant so finance staff can prioritize it.
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        // Seed: ClinicService with OnPaymentCollection recognition mode
        var svc = new ClinicService
        {
            Id = Guid.NewGuid(),
            ArabicName = "تقويم",
            EnglishName = "Ortho",
            Code = "ORTHO-1",
            CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection,
        };
        db.ClinicServices.Add(svc);

        // Seed: Invoice linked to the patient
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-SPRINT14-001",
            Status = InvoiceStatus.Issued,
            TotalAmount = 10_000m,
            CreatedAt = DateTime.UtcNow,
        };
        db.Invoices.Add(invoice);

        // Seed: InvoiceLineItem in Approved commission status (the high-risk case)
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            ServiceId = svc.Id,
            Service = svc,
            DoctorId = null,
            TotalPrice = 10_000m,
            DoctorCommissionPercentage = 40m,
            DoctorCommissionAmount = 4_000m,
            CommissionStatus = CommissionStatus.Approved,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        // Create a payment linked to the invoice (so the refund flow has InvoiceId context)
        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            InvoiceId = invoice.Id,
            Amount = 10_000m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة لفاتورة تقويم"
        });

        // Act: refund the payment
        await service.RefundPaymentAsync(paymentDto.Id, "استرداد فاتورة بها عمولة معتمدة");

        // Assert: the audit entry must flag the high-risk scenario
        var auditEntry = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "PaymentRefund.CommissionAdjustment");

        auditEntry.Should().NotBeNull();
        var newDataJson = auditEntry!.NewData!.RootElement;

        newDataJson.TryGetProperty("hasApprovedOrPaidOnPaymentCollection", out var hrProp).Should().BeTrue();
        hrProp.GetBoolean().Should().BeTrue(
            "the invoice had an Approved OnPaymentCollection commission line item");

        newDataJson.TryGetProperty("onPaymentCollectionItemsAffected", out var cntProp).Should().BeTrue();
        cntProp.GetInt32().Should().Be(1, "exactly one OnPaymentCollection line item was on the invoice");

        newDataJson.TryGetProperty("warning", out var warnProp).Should().BeTrue();
        var warningText = warnProp.GetString();
        warningText.Should().Contain("تحذير عالي الخطورة",
            "the high-risk warning variant must be used when Approved/Paid commissions exist");
        warningText.Should().Contain("العمولات");
    }

    [Fact]
    public async Task Refund_WithoutApprovedCommissions_UsesBaselineWarning()
    {
        // Scenario: payment is linked to an invoice with OnPaymentCollection commission
        // line items, but none are Approved/Paid yet (all in Calculated status). The
        // audit warning must NOT escalate to the high-risk variant.
        await using var db = CreateContext();
        var (service, branchId, cashierId) = CreateService(db);
        var patient = SeedPatient(db, branchId);
        CreateOpenSession(db, cashierId, branchId);

        var svc = new ClinicService
        {
            Id = Guid.NewGuid(),
            ArabicName = "حشو",
            EnglishName = "Filling",
            Code = "FILL-1",
            CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection,
        };
        db.ClinicServices.Add(svc);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-SPRINT14-002",
            Status = InvoiceStatus.Issued,
            TotalAmount = 5_000m,
            CreatedAt = DateTime.UtcNow,
        };
        db.Invoices.Add(invoice);

        // Calculated (not Approved/Paid) — should NOT trigger high-risk warning
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            ServiceId = svc.Id,
            Service = svc,
            TotalPrice = 5_000m,
            DoctorCommissionPercentage = 40m,
            DoctorCommissionAmount = 2_000m,
            CommissionStatus = CommissionStatus.Calculated,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var paymentDto = await service.CreatePaymentAsync(new CreatePaymentRequest
        {
            PatientId = patient.Id,
            InvoiceId = invoice.Id,
            Amount = 5_000m,
            PaymentMethod = "cash",
            ServiceDescription = "دفعة لفاتورة حشو"
        });

        await service.RefundPaymentAsync(paymentDto.Id, "استرداد عادي");

        var auditEntry = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "PaymentRefund.CommissionAdjustment");

        auditEntry.Should().NotBeNull();
        var newDataJson = auditEntry!.NewData!.RootElement;

        newDataJson.TryGetProperty("hasApprovedOrPaidOnPaymentCollection", out var hrProp).Should().BeTrue();
        hrProp.GetBoolean().Should().BeFalse(
            "no commission line items were Approved or Paid");

        newDataJson.TryGetProperty("warning", out var warnProp).Should().BeTrue();
        var warningText = warnProp.GetString();
        warningText.Should().NotContain("تحذير عالي الخطورة",
            "baseline warning must be used when no Approved/Paid commissions exist");
        warningText.Should().Contain("العمولات",
            "baseline warning must still mention commissions so finance staff can grep");
    }
}
