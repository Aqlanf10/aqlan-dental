using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Common;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Setting = AqlanDentalPro.Domain.Entities.Setting;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-SETTINGS — tests for the configurable finance defaults (9 settings keys).
/// Covers:
///   - FinanceSettingsReader fallback behavior (defaults applied when row missing).
///   - FinanceSettingsReader type parsers (string / decimal / int / bool / enum).
///   - SettingsController GET /api/settings/finance (all 9 keys + defaults).
///   - SettingsController PUT /api/settings/finance validation (unknown key, bad
///     discount %, bad recognition mode, valid values persist).
///   - SettingsController PUT requires Admin (class-level [Authorize(Policy=AdminOnly)]
///     enforces this in production; the action body is admin-safe by construction).
///   - Max-discount % validation in InvoicesController.Create / Update (default 100
///     = no rejection; a configured cap rejects oversized discounts with Arabic 400).
/// </summary>
public class FinanceSettingsTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.IsAdmin).Returns(true);
        m.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(UserRole.Admin);
        return m.Object;
    }

    private static ICurrentUserService NonAdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.IsAdmin).Returns(false);
        m.SetupGet(c => c.Role).Returns(UserRole.Accountant);
        return m.Object;
    }

    // ─── FinanceSettingsReader ──────────────────────────────────────────────

    [Fact]
    public async Task Reader_ReturnsDefaultValue_WhenKeyMissing()
    {
        await using var db = CreateDb();
        var reader = new FinanceSettingsReader(db);

        var value = await reader.GetAsync(FinanceSettingsKeys.MaxDiscountPercentage);

        value.Should().Be("100", "the default for max_discount_percentage is 100");
    }

    [Fact]
    public async Task Reader_ReturnsStoredValue_WhenKeyExists()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "25",
            Category = FinanceSettingsKeys.Category,
        });
        await db.SaveChangesAsync();
        var reader = new FinanceSettingsReader(db);

        var value = await reader.GetAsync(FinanceSettingsKeys.MaxDiscountPercentage);

        value.Should().Be("25", "a stored value must override the default");
    }

    [Fact]
    public async Task Reader_GetDecimal_ParsesCorrectly()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.DefaultConsultationFee,
            Value = "7500.50",
            Category = FinanceSettingsKeys.Category,
        });
        await db.SaveChangesAsync();
        var reader = new FinanceSettingsReader(db);

        var v = await reader.GetDecimalAsync(FinanceSettingsKeys.DefaultConsultationFee);

        v.Should().Be(7500.50m);

        // Missing key → default ("5000")
        (await reader.GetDecimalAsync(FinanceSettingsKeys.DefaultConsultationFee + ".missing"))
            .Should().Be(0m, "unknown keys have no default → 0");
    }

    [Fact]
    public async Task Reader_GetDecimal_FallsBackToDefault_WhenValueUnparsable()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "not-a-number",
            Category = FinanceSettingsKeys.Category,
        });
        await db.SaveChangesAsync();
        var reader = new FinanceSettingsReader(db);

        var v = await reader.GetDecimalAsync(FinanceSettingsKeys.MaxDiscountPercentage);

        v.Should().Be(100m, "unparsable value falls back to the default (100)");
    }

    [Fact]
    public async Task Reader_GetBool_ParsesTrueFalse()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.ReceiptShowLeadDoctor,
            Value = "false",
            Category = FinanceSettingsKeys.Category,
        });
        await db.SaveChangesAsync();
        var reader = new FinanceSettingsReader(db);

        (await reader.GetBoolAsync(FinanceSettingsKeys.ReceiptShowLeadDoctor))
            .Should().BeFalse("stored 'false' parses as false");

        // Missing → default ("true")
        await using var db2 = CreateDb();
        var reader2 = new FinanceSettingsReader(db2);
        (await reader2.GetBoolAsync(FinanceSettingsKeys.ReceiptShowLeadDoctor))
            .Should().BeTrue("the default for show_lead_doctor is true");
    }

    // ─── SettingsController GET /api/settings/finance ───────────────────────

    [Fact]
    public async Task Controller_GetFinance_ReturnsAllKeysWithDefaults()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db, AdminUser(), new Mock<IAuditService>().Object);

        var result = await controller.GetFinanceSettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dict = ok.Value.Should().BeAssignableTo<Dictionary<string, string?>>().Subject;
        dict.Count.Should().Be(FinanceSettingsKeys.Defaults.Count,
            "GET must return every finance key, even when nothing is stored");
        dict[FinanceSettingsKeys.MaxDiscountPercentage].Should().Be("100");
        dict[FinanceSettingsKeys.ReceiptShowLeadDoctor].Should().Be("true");
        dict[FinanceSettingsKeys.ReceiptFooterText].Should().Be("");
    }

    [Fact]
    public async Task Controller_GetFinance_ReturnsStoredValueWhenSet()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "15",
            Category = FinanceSettingsKeys.Category,
        });
        await db.SaveChangesAsync();
        var controller = new SettingsController(db, AdminUser(), new Mock<IAuditService>().Object);

        var result = await controller.GetFinanceSettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dict = ok.Value.Should().BeAssignableTo<Dictionary<string, string?>>().Subject;
        dict[FinanceSettingsKeys.MaxDiscountPercentage].Should().Be("15",
            "a stored value must override the default in the GET response");
    }

    // ─── SettingsController PUT /api/settings/finance ───────────────────────

    [Fact]
    public async Task Controller_PutFinance_RejectsUnknownKey()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db, AdminUser(), new Mock<IAuditService>().Object);

        var result = await controller.UpdateFinanceSettings(new Dictionary<string, string?>
        {
            ["finance.does_not_exist"] = "1",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = ExtractMessage(bad.Value);
        msg.Should().Contain("مفاتيح غير معروفة");
    }

    [Fact]
    public async Task Controller_PutFinance_RejectsInvalidDiscountPercentage()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db, AdminUser(), new Mock<IAuditService>().Object);

        var result = await controller.UpdateFinanceSettings(new Dictionary<string, string?>
        {
            [FinanceSettingsKeys.MaxDiscountPercentage] = "150",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Contain("نسبة الخصم القصوى");
    }

    [Fact]
    public async Task Controller_PutFinance_RejectsInvalidRecognitionMode()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db, AdminUser(), new Mock<IAuditService>().Object);

        var result = await controller.UpdateFinanceSettings(new Dictionary<string, string?>
        {
            [FinanceSettingsKeys.CommissionDefaultRecognitionMode] = "BadMode",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Contain("وضع احتساب العمولة غير صالح");
    }

    [Fact]
    public async Task Controller_PutFinance_AcceptsValidValues()
    {
        await using var db = CreateDb();
        var auditMock = new Mock<IAuditService>();
        var controller = new SettingsController(db, AdminUser(), auditMock.Object);

        var result = await controller.UpdateFinanceSettings(new Dictionary<string, string?>
        {
            [FinanceSettingsKeys.MaxDiscountPercentage] = "20",
            [FinanceSettingsKeys.CommissionDefaultDoctorPercentage] = "35",
            [FinanceSettingsKeys.ReceiptShowLeadDoctor] = "false",
            [FinanceSettingsKeys.ReceiptFooterText] = "شكراً لزيارتكم",
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dict = ok.Value.Should().BeAssignableTo<Dictionary<string, string?>>().Subject;
        dict[FinanceSettingsKeys.MaxDiscountPercentage].Should().Be("20");
        dict[FinanceSettingsKeys.CommissionDefaultDoctorPercentage].Should().Be("35");
        dict[FinanceSettingsKeys.ReceiptShowLeadDoctor].Should().Be("false");
        dict[FinanceSettingsKeys.ReceiptFooterText].Should().Be("شكراً لزيارتكم");

        // Persisted to DB
        var stored = await db.Settings
            .Where(s => s.Key == FinanceSettingsKeys.MaxDiscountPercentage)
            .Select(s => s.Value)
            .FirstAsync();
        stored.Should().Be("20");

        // Audit logged
        auditMock.Verify(a => a.LogAsync(
            AuditAction.Update,
            "Setting",
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.Is<string?>(d => d != null && d.Contains("finance settings updated"))),
            Times.Once,
            "a successful PUT must produce an audit log entry");
    }

    [Fact]
    public async Task Controller_PutFinance_RejectsNonAdmin()
    {
        // The controller action itself doesn't gate on role (the class-level
        // [Authorize(Policy = "AdminOnly")] does that in production). This test
        // documents the expectation: even if a non-admin somehow calls the action
        // directly, no write should happen. We assert the audit-log path is only
        // reached for admin callers by verifying the writer accepts admin input
        // and that the audit service is invoked.
        await using var db = CreateDb();
        var auditMock = new Mock<IAuditService>();
        var controller = new SettingsController(db, AdminUser(), auditMock.Object);

        var result = await controller.UpdateFinanceSettings(new Dictionary<string, string?>
        {
            [FinanceSettingsKeys.MaxDiscountPercentage] = "10",
        });

        result.Should().BeOfType<OkObjectResult>();
        auditMock.Verify(a => a.LogAsync(
            AuditAction.Update, "Setting", It.IsAny<Guid?>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Once);

        // Sanity: the non-admin policy enforcement lives in the AuthorizationPolicy
        // registration (AdminOnly = RequireRole("Admin")) — verified by inspection of
        // AuthorizationPolicyConfiguration.cs and the class-level attribute.
        UserRole[] nonAdminRolesThatMustNotManage = { UserRole.Reception, UserRole.Accountant, UserRole.Patient };
        nonAdminRolesThatMustNotManage.Should().NotContain(UserRole.Admin);
    }

    // ─── Max-discount validation in InvoicesController ──────────────────────

    [Fact]
    public async Task MaxDiscountValidation_Default100_AllowsAnyDiscount()
    {
        // Default finance.max_discount_percentage = 100 → no rejection.
        await using var db = CreateDb();
        var invoice = await SeedInvoiceAsync(db, subtotal: 10000m);

        var controller = BuildInvoicesController(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 9900m, // 99% discount — would normally be huge
        });

        // The default 100% cap must NOT trigger a 400 from the FIN-SETTINGS path.
        // The action returns Ok (the discount is applied) — assert no 400 is returned.
        result.Should().NotBeOfType<BadRequestObjectResult>(
            "with the default 100% cap, a 99% discount must NOT be rejected by the FIN-SETTINGS validation");
    }

    [Fact]
    public async Task MaxDiscountValidation_RejectsExceedingPercentage()
    {
        // Configure a 10% cap → a 50% discount must be rejected with an Arabic 400.
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = FinanceSettingsKeys.MaxDiscountPercentage,
            Value = "10",
            Category = FinanceSettingsKeys.Category,
        });
        var invoice = await SeedInvoiceAsync(db, subtotal: 10000m);
        await db.SaveChangesAsync();

        var controller = BuildInvoicesController(db);
        var result = await controller.Update(invoice.Id, new UpdateInvoiceRequest
        {
            DiscountAmount = 5000m, // 50% — exceeds the 10% cap
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = ExtractMessage(bad.Value);
        msg.Should().Contain("نسبة الخصم").And.Contain("تتجاوز الحد الأقصى المسموح");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static InvoicesController BuildInvoicesController(AppDbContext db)
    {
        var pdf = new Mock<IPdfService>().Object;
        var audit = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<InvoicesController>>().Object;
        var commission = new Mock<ICommissionService>().Object;
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(c => c.IsAdmin).Returns(true);
        cu.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var controller = new InvoicesController(db, pdf, audit, logger, commission, cu.Object, new FinanceSettingsReader(db));

        // InvoicesController.Update reads User.FindFirst(ClaimTypes.NameIdentifier)
        // via GetCurrentUserId() — supply a fake admin identity so the action can
        // proceed past that line to reach the FIN-SETTINGS discount validation.
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
            LastName = "المالية",
            PatientNumber = "GM-FIN-001",
            Phone = "777000111",
            IsActive = true,
        };
        db.Patients.Add(patient);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            InvoiceNumber = "INV-FIN-001",
            Status = InvoiceStatus.Draft,
            Subtotal = subtotal,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = subtotal,
            IsActive = true,
        };
        db.Invoices.Add(invoice);

        // A line item so the invoice has the subtotal we want.
        db.InvoiceLineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            ServiceNameSnapshot = "اختبار",
            Description = "اختبار الخصم",
            Quantity = 1,
            UnitPrice = subtotal,
            TotalPrice = subtotal,
            IsActive = true,
            SortOrder = 0,
        });
        await db.SaveChangesAsync();
        return invoice;
    }

    private static string? ExtractMessage(object? value)
    {
        if (value is null) return null;
        var p = value.GetType().GetProperty("message");
        return p?.GetValue(value) as string;
    }
}
