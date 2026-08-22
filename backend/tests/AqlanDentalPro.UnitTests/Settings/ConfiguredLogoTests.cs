using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Settings;

/// <summary>
/// CORE-REQ-006 — the logo on a printed document is the one the clinic configured.
///
/// <para>
/// The clinic can upload a logo at <c>/settings/website</c> and it appears on the public site
/// at once. Every PDF, though, kept printing <c>Fonts/logo.png</c> — the file compiled into the
/// deployment — because that was the only source the PDF cache knew, and it held it in a
/// <c>Lazy</c> that never re-reads. Re-uploading changed nothing; a restart changed nothing.
/// </para>
/// </summary>
[Collection("PdfLogo")]
public class ConfiguredLogoTests : IDisposable
{
    private readonly string _uploadsDir;
    private readonly string? _previousUploadsPath;

    public ConfiguredLogoTests()
    {
        _previousUploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        _uploadsDir = Path.Combine(Path.GetTempPath(), $"logo-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_uploadsDir);
        Environment.SetEnvironmentVariable("UPLOADS_PATH", _uploadsDir);
        PdfLogoCache.ResetConfiguredCache();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("UPLOADS_PATH", _previousUploadsPath);
        PdfLogoCache.ResetConfiguredCache();
        try { Directory.Delete(_uploadsDir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private string WriteUpload(string name, byte[] content)
    {
        File.WriteAllBytes(Path.Combine(_uploadsDir, name), content);
        return $"/uploads/{name}";
    }

    private static async Task SetLogoAsync(AppDbContext db, string url)
    {
        db.Settings.Add(new Setting { Key = "website.logoUrl", Value = url, Category = "website" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_configured_logo_is_what_the_document_gets()
    {
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        using var db = CreateDb();
        await SetLogoAsync(db, WriteUpload("clinic-logo.png", expected));

        var bytes = await PdfLogoCache.ResolveAsync(db);

        bytes.Should().Equal(expected, "the PDF must print the logo the clinic uploaded");
    }

    [Fact]
    public async Task With_no_configured_logo_the_shipped_file_is_used()
    {
        using var db = CreateDb();

        var bytes = await PdfLogoCache.ResolveAsync(db);

        bytes.Should().BeEquivalentTo(PdfLogoCache.LogoBytes,
            "an unconfigured clinic keeps the logo shipped with the build");
    }

    /// <summary>
    /// The behaviour the old <c>Lazy</c> could not provide: replacing the logo takes effect on
    /// the next document, without redeploying or restarting.
    /// </summary>
    [Fact]
    public async Task Replacing_the_logo_takes_effect_without_a_restart()
    {
        var first = new byte[] { 9, 9, 9 };
        var second = new byte[] { 7, 7, 7, 7 };

        using var db = CreateDb();
        await SetLogoAsync(db, WriteUpload("first.png", first));
        (await PdfLogoCache.ResolveAsync(db)).Should().Equal(first);

        var setting = await db.Settings.FirstAsync(s => s.Key == "website.logoUrl");
        setting.Value = WriteUpload("second.png", second);
        await db.SaveChangesAsync();

        (await PdfLogoCache.ResolveAsync(db)).Should().Equal(second,
            "a re-upload must reach the next PDF; the previous Lazy cache never re-read");
    }

    /// <summary>
    /// A configured logo whose file is missing is a broken upload, not an instruction to print
    /// a document with no identity on it.
    /// </summary>
    [Fact]
    public async Task A_configured_logo_that_cannot_be_read_falls_back_rather_than_printing_nothing()
    {
        using var db = CreateDb();
        await SetLogoAsync(db, "/uploads/does-not-exist.png");

        var bytes = await PdfLogoCache.ResolveAsync(db);

        bytes.Should().BeEquivalentTo(PdfLogoCache.LogoBytes);
    }

    /// <summary>
    /// Uploads are read by name from a known directory, so a traversing name must resolve to
    /// nothing rather than reaching a file elsewhere on the host.
    /// </summary>
    [Theory]
    [InlineData("/uploads/../../etc/passwd")]
    [InlineData("/uploads/sub/dir.png")]
    public async Task A_traversing_upload_name_is_refused(string url)
    {
        using var db = CreateDb();
        await SetLogoAsync(db, url);

        var bytes = await PdfLogoCache.ResolveAsync(db);

        bytes.Should().BeEquivalentTo(PdfLogoCache.LogoBytes, "the traversing path must not resolve");
    }

    /// <summary>
    /// The identity object carries the resolved logo, so a document never decides for itself
    /// where its logo comes from.
    /// </summary>
    [Fact]
    public async Task Clinic_identity_carries_the_configured_logo()
    {
        var expected = new byte[] { 4, 2 };
        using var db = CreateDb();
        await SetLogoAsync(db, WriteUpload("identity-logo.png", expected));

        var identity = await FinanceClinicIdentity.ResolveAsync(db);

        identity.LogoBytes.Should().Equal(expected);
    }
}
