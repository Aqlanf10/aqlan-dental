using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Settings;

/// <summary>
/// CORE-REQ-006 — the branding endpoint serves the identity a printed form needs, from
/// Settings.
///
/// <para>
/// The English identity keys already reached the response — the query filtered on the
/// <c>website</c> category, which they carry. What did not reach it was the Arabic lead-doctor
/// block, because it lives in <c>clinic.*</c> under a different category, and the declared key
/// list the query was supposed to use had never actually been applied.
/// </para>
/// </summary>
public class PrintLanguageSettingTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Dictionary<string, string>> GetBrandingAsync(AppDbContext db)
    {
        var result = await new PublicController(db).GetWebsiteSettings();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return (Dictionary<string, string>)ok.Value!;
    }

    private static async Task SetAsync(AppDbContext db, params (string Key, string Value)[] rows)
    {
        foreach (var (key, value) in rows)
            db.Settings.Add(new Setting { Key = key, Value = value, Category = key.Split('.')[0] });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_configured_English_identity_actually_reaches_the_response()
    {
        using var db = CreateDb();
        await SetAsync(db,
            ("website.clinicNameEn", "Configured English Name"),
            ("website.addressEn", "Configured English Address"),
            ("website.leadDoctorEn", "Configured Doctor"),
            ("website.leadDoctorCredentialsEn", "Configured Credentials"));

        var branding = await GetBrandingAsync(db);

        branding["clinicNameEn"].Should().Be("Configured English Name",
            "a configured English identity must reach the form that prints it");
        branding["addressEn"].Should().Be("Configured English Address");
        branding["leadDoctorEn"].Should().Be("Configured Doctor");
        branding["leadDoctorCredentialsEn"].Should().Be("Configured Credentials");
    }

    /// <summary>
    /// Reversed deliberately, with the reason recorded rather than the assertion just flipped.
    ///
    /// <para>
    /// This used to assert English, on the grounds that "the patient-carried forms have always
    /// printed English". That was true of two browser-printed forms — prescriptions and
    /// radiology orders — and their English identity came from hardcoded literals, not from a
    /// decision; CORE-REQ-006 moved those literals into Settings precisely so they stopped
    /// being accidental.
    /// </para>
    ///
    /// <para>
    /// The setting now also governs the server-generated PDFs (receipts, statements,
    /// invoices), which have always printed Arabic. One value cannot preserve both histories,
    /// and for a clinic in Taiz whose patients read Arabic, Arabic is the right default for a
    /// prescription. English stays one setting away.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Print_language_defaults_to_Arabic()
    {
        using var db = CreateDb();

        (await GetBrandingAsync(db))["printLanguage"].Should().Be("ar",
            "an unset print language must mean the language this clinic's documents are in");
    }

    [Fact]
    public async Task Print_language_follows_the_setting()
    {
        using var db = CreateDb();
        await SetAsync(db, ("website.printLanguage", "ar"));

        (await GetBrandingAsync(db))["printLanguage"].Should().Be("ar");
    }

    [Theory]
    // A recognised value survives whitespace and case.
    [InlineData("  EN  ", "en")]
    [InlineData("AR", "ar")]
    // Anything unusable falls back to the clinic's own language, not to English.
    [InlineData("fr", "ar")]
    [InlineData("", "ar")]
    public async Task An_unusable_language_value_resolves_to_a_language_that_renders(
        string configured, string expected)
    {
        using var db = CreateDb();
        await SetAsync(db, ("website.printLanguage", configured));

        (await GetBrandingAsync(db))["printLanguage"].Should().Be(expected,
            "a typo in a settings row must not make a printed medical form render in no language, "
            + "and the language it falls back to is the one this clinic prints in");
    }

    /// <summary>
    /// The Arabic lead doctor is read from clinic.* — the same rows the PDFs use — so a printed
    /// Arabic form and a printed Arabic PDF cannot name the doctor differently.
    /// </summary>
    [Fact]
    public async Task The_Arabic_lead_doctor_comes_from_the_same_rows_the_PDFs_read()
    {
        using var db = CreateDb();
        await SetAsync(db,
            ("clinic.lead_doctor", "د. عقلان الكامل"),
            ("clinic.lead_doctor_title", "أخصائي تقويم الأسنان"),
            ("clinic.lead_doctor_credentials", "جامعة مانيلا المركزية — الفلبين"));

        var branding = await GetBrandingAsync(db);

        branding["leadDoctorAr"].Should().Be("د. عقلان الكامل — أخصائي تقويم الأسنان");
        branding["leadDoctorCredentialsAr"].Should().Be("جامعة مانيلا المركزية — الفلبين");
    }

    [Fact]
    public async Task A_lead_doctor_with_no_title_is_not_printed_with_a_dangling_dash()
    {
        using var db = CreateDb();
        await SetAsync(db, ("clinic.lead_doctor", "د. عقلان الكامل"));

        (await GetBrandingAsync(db))["leadDoctorAr"].Should().Be("د. عقلان الكامل");
    }

    [Fact]
    public async Task No_configured_lead_doctor_yields_an_empty_string_not_a_stray_separator()
    {
        using var db = CreateDb();

        (await GetBrandingAsync(db))["leadDoctorAr"].Should().Be("");
    }
}
