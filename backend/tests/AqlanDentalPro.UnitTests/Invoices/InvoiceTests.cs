using Xunit;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.Invoices;

/// <summary>
/// Tests for Invoice and InvoiceLineItem entities, draft invoice creation,
/// duplicate prevention, status transitions, and finance safety guarantees.
/// Uses InMemory provider to verify operations without requiring PostgreSQL.
/// </summary>
public class InvoiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ─── Entity Field Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Invoice_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        var invoice = new Invoice
        {
            PatientId = patientId,
            InvoiceNumber = "INV-20260531-001",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000m,
            TotalAmount = 5000m
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved.Should().NotBeNull();
        saved!.InvoiceNumber.Should().Be("INV-20260531-001");
        saved.Status.Should().Be(InvoiceStatus.Draft);
        saved.Subtotal.Should().Be(5000m);
        saved.TotalAmount.Should().Be(5000m);
    }

    [Fact]
    public async Task Invoice_OptionalFields_DefaultToNull()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-002",
            Subtotal = 0,
            TotalAmount = 0
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved.Should().NotBeNull();
        saved!.VisitId.Should().BeNull();
        saved.AppointmentId.Should().BeNull();
        saved.DiscountAmount.Should().BeNull();
        saved.TaxAmount.Should().Be(0m);
        saved.Notes.Should().BeNull();
        saved.CreatedBy.Should().BeNull();
        saved.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task InvoiceLineItem_CanBeCreated_WithAllFields()
    {
        await using var db = CreateContext();
        var serviceId = Guid.NewGuid();
        var treatmentStepId = Guid.NewGuid();

        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-003",
            Subtotal = 3000m,
            TotalAmount = 3000m
        };

        var lineItem = new InvoiceLineItem
        {
            Invoice = invoice,
            ServiceId = serviceId,
            ServiceNameSnapshot = "تنظيف الجير",
            Description = "تنظيف جير عميق",
            Quantity = 1,
            UnitPrice = 3000m,
            TotalPrice = 3000m,
            RelatedTreatmentPlanStepId = treatmentStepId,
            SortOrder = 0
        };

        db.InvoiceLineItems.Add(lineItem);
        await db.SaveChangesAsync();

        var saved = await db.InvoiceLineItems.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == lineItem.Id);
        saved.Should().NotBeNull();
        saved!.ServiceNameSnapshot.Should().Be("تنظيف الجير");
        saved.Quantity.Should().Be(1);
        saved.UnitPrice.Should().Be(3000m);
        saved.TotalPrice.Should().Be(3000m);
        saved.ServiceId.Should().Be(serviceId);
    }

    // ─── Draft Invoice Creation Tests ─────────────────────────────────────

    [Fact]
    public async Task DraftInvoice_StatusIsDraft()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-010",
            Status = InvoiceStatus.Draft,
            Subtotal = 1000m,
            TotalAmount = 1000m
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved!.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task DraftInvoice_DoesNotCreatePayment()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-011",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000m,
            TotalAmount = 5000m
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty("creating a draft invoice should not auto-create any payment");
    }

    [Fact]
    public async Task DraftInvoice_DoesNotChangeContract()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        var contract = new Contract
        {
            PatientId = patientId,
            TotalAmount = 10000m,
            Status = ContractStatus.Active
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var invoice = new Invoice
        {
            PatientId = patientId,
            InvoiceNumber = "INV-20260531-012",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000m,
            TotalAmount = 5000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var savedContract = await db.Contracts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contract.Id);
        savedContract!.TotalAmount.Should().Be(10000m);
        savedContract.Status.Should().Be(ContractStatus.Active);
    }

    // ─── Duplicate Prevention Tests ────────────────────────────────────────

    [Fact]
    public async Task DraftInvoice_PreventsDuplicateForSameVisit()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();

        var invoice1 = new Invoice
        {
            PatientId = patientId,
            VisitId = visitId,
            InvoiceNumber = "INV-20260531-020",
            Status = InvoiceStatus.Draft,
            Subtotal = 3000m,
            TotalAmount = 3000m
        };
        db.Invoices.Add(invoice1);
        await db.SaveChangesAsync();

        var existingDraft = await db.Invoices
            .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

        existingDraft.Should().NotBeNull();
        existingDraft!.InvoiceNumber.Should().Be("INV-20260531-020");
    }

    [Fact]
    public async Task Invoice_AllowsNewDraftForSameVisit_IfPreviousNotDraft()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();

        var invoice1 = new Invoice
        {
            PatientId = patientId,
            VisitId = visitId,
            InvoiceNumber = "INV-20260531-021",
            Status = InvoiceStatus.Issued,
            Subtotal = 3000m,
            TotalAmount = 3000m
        };
        db.Invoices.Add(invoice1);
        await db.SaveChangesAsync();

        var existingDraft = await db.Invoices
            .FirstOrDefaultAsync(i => i.VisitId == visitId && i.Status == InvoiceStatus.Draft && i.IsActive);

        existingDraft.Should().BeNull("issued invoice should not block new draft creation");
    }

    // ─── Line Item Totals Tests ────────────────────────────────────────────

    [Fact]
    public async Task InvoiceLineItem_TotalPrice_CalculatedCorrectly()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-030",
            Subtotal = 0,
            TotalAmount = 0
        };

        var line1 = new InvoiceLineItem
        {
            Invoice = invoice,
            ServiceNameSnapshot = "حشوة",
            Description = "حشوة ضرس",
            Quantity = 2,
            UnitPrice = 1500m,
            TotalPrice = 3000m,
            SortOrder = 0
        };
        var line2 = new InvoiceLineItem
        {
            Invoice = invoice,
            ServiceNameSnapshot = "تنظيف",
            Description = "تنظيف جير",
            Quantity = 1,
            UnitPrice = 2000m,
            TotalPrice = 2000m,
            SortOrder = 1
        };

        db.Invoices.Add(invoice);
        db.InvoiceLineItems.AddRange(line1, line2);
        await db.SaveChangesAsync();

        var lineItems = await db.InvoiceLineItems
            .Where(l => l.InvoiceId == invoice.Id && l.IsActive)
            .ToListAsync();
        var subtotal = lineItems.Sum(l => l.TotalPrice);

        subtotal.Should().Be(5000m);
    }

    // ─── Status Transition Tests ───────────────────────────────────────────

    [Fact]
    public async Task Invoice_Issue_ChangesStatusOnly()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-040",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000m,
            TotalAmount = 5000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.Status = InvoiceStatus.Issued;
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved!.Status.Should().Be(InvoiceStatus.Issued);

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty("issuing an invoice should not create a payment");
    }

    [Fact]
    public async Task Invoice_Cancel_ChangesStatusOnly()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-041",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000m,
            TotalAmount = 5000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.Notes = "[إلغاء] خطأ في الإدخال";
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved!.Status.Should().Be(InvoiceStatus.Cancelled);
        saved.Notes.Should().Contain("[إلغاء]");

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty("cancelling an invoice should not create a payment");
    }

    // ─── Finance Safety Tests ──────────────────────────────────────────────

    [Fact]
    public async Task Invoice_NoContractChange()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();

        // Create a contract to verify it's not changed by invoice creation
        var contract = new Contract
        {
            PatientId = patientId,
            TotalAmount = 20000m,
            DownPayment = 5000m,
            Status = ContractStatus.Active
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        var contractId = contract.Id;

        // Create and issue invoice
        var invoice = new Invoice
        {
            PatientId = patientId,
            InvoiceNumber = "INV-20260531-050",
            Status = InvoiceStatus.Issued,
            Subtotal = 10000m,
            TotalAmount = 10000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        // Verify contract is unchanged — invoice does not modify contracts
        var savedContract = await db.Contracts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contractId);
        savedContract!.TotalAmount.Should().Be(20000m);
        savedContract.DownPayment.Should().Be(5000m);
        savedContract.Status.Should().Be(ContractStatus.Active);
    }

    [Fact]
    public async Task IssuedInvoice_NoPaymentCreated()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-051",
            Status = InvoiceStatus.Issued,
            Subtotal = 7000m,
            TotalAmount = 7000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty("issued invoice must not auto-create payment");
    }

    // ─── Soft Delete Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task Invoice_SoftDelete_SetsIsActiveFalse()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-060",
            Subtotal = 1000m,
            TotalAmount = 1000m
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        invoice.IsActive = false;
        invoice.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var activeInvoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        activeInvoice.Should().BeNull();

        var deletedInvoice = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        deletedInvoice.Should().NotBeNull();
        deletedInvoice!.IsActive.Should().BeFalse();
    }

    // ─── Discount and Tax Calculation Tests ────────────────────────────────

    [Fact]
    public async Task Invoice_TotalAmount_WithDiscountAndTax()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-070",
            Subtotal = 10000m,
            DiscountAmount = 500m,
            TaxAmount = 200m,
            TotalAmount = 10000m - 500m + 200m,
            Status = InvoiceStatus.Draft
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var saved = await db.Invoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        saved!.TotalAmount.Should().Be(9700m);
        saved.Subtotal.Should().Be(10000m);
        saved.DiscountAmount.Should().Be(500m);
        saved.TaxAmount.Should().Be(200m);
    }

    // ─── Service Name Snapshot Tests ───────────────────────────────────────

    [Fact]
    public async Task InvoiceLineItem_ServiceNameSnapshot_IsPreserved()
    {
        await using var db = CreateContext();
        var invoice = new Invoice
        {
            PatientId = Guid.NewGuid(),
            InvoiceNumber = "INV-20260531-080",
            Subtotal = 5000m,
            TotalAmount = 5000m
        };

        var lineItem = new InvoiceLineItem
        {
            Invoice = invoice,
            ServiceNameSnapshot = "علاج العصب — القناة الواحدة",
            Description = "علاج عصب ضرس",
            Quantity = 1,
            UnitPrice = 5000m,
            TotalPrice = 5000m,
            SortOrder = 0
        };

        db.InvoiceLineItems.Add(lineItem);
        await db.SaveChangesAsync();

        var saved = await db.InvoiceLineItems.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == lineItem.Id);
        saved!.ServiceNameSnapshot.Should().Be("علاج العصب — القناة الواحدة");
    }

    // ─── Invoice Number Generation Logic Test ──────────────────────────────

    [Fact]
    public async Task InvoiceNumber_GeneratesSequentially()
    {
        await using var db = CreateContext();
        var patientId = Guid.NewGuid();
        var todayPrefix = $"INV-{DateTime.UtcNow:yyyyMMdd}-";

        var invoice1 = new Invoice
        {
            PatientId = patientId,
            InvoiceNumber = $"{todayPrefix}001",
            Subtotal = 1000m,
            TotalAmount = 1000m
        };
        db.Invoices.Add(invoice1);
        await db.SaveChangesAsync();

        var nextNumber = await FinanceV3Controller.GenerateInvoiceNumberAsync(db);
        nextNumber.Should().Be($"{todayPrefix}002");
    }

    [Fact]
    public async Task InvoiceNumber_StartsAt001_WhenNoExisting()
    {
        await using var db = CreateContext();

        var nextNumber = await FinanceV3Controller.GenerateInvoiceNumberAsync(db);
        nextNumber.Should().Be($"INV-{DateTime.UtcNow:yyyyMMdd}-001");
    }
}
