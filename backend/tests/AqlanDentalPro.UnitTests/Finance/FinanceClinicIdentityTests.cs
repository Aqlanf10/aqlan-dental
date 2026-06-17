using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Finance PDFs (receipt voucher / statement / invoice) must read the clinic
/// identity from the Settings table (clinic.* keys) — no hardcoding, per the
/// owner's report-identity decision — and fall back to the legacy printed text
/// when keys are unset so existing documents never regress.
/// </summary>
public class FinanceClinicIdentityTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task ResolveAsync_ReadsClinicIdentityFromSettings()
    {
        await using var db = CreateDb();
        db.Settings.AddRange(
            new Setting { Key = "clinic.name", Value = "مركز تجريبي للأسنان" },
            new Setting { Key = "clinic.lead_doctor", Value = "د. عقلان الكامل" },
            new Setting { Key = "clinic.lead_doctor_title", Value = "أخصائي تقويم الأسنان" },
            new Setting { Key = "clinic.lead_doctor_credentials", Value = "جامعة مانيلا المركزية — الفلبين" },
            new Setting { Key = "clinic.phones", Value = "هاتف: 777123456" },
            new Setting { Key = "clinic.location", Value = "تعز — اليمن" });
        await db.SaveChangesAsync();

        var id = await FinanceClinicIdentity.ResolveAsync(db);

        id.Name.Should().Be("مركز تجريبي للأسنان");
        id.LeadDoctor.Should().Be("د. عقلان الكامل");
        id.LeadDoctorTitle.Should().Be("أخصائي تقويم الأسنان");
        id.LeadDoctorCredentials.Should().Be("جامعة مانيلا المركزية — الفلبين");
        id.Phones.Should().Be("هاتف: 777123456");
        id.Location.Should().Be("تعز — اليمن");
        id.HasLeadDoctor.Should().BeTrue();
        id.HasLeadDoctorCredentials.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_FallsBackWhenKeysUnset_AndHidesLeadDoctorBlock()
    {
        await using var db = CreateDb();

        var id = await FinanceClinicIdentity.ResolveAsync(db);

        id.Name.Should().Be(FinanceClinicIdentity.DefaultName);
        id.Location.Should().Be(FinanceClinicIdentity.DefaultLocation);
        id.Phones.Should().Be(FinanceClinicIdentity.DefaultPhones);
        id.HasLeadDoctor.Should().BeFalse("the lead-doctor block must not print when unconfigured");
    }

    [Fact]
    public void Fallback_UsesLegacyDefaults_NoLeadDoctor()
    {
        var id = FinanceClinicIdentity.Fallback;
        id.Name.Should().Be(FinanceClinicIdentity.DefaultName);
        id.ContactLine.Should().Contain(FinanceClinicIdentity.DefaultPhones)
            .And.Contain(FinanceClinicIdentity.DefaultLocation);
        id.HasLeadDoctor.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiptVoucher_RendersWithConfiguredIdentity_WithoutThrowing()
    {
        PdfService.EnsureFontsRegistered();
        await using var db = CreateDb();
        db.Settings.AddRange(
            new Setting { Key = "clinic.name", Value = "مركز الاختبار" },
            new Setting { Key = "clinic.lead_doctor", Value = "د. عقلان الكامل" },
            new Setting { Key = "clinic.lead_doctor_title", Value = "أخصائي تقويم الأسنان" });
        await db.SaveChangesAsync();
        var identity = await FinanceClinicIdentity.ResolveAsync(db);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = 5000m,
            ReceiptNumber = "RCP-20260610-002",
            PaymentDate = new DateOnly(2026, 6, 10),
            PaymentMethod = "cash",
            ServiceDescription = "دفعة تقويم",
        };

        byte[]? bytes = null;
        var ex = Record.Exception(() => { bytes = new PaymentReceiptDocument(payment, identity).GeneratePdf(); });

        ex.Should().BeNull("the receipt voucher must render with a Settings-driven identity");
        bytes.Should().NotBeNullOrEmpty();
        bytes!.Length.Should().BeGreaterThan(3000);
    }
}
