using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.Finance;

public class PartyAccountStatementServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"party-statements-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Statements_KeepCurrenciesSeparate_ForAllSupportedPartyTypes()
    {
        await using var db = CreateContext();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "QA", LastName = "Patient",
            PatientNumber = "QA-P-1", BranchId = branchId
        };
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "QA Supplier", IsActive = true };
        var lab = new LabEntity { Id = Guid.NewGuid(), Name = "QA Lab", SupplierId = supplier.Id, IsActive = true };
        var doctor = new Doctor { Id = Guid.NewGuid(), Name = "QA Doctor", BranchId = branchId, IsActive = true };
        db.AddRange(patient, supplier, lab, doctor);

        AddPostedLine(db, patient.Id, JournalAccountType.PatientReceivable,
            branchId, userId, "YER", 10_000m, 0m, "INV-YER");
        AddPostedLine(db, patient.Id, JournalAccountType.PatientAdvance,
            branchId, userId, "USD", 0m, 50m, "ADV-USD");
        AddPostedLine(db, supplier.Id, JournalAccountType.AccountsPayable,
            branchId, userId, "SAR", 0m, 400m, "BILL-SAR");
        // Lab payments are historically posted against LabId while their accrual is
        // posted against the linked SupplierId. The lab statement must include both.
        AddPostedLine(db, lab.Id, JournalAccountType.Payable,
            branchId, userId, "SAR", 100m, 0m, "LAB-PAY-SAR");

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, Patient = patient,
            InvoiceNumber = "INV-DOC-USD", Currency = "USD", Status = InvoiceStatus.Issued,
            TotalAmount = 100m, Subtotal = 100m, CreatedBy = userId, IsActive = true
        };
        db.Invoices.Add(invoice);
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(), InvoiceId = invoice.Id, Invoice = invoice,
            Description = "QA Service", ServiceNameSnapshot = "QA Service",
            Quantity = 1, UnitPrice = 100m, TotalPrice = 100m,
            DoctorId = doctor.Id, Doctor = doctor, DoctorCommissionAmount = 30m,
            CommissionStatus = CommissionStatus.Approved,
            CommissionApprovedAt = DateTime.UtcNow, IsActive = true
        });
        db.DoctorCommissionPayments.Add(new DoctorCommissionPayment
        {
            Id = Guid.NewGuid(), DoctorId = doctor.Id, Doctor = doctor,
            BranchId = branchId, Currency = "YER", IdempotencyKey = "QA-DOC-PAY",
            Amount = 5_000m, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new PartyAccountStatementService(db);
        var patientStatement = await service.GetAsync("patient", patient.Id, branchId, null);
        patientStatement!.Currencies.Select(x => x.Currency).Should().BeEquivalentTo(["YER", "USD"]);
        patientStatement.Currencies.Single(x => x.Currency == "YER").BalanceNature.Should().Be("Debit");
        patientStatement.Currencies.Single(x => x.Currency == "USD").BalanceNature.Should().Be("Credit");

        var supplierStatement = await service.GetAsync("supplier", supplier.Id, branchId, null);
        supplierStatement!.Currencies.Single().Currency.Should().Be("SAR");
        supplierStatement.Currencies.Single().BalanceNature.Should().Be("Credit");
        supplierStatement.Currencies.Single().TotalDebit.Should().Be(100m);

        var labStatement = await service.GetAsync("lab", lab.Id, branchId, null);
        labStatement!.PartyName.Should().Be("QA Lab");
        labStatement.Currencies.Single().TotalCredit.Should().Be(400m);
        labStatement.Currencies.Single().TotalDebit.Should().Be(100m);
        labStatement.Currencies.Single().ClosingBalance.Should().Be(300m);

        var doctorStatement = await service.GetAsync("doctor", doctor.Id, branchId, null);
        doctorStatement!.Currencies.Select(x => x.Currency).Should().BeEquivalentTo(["YER", "USD"]);
        doctorStatement.Currencies.Single(x => x.Currency == "USD").TotalCredit.Should().Be(30m);
        doctorStatement.Currencies.Single(x => x.Currency == "YER").TotalDebit.Should().Be(5_000m);
    }

    [Fact]
    public async Task BranchScopedStatement_DoesNotRevealAnotherBranchPatientOrDoctor()
    {
        await using var db = CreateContext();
        var ownerBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var patient = new Patient
        {
            Id = Guid.NewGuid(), PatientNumber = "QA-PRIVATE",
            FirstName = "Private", LastName = "Patient", BranchId = ownerBranchId
        };
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(), Name = "Private Doctor", BranchId = ownerBranchId, IsActive = true
        };
        db.AddRange(patient, doctor);
        await db.SaveChangesAsync();

        var service = new PartyAccountStatementService(db);
        (await service.GetAsync("patient", patient.Id, otherBranchId, null)).Should().BeNull();
        (await service.GetAsync("doctor", doctor.Id, otherBranchId, null)).Should().BeNull();
    }

    private static void AddPostedLine(
        AppDbContext db, Guid accountId, JournalAccountType accountType,
        Guid branchId, Guid userId, string currency, decimal debit, decimal credit,
        string entryNumber)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), EntryNumber = entryNumber,
            FinancialDocumentId = Guid.NewGuid(),
            FinancialDocumentType = FinancialDocumentType.Invoice,
            Description = entryNumber, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = currency, ExchangeRateToYer = 1m, BranchId = branchId,
            PerformedBy = userId, IsPosted = true, PostedAt = DateTime.UtcNow, IsActive = true
        };
        var counterAccount = new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntry = entry, JournalEntryId = entry.Id,
            AccountType = JournalAccountType.Revenue, AccountId = Guid.NewGuid(),
            Debit = credit, Credit = debit, BranchId = branchId, IsActive = true
        };
        var partyLine = new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntry = entry, JournalEntryId = entry.Id,
            AccountType = accountType, AccountId = accountId,
            Debit = debit, Credit = credit, BranchId = branchId,
            Description = entryNumber, IsActive = true
        };
        entry.Lines.Add(partyLine);
        entry.Lines.Add(counterAccount);
        db.JournalEntries.Add(entry);
    }
}
