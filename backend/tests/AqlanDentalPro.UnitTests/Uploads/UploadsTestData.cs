using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace AqlanDentalPro.UnitTests.Uploads;

/// <summary>
/// Shared seed + builder helpers for <see cref="UploadsController"/> unit tests.
///
/// Uploads handle PHI (patient clinical files: X-rays, intraoral photos, consent PDFs,
/// voice notes). The controller's security-critical surface is:
///   - File-extension allow-list (reject .exe / .js / .html / etc.)
///   - MIME-type allow-list (reject application/octet-stream, text/html, etc.)
///   - Size cap (10 MB — matches [RequestSizeLimit])
///   - Admin-only Delete (SEC-24: previously any staff member could delete clinical
///     photos by GUID)
///   - Path-traversal guard on Download + Delete (reject '/', '\\', '..')
///   - Authenticated file-serving endpoint (SEC-03: legacy UseStaticFiles served
///     /uploads BEFORE auth — anyone with a URL could read clinical photos)
///
/// Static state caveat:
///   The controller caches the uploads directory in a static `_resolvedPath` field
///   (locked, set-once). To keep tests deterministic we set the UPLOADS_PATH env var
///   to a per-test-run temp directory BEFORE constructing the controller. The static
///   cache means the FIRST test that calls ResolveUploadsDirectory pins the path for
///   every subsequent test in the run — so we use a single shared temp directory for
///   the whole fixture (set once via the static ctor).
/// </summary>
internal static class UploadsTestData
{
    public static readonly Guid DefaultUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultBranchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    /// <summary>
    /// Shared temp directory for all UploadsController tests in this assembly run.
    /// Set via UPLOADS_PATH env var before the first controller is constructed so the
    /// controller's static `_resolvedPath` cache points here.
    /// </summary>
    public static readonly string SharedUploadsDir = Path.Combine(
        Path.GetTempPath(),
        "aqlan-test-uploads-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly FieldInfo ResolvedPathField =
        typeof(UploadsController).GetField("_resolvedPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("UploadsController._resolvedPath was not found.");

    static UploadsTestData()
    {
        EnsureSharedUploadsPath();
    }

    private static void EnsureSharedUploadsPath()
    {
        // CI can construct UploadsController in a different test before this helper runs,
        // so pin the controller cache to this fixture's directory every time we build one.
        Directory.CreateDirectory(SharedUploadsDir);
        Environment.SetEnvironmentVariable("UPLOADS_PATH", SharedUploadsDir);
        ResolvedPathField.SetValue(null, SharedUploadsDir);
    }

    /// <summary>
    /// Builds a mocked <see cref="IFormFile"/> backed by an in-memory stream of
    /// <paramref name="contents"/>. The mock honours the three properties the
    /// UploadsController reads: <c>FileName</c>, <c>ContentType</c>, <c>Length</c>;
    /// plus <c>CopyToAsync</c> (used by the Upload action) and <c>OpenReadStream</c>
    /// (used elsewhere if needed).
    /// </summary>
    public static IFormFile BuildFormFile(
        byte[] contents,
        string fileName = "photo.jpg",
        string contentType = "image/jpeg")
    {
        var stream = new MemoryStream(contents);
        var fileMock = new Mock<IFormFile>();
        fileMock.SetupGet(f => f.FileName).Returns(fileName);
        fileMock.SetupGet(f => f.ContentType).Returns(contentType);
        fileMock.SetupGet(f => f.Length).Returns(contents.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, ct) => stream.CopyTo(s))
            .Returns(Task.CompletedTask);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        return fileMock.Object;
    }

    /// <summary>
    /// Constructs the <see cref="UploadsController"/> with mocked deps. The
    /// <c>UPLOADS_PATH</c> env var is already set by the static ctor — the controller's
    /// first call to <c>ResolveUploadsDirectory</c> will use it (and cache it for the
    /// remainder of the assembly run).
    /// </summary>
    public static UploadsController BuildController(
        Mock<ICurrentUserService>? currentUserMock = null,
        Mock<IHttpClientFactory>? httpClientFactoryMock = null,
        Mock<ILogger<UploadsController>>? loggerMock = null,
        AppDbContext? db = null,
        IConfiguration? configuration = null)
    {
        EnsureSharedUploadsPath();

        currentUserMock ??= new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(x => x.UserId).Returns(DefaultUserId);
        currentUserMock.SetupGet(x => x.BranchId).Returns(DefaultBranchId);
        currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUserMock.SetupGet(x => x.Role).Returns(UserRole.GeneralDentist);
        httpClientFactoryMock ??= new Mock<IHttpClientFactory>();
        loggerMock ??= new Mock<ILogger<UploadsController>>();
        db ??= BuildDb();
        configuration ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Uploads:RemoteImageAllowedHosts:0"] = "example.com",
                ["Uploads:RemoteImageAllowedHosts:1"] = "93.184.216.34"
            })
            .Build();

        return new UploadsController(
            loggerMock.Object,
            httpClientFactoryMock.Object,
            currentUserMock.Object,
            new BranchResourceScope(currentUserMock.Object),
            db,
            configuration);
    }

    public static AppDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"uploads-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// Cleans any files written to the shared uploads dir between tests so each test
    /// starts from a known empty state. Called from each test's Dispose.
    /// </summary>
    public static void CleanSharedDir()
    {
        EnsureSharedUploadsPath();

        if (!Directory.Exists(SharedUploadsDir)) return;
        foreach (var file in Directory.EnumerateFiles(SharedUploadsDir))
        {
            try { File.Delete(file); }
            catch { /* best-effort — another test may be writing */ }
        }
    }
}
