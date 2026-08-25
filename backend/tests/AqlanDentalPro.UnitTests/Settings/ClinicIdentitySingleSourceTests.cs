using System.Text.RegularExpressions;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Settings;

/// <summary>
/// CORE-REQ-006 — clinic identity comes from central settings through one resolver, not from
/// each generator's own read with its own fallback.
///
/// <para>
/// This was not theoretical. Three independent readers existed, and their fallbacks disagreed:
/// <c>FinanceClinicIdentity</c> fell back to the owner's real centre name, the lab work order
/// fell back to the generic "مركز طب الأسنان", and outbound SMS fell back to
/// "عيادة أقلان لطب الأسنان" — a name that is not the clinic's and misspells the owner's.
/// </para>
/// <para>
/// And the fallbacks were live. The <c>clinic.*</c> keys were seeded only by
/// <c>DbSeeder.SeedSettingsAsync</c>, which runs behind <c>if (!await Settings.AnyAsync())</c>
/// — a condition never true in practice, because the website and finance seeders insert rows
/// on any database first. Checked on two real databases: 62 settings rows, <c>website.*</c>
/// present, and <b>zero</b> <c>clinic.*</c>. So every document was printing a fallback, and a
/// different one each time.
/// </para>
/// </summary>
public class ClinicIdentitySingleSourceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the test must be able to locate the backend source tree");
        return dir!.FullName;
    }

    /// <summary>
    /// Files allowed to name a <c>clinic.*</c> key directly: the resolver itself, the seeders
    /// that create the rows, and the timezone key which is infrastructure rather than identity.
    /// </summary>
    private static readonly string[] Allowed =
    [
        "FinanceClinicIdentity.cs",
        "DbSeeder.cs",
        "StartupDatabaseMaintenance.cs",
        "ClinicTimeZoneInitializer.cs",
        "SettingsController.cs",
    ];

    /// <summary>
    /// The guard. A fourth reader is how the fallbacks diverged in the first place.
    /// </summary>
    [Fact]
    public void No_new_component_reads_a_clinic_identity_key_directly()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            if (Allowed.Contains(Path.GetFileName(file))) continue;

            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, "\"clinic\\.[a-z_]+\""))
                offenders.Add($"{Path.GetFileName(file)}: {m.Value}");
        }

        offenders.Should().BeEmpty(
            "clinic identity must be read through FinanceClinicIdentity.ResolveAsync so there is "
            + "one value and one fallback. Reading the key directly is how the lab work order "
            + "came to print a different clinic name than the receipts.");
    }

    /// <summary>
    /// The resolver's fallback is the owner's real centre name. Anything else printed on a
    /// document that leaves the building is wrong.
    /// </summary>
    [Fact]
    public async Task With_no_settings_at_all_the_resolver_still_gives_the_real_clinic_name()
    {
        using var db = CreateDb();

        var clinic = await FinanceClinicIdentity.ResolveAsync(db);

        clinic.Name.Should().Be(FinanceClinicIdentity.DefaultName);
        clinic.Name.Should().Contain("عقلان");
        clinic.Name.Should().NotBe("مركز طب الأسنان");
        clinic.Name.Should().NotContain("أقلان", "that spelling of the owner's name was the SMS fallback and is wrong");
    }

    [Fact]
    public async Task A_configured_name_wins_over_the_fallback()
    {
        using var db = CreateDb();
        db.Settings.Add(new Setting { Key = "clinic.name", Value = "اسم مختار من الإعدادات", Category = "clinic" });
        await db.SaveChangesAsync();

        (await FinanceClinicIdentity.ResolveAsync(db)).Name.Should().Be("اسم مختار من الإعدادات");
    }

    /// <summary>
    /// The owner's standing rule: reports carry the lead doctor, their title and their
    /// qualification, and all three come from Settings rather than from a literal.
    /// </summary>
    [Fact]
    public async Task The_lead_doctor_block_resolves_from_settings()
    {
        using var db = CreateDb();
        db.Settings.AddRange(
            new Setting { Key = "clinic.lead_doctor", Value = "د. عقلان الكامل", Category = "clinic" },
            new Setting { Key = "clinic.lead_doctor_title", Value = "أخصائي تقويم الأسنان", Category = "clinic" },
            new Setting { Key = "clinic.lead_doctor_credentials", Value = "جامعة مانيلا المركزية — الفلبين", Category = "clinic" });
        await db.SaveChangesAsync();

        var clinic = await FinanceClinicIdentity.ResolveAsync(db);

        clinic.HasLeadDoctor.Should().BeTrue();
        clinic.ShouldRenderLeadDoctor.Should().BeTrue();
        clinic.LeadDoctorTitle.Should().Be("أخصائي تقويم الأسنان");
        clinic.LeadDoctorCredentials.Should().Be("جامعة مانيلا المركزية — الفلبين");
    }

    /// <summary>
    /// The seeding defect itself: the clinic keys must be created additively, on a database
    /// that already has other settings. Seeding them only when the whole table is empty is
    /// what left production without them.
    /// </summary>
    [Fact]
    public void The_clinic_keys_are_seeded_additively_not_only_into_an_empty_table()
    {
        var startup = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Configuration", "StartupDatabaseMaintenance.cs"));

        // Assert the CALL, not the name. Checking only that the identifier appears somewhere
        // passes against a seeder that has been renamed or commented out of the startup path —
        // which is exactly what a sabotage run of this test proved before it was tightened.
        startup.Should().Contain("await EnsureClinicIdentitySettingsSeedAsync(app);",
            "the clinic identity keys need an additive seeder that is actually invoked at "
            + "startup; without one they are only created when the Settings table is completely "
            + "empty, which never happens once any other seeder has run");

        foreach (var key in new[]
                 {
                     "clinic.name", "clinic.location", "clinic.phones",
                     "clinic.lead_doctor", "clinic.lead_doctor_title", "clinic.lead_doctor_credentials",
                 })
        {
            startup.Should().Contain($"\"{key}\"", $"{key} must be part of the additive seed");
        }
    }
}
