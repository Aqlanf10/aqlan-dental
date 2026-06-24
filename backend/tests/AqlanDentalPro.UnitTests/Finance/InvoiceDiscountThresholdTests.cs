using AqlanDentalPro.Application.Common;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-11: invoice discount approval threshold. Sprint 1 wired the configurable
/// finance.max_discount_percentage setting (default 100 = no rejection) into
/// InvoicesController.Create and Update. This test class verifies the behavior:
///   - Default cap (100) → any discount ≤ subtotal is allowed (no rejection).
///   - Owner lowers cap below 100 → discounts exceeding the cap are rejected with
///     an Arabic message: "نسبة الخصم (...) تتجاوز الحد الأقصى المسموح (...)".
/// A separate two-step approval workflow (like OperationalExpenses) is intentionally
/// NOT added in this increment — the threshold rejection is sufficient for FIN-11.
/// (A two-step workflow is a documented follow-up if the owner wants it.)
/// </summary>
public class InvoiceDiscountThresholdTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService AdminUser()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(u => u.Role).Returns(UserRole.Admin);
        mock.SetupGet(u => u.IsAdmin).Returns(true);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.BranchId).Returns((Guid?)null);
        return mock.Object;
    }

    private static InvoicesController Build(AppDbContext db)
    {
        var pdf = new Mock<IPdfService>();
        var audit = new Mock<IAuditService>();
        var commission = new Mock<ICommissionService>();
        var controller = new InvoicesController(
            db, pdf.Object, audit.Object,
            NullLogger<InvoicesController>.Instance,
            commission.Object, AdminUser(), new FinanceSettingsReader(db));

        // InvoicesController.Update reads User.FindFirst(ClaimTypes.NameIdentifier)
        // via GetCurrentUserId() — supply a fake admin identity so the action can
        // proceed past that line to reach the FIN-11 discount validation.
        var adminId = Guid.NewGuid();
        var identity = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, nameof(UserRole.Admin)),
        }, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) },
        };
        return controller;
    }

    private static async Task<Invoice> SeedInvoiceAsync(AppDbContext db, decimal subtotal)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "اختبار",
            LastName = "الخصم",
            PatientNumber = "GM-FIN11-001",
            Phone = "777000111",
            IsActive = true,
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-FIN11-001",
            Status = InvoiceStatus.Draft,
            Subtotal = subtotal,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = subtotal,
            IsActive = true,
        };
        db.Invoices.Add(invoice);
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            ServiceNameSnapshot = "خدمة علاجية",
            Quantity = 1,
            UnitPrice = subtotal,
            TotalPrice = subtotal,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return invoice;
    }

    private static string? ExtractMessage(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task DefaultCap100_AllowsHalfDiscount()
    {
        // Default finance.max_discount_percentage = 100 → no rejection.
        // A 50% discount must be accepted (OkObjectResult), not rejected.
        await using var db = CreateDb();
        var invoice = await SeedInvoiceAsync(db, subtotal: 10_000m);

        var controller = Build(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 5_000m, // 50% discount
        });

        result.Should().NotBeOfType<BadRequestObjectResult>(
            "with the default 100% cap, a 50% discount must NOT be rejected by the FIN-11 validation");
    }

    [Fact]
    public async Task DefaultCap100_AllowsLargeDiscount()
    {
        // Default cap = 100 → even a 99% discount is allowed (no rejection).
        await using var db = CreateDb();
        var invoice = await SeedInvoiceAsync(db, subtotal: 10_000m);

        var controller = Build(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 9_900m, // 99% discount
        });

        result.Should().NotBeOfType<BadRequestObjectResult>(
            "with the default 100% cap, even a 99% discount is allowed (no rejection until the owner lowers the cap)");
    }

    [Fact]
    public async Task OwnerLowersCapTo10_RejectsFiftyPercentDiscount_WithArabicMessage()
    {
        // Owner sets finance.max_discount_percentage = 10. A 50% discount must now
        // be rejected with the Arabic FIN-11 message.
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "10",
            Category = FinanceSettingsKeys.Category,
        });
        var invoice = await SeedInvoiceAsync(db, subtotal: 10_000m);
        await db.SaveChangesAsync();

        var controller = Build(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 5_000m, // 50% — exceeds the 10% cap
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = ExtractMessage(bad);
        msg.Should().NotBeNullOrEmpty("FIN-11 must reject with an Arabic message");
        msg.Should().Contain("نسبة الخصم", "message must mention the discount percentage");
        msg.Should().Contain("تتجاوز الحد الأقصى المسموح", "message must mention the max allowed");
    }

    [Fact]
    public async Task OwnerLowersCapTo10_AllowsFivePercentDiscount()
    {
        // Owner sets the cap to 10%. A 5% discount is within the cap → accepted.
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "10",
            Category = FinanceSettingsKeys.Category,
        });
        var invoice = await SeedInvoiceAsync(db, subtotal: 10_000m);
        await db.SaveChangesAsync();

        var controller = Build(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 500m, // 5% — within the 10% cap
        });

        result.Should().NotBeOfType<BadRequestObjectResult>(
            "a 5% discount is within the 10% cap and must be accepted");
    }

    [Fact]
    public async Task OwnerLowersCapTo10_RejectsDiscountAtExactlyCapBoundary_WhenStrictlyAbove()
    {
        // 10% cap, 11% discount → strictly above → rejected.
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "10",
            Category = FinanceSettingsKeys.Category,
        });
        var invoice = await SeedInvoiceAsync(db, subtotal: 10_000m);
        await db.SaveChangesAsync();

        var controller = Build(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 1_100m, // 11% — strictly above the 10% cap
        });

        result.Should().BeOfType<BadRequestObjectResult>(
            "11% is strictly above the 10% cap and must be rejected");
    }
}
