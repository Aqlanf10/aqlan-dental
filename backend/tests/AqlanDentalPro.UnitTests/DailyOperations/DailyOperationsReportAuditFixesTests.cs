using System.Text.Json;
using AqlanDentalPro.API.Controllers;
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

namespace AqlanDentalPro.UnitTests.DailyOperations;

/// <summary>
/// Daily Operations audit (2026-07-29) regression tests for GetDailyReport's clinic-day
/// boundary and multi-currency handling.
/// </summary>
public class DailyOperationsReportAuditFixesTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DailyOperationsController BuildController(AppDbContext db) =>
        new(db, new Mock<ILogger<DailyOperationsController>>().Object);

    private static JsonElement ToJsonElement(object value) =>
        JsonSerializer.SerializeToElement(value, value.GetType());

    [Fact]
    public async Task GetDailyReport_InvoiceCreatedJustAfterLocalMidnight_CountsTowardCorrectClinicDay()
    {
        // Asia/Aden is UTC+3 with no DST. 01:00 local on `reportDate` is 22:00 UTC on the
        // PREVIOUS calendar day. Before the fix, the controller treated clinic-local midnight
        // as if it were UTC midnight, so this invoice would have been attributed to the
        // previous day's report instead of `reportDate`.
        await using var db = CreateDb();

        var reportDate = new DateOnly(2026, 8, 1);
        var localOneAm = reportDate.ToDateTime(new TimeOnly(1, 0));
        var utcCreatedAt = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localOneAm, DateTimeKind.Unspecified),
            ClinicTimeProvider.ResolveTimeZone("Asia/Aden"));

        var patient = new Patient { PatientNumber = "GM-TZ-001", FirstName = "مريض", LastName = "اختبار" };
        var invoice = new Invoice
        {
            PatientId = patient.Id,
            InvoiceNumber = "TZ-TEST-001",
            Status = InvoiceStatus.Issued,
            Subtotal = 7000,
            TotalAmount = 7000,
            IsActive = true
        };

        db.AddRange(patient, invoice);
        await db.SaveChangesAsync();

        // AppDbContext.SaveChangesAsync stamps CreatedAt with DateTime.UtcNow for newly-Added
        // entities, overriding any value set before the first save. Set the desired timestamp
        // AFTER the insert and save again — the override only touches Added-state entities, so
        // this second (Modified-state) save leaves CreatedAt exactly as set here.
        invoice.CreatedAt = DateTime.SpecifyKind(utcCreatedAt, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetDailyReport(reportDate.ToString("yyyy-MM-dd"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = ToJsonElement(ok.Value!);
        var draftAndDebt = json.GetProperty("Financial").GetProperty("NewDebts").GetDecimal();

        draftAndDebt.Should().Be(7000m,
            "an invoice created at 01:00 clinic-local time on reportDate must be counted in reportDate's report, not the previous day's");
    }

    [Fact]
    public async Task GetDailyReport_MixedCurrencyPayments_DoesNotSumForeignCurrencyIntoYerTotal()
    {
        await using var db = CreateDb();

        var reportDate = ClinicTimeProvider.ClinicToday();
        var patient = new Patient { PatientNumber = "GM-CUR-001", FirstName = "مريض", LastName = "اختبار" };
        var yerPayment = new Payment
        {
            PatientId = patient.Id,
            Amount = 10000,
            Currency = "YER",
            PaymentMethod = "cash",
            PaymentDate = reportDate,
            IsActive = true
        };
        var usdPayment = new Payment
        {
            PatientId = patient.Id,
            Amount = 100,
            Currency = "USD",
            PaymentMethod = "cash",
            PaymentDate = reportDate,
            IsActive = true
        };

        db.AddRange(patient, yerPayment, usdPayment);
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetDailyReport(null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = ToJsonElement(ok.Value!);
        var financial = json.GetProperty("Financial");

        financial.GetProperty("TotalCollected").GetDecimal().Should().Be(10000m,
            "the YER headline total must not silently absorb the 100 USD payment as if it were 100 YER");

        var foreign = financial.GetProperty("ForeignCurrencyCollections").EnumerateArray().ToList();
        foreign.Should().ContainSingle(f => f.GetProperty("Currency").GetString() == "USD"
            && f.GetProperty("Amount").GetDecimal() == 100m);

        json.GetProperty("PartialDataWarning").GetString().Should().NotBeNullOrEmpty(
            "staff must be told that foreign-currency collections were excluded from the headline total");
    }
}
