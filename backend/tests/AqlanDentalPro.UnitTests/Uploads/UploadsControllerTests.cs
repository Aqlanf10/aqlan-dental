using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Uploads;

/// <summary>
/// Unit tests for <see cref="UploadsController"/> — the PHI file-upload surface.
///
/// Coverage map:
///   - Upload: file-extension allow-list (reject .exe/.js/.html), MIME-type allow-list,
///     size cap (10 MB), null/empty file rejection, happy path persists file to disk
///     and returns /uploads/{guid}{ext} URL.
///   - Download: 404 for unknown file, 200 + PhysicalFile for existing, path-traversal
///     guard (rejects '/', '\\', '..'), correct Content-Type per extension.
///   - Delete: SEC-24 admin-only — non-admin gets 403 Arabic, admin can delete,
///     path-traversal guard, 404 for unknown file, success returns Arabic message.
///
/// FINDING #1 (TEST-13, documented in PR description, NOT fixed per test-only scope):
///   UploadsController.Upload does NOT call IAuditService.LogAsync. Files uploaded
///   through this endpoint (X-rays, intraoral photos, consent PDFs, voice notes) ARE
///   patient PHI — but no audit record is created when they are uploaded. The only
///   audit trail is the file's filesystem mtime. Compare with DocumentsController which
///   at least captures UploadedBy (though it also doesn't write an audit entry). A
///   follow-up PR should add `IAuditService.LogAsync(AuditAction.Create, "Upload", ...)`
///   to the Upload action.
///
/// FINDING #2: UploadsController.Upload takes only `IFormFile file` — it does NOT
/// accept a patientId. The file is stored with a random GUID filename and a relative
/// URL `/uploads/{guid}{ext}` is returned. There is NO link from the uploaded file
/// back to a patient at this layer; the caller is expected to create a Document entity
/// (or ClinicalPhoto, Radiograph, etc.) with the returned URL. This is by design (the
/// Document entity is the patient-bound record). Tests assert the response shape only.
/// </summary>
public class UploadsControllerTests : IDisposable
{
    public UploadsControllerTests()
    {
        UploadsTestData.CleanSharedDir();
    }

    public void Dispose() => UploadsTestData.CleanSharedDir();

    // ── Upload (POST /api/uploads) — file validation ─────────────────────────────

    [Fact]
    public async Task Upload_NullFile_Returns400_ArabicMessage()
    {
        var controller = UploadsTestData.BuildController();

        var result = await controller.Upload(null!);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("الملف مطلوب");
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400_ArabicMessage()
    {
        var controller = UploadsTestData.BuildController();
        var file = UploadsTestData.BuildFormFile(Array.Empty<byte>());

        var result = await controller.Upload(file);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("الملف مطلوب");
    }

    [Fact]
    public async Task Upload_ExeFile_Returns400_ArabicMessage_FileNotPersisted()
    {
        // PHI surface guard: .exe (and other executable types) must never be accepted —
        // even if a malicious request sets the ContentType to image/jpeg, the extension
        // check rejects it. The file MUST NOT be persisted to disk.
        var controller = UploadsTestData.BuildController();
        var file = UploadsTestData.BuildFormFile(
            contents: new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, // MZ header
            fileName: "malware.exe",
            contentType: "application/x-msdownload");

        var result = await controller.Upload(file);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("نوع الملف غير مدعوم. المسموح به: JPG، PNG، WEBP، PDF");
        Directory.GetFiles(UploadsTestData.SharedUploadsDir).Should().BeEmpty(
            "a rejected extension must NOT leave any file on disk");
    }

    [Theory]
    [InlineData("evil.exe")]
    [InlineData("evil.js")]
    [InlineData("evil.html")]
    [InlineData("evil.svg")]
    [InlineData("evil.php")]
    [InlineData("evil.sh")]
    [InlineData("evil.bat")]
    [InlineData("evil.dll")]
    public async Task Upload_DisallowedExtensions_Returns400_ArabicMessage(string fileName)
    {
        var controller = UploadsTestData.BuildController();
        var file = UploadsTestData.BuildFormFile(
            contents: new byte[] { 1, 2, 3 },
            fileName: fileName,
            contentType: "application/octet-stream");

        var result = await controller.Upload(file);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("نوع الملف غير مدعوم. المسموح به: JPG، PNG، WEBP، PDF");
    }

    [Fact]
    public async Task Upload_JpgFile_WithDisallowedMimeType_Returns400_ArabicMessage()
    {
        // Defense in depth: extension is allowed (.jpg) but MIME type is text/html
        // (NOT in the allowed-MIME list). The MIME check must reject it.
        var controller = UploadsTestData.BuildController();
        var file = UploadsTestData.BuildFormFile(
            contents: new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            fileName: "photo.jpg",
            contentType: "text/html"); // disallowed MIME

        var result = await controller.Upload(file);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("نوع MIME غير مدعوم");
    }

    [Fact]
    public async Task Upload_FileExceeding10MB_Returns400_ArabicMessage()
    {
        var controller = UploadsTestData.BuildController();
        // 10 MB + 1 byte — just over the cap
        var oversize = new byte[(10 * 1024 * 1024) + 1];
        var file = UploadsTestData.BuildFormFile(oversize, fileName: "big.jpg", contentType: "image/jpeg");

        var result = await controller.Upload(file);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("حجم الملف يتجاوز 10 ميجابايت");
        Directory.GetFiles(UploadsTestData.SharedUploadsDir).Should().BeEmpty(
            "an oversize file must NOT be persisted");
    }

    [Fact]
    public async Task Upload_ValidJpg_PersistsFile_ReturnsUrlAndMetadata()
    {
        var controller = UploadsTestData.BuildController();
        var contents = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var file = UploadsTestData.BuildFormFile(contents, fileName: "intraoral.jpg", contentType: "image/jpeg");

        var result = await controller.Upload(file);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        dynamic payload = ok.Value!;
        string url = payload.url;
        string storedFileName = payload.fileName;
        string originalName = payload.originalName;
        long size = payload.size;
        string contentType = payload.contentType;

        url.Should().StartWith("/uploads/");
        storedFileName.Should().EndWith(".jpg");
        originalName.Should().Be("intraoral.jpg");
        size.Should().Be(contents.Length);
        contentType.Should().Be("image/jpeg");

        // The file must exist on disk under the shared uploads dir
        var filesOnDisk = Directory.GetFiles(UploadsTestData.SharedUploadsDir);
        filesOnDisk.Should().HaveCount(1);
        Path.GetExtension(filesOnDisk[0]).Should().Be(".jpg",
            "the stored file must keep the original extension");
    }

    [Fact]
    public async Task Upload_ValidPdf_PersistsFile_ReturnsUrl()
    {
        // PDFs are clinical documents (consent forms, referral letters, lab prescriptions).
        // The allow-list explicitly includes .pdf / application/pdf for this reason.
        var controller = UploadsTestData.BuildController();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4 header
        var file = UploadsTestData.BuildFormFile(pdfBytes, fileName: "consent.pdf", contentType: "application/pdf");

        var result = await controller.Upload(file);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((string)payload.url).Should().EndWith(".pdf");
        ((string)payload.contentType).Should().Be("application/pdf");
    }

    // ── Download (GET /api/uploads/{fileName}) ───────────────────────────────────

    [Fact]
    public async Task Download_UnknownFile_Returns404_ArabicMessage()
    {
        var controller = UploadsTestData.BuildController();

        var result = controller.Download("never-exists.jpg");

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الملف غير موجود");
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("subdir/file.jpg")]
    [InlineData("a/b/c.jpg")]
    public void Download_PathTraversalAttempt_Returns400_ArabicMessage(string fileName)
    {
        var controller = UploadsTestData.BuildController();

        var result = controller.Download(fileName);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be("اسم الملف غير صالح");
    }

    [Fact]
    public async Task Download_ExistingFile_ReturnsPhysicalFile_WithContentType()
    {
        // Seed a file on disk, then verify Download serves it with the correct content type.
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(UploadsTestData.SharedUploadsDir, fileName);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        var controller = UploadsTestData.BuildController();

        var result = controller.Download(fileName);

        var physical = result.Should().BeOfType<PhysicalFileResult>().Subject;
        physical.ContentType.Should().Be("image/jpeg",
            "the controller maps .jpg → image/jpeg (SEC-03: served via authenticated endpoint, not UseStaticFiles)");
        physical.FileDownloadName.Should().Be(fileName);
    }

    [Fact]
    public async Task Download_PdfFile_ReturnsApplicationPdfContentType()
    {
        var fileName = $"{Guid.NewGuid():N}.pdf";
        var filePath = Path.Combine(UploadsTestData.SharedUploadsDir, fileName);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var controller = UploadsTestData.BuildController();

        var result = controller.Download(fileName);

        var physical = result.Should().BeOfType<PhysicalFileResult>().Subject;
        physical.ContentType.Should().Be("application/pdf");
    }

    // ── Delete (DELETE /api/uploads/{fileName}) — SEC-24 admin-only ──────────────

    [Fact]
    public async Task Delete_NonAdmin_Returns403_ArabicMessage_DoesNotDeleteFile()
    {
        // SEC-24: previously any staff member (receptionist, accountant, etc.) could delete
        // any uploaded file by GUID — including clinical photos they didn't upload. The fix
        // restricts Delete to Admin only. This test pins the SEC-24 contract.
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(UploadsTestData.SharedUploadsDir, fileName);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8 });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(false);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Reception);
        var controller = UploadsTestData.BuildController(currentUserMock: currentUserMock);

        var result = controller.Delete(fileName);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
        ExtractMessage(statusResult.Value).Should().Be("غير مصرح لك بحذف الملفات — يتطلب صلاحية مدير");
        File.Exists(filePath).Should().BeTrue(
            "a non-admin Delete must NOT actually remove the file from disk");
    }

    [Fact]
    public async Task Delete_AdminRole_DeletesFile_ReturnsArabicSuccessMessage()
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(UploadsTestData.SharedUploadsDir, fileName);
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8 });

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        currentUserMock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var controller = UploadsTestData.BuildController(currentUserMock: currentUserMock);

        var result = controller.Delete(fileName);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم حذف الملف");
        File.Exists(filePath).Should().BeFalse("admin Delete must remove the file from disk");
    }

    [Fact]
    public async Task Delete_AdminRole_UnknownFile_Returns404_ArabicMessage()
    {
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        var controller = UploadsTestData.BuildController(currentUserMock: currentUserMock);

        var result = controller.Delete("never-exists.jpg");

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("الملف غير موجود");
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("subdir/file.jpg")]
    public void Delete_PathTraversalAttempt_Returns400_ArabicMessage_EvenForAdmin(string fileName)
    {
        // Path-traversal guard applies even to admins — a compromised admin token must NOT
        // be able to escape the uploads directory and delete arbitrary files.
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.SetupGet(c => c.IsAdmin).Returns(true);
        var controller = UploadsTestData.BuildController(currentUserMock: currentUserMock);

        var result = controller.Delete(fileName);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("اسم الملف غير صالح");
    }

    // ── ImportImage (POST /api/uploads/import-image) — URL validation only ───────

    [Fact]
    public async Task ImportImage_RelativeUrl_Returns400_ArabicMessage()
    {
        // The remote-image importer must reject anything that isn't an absolute http/https URL
        // — this is the first guard before any DNS resolution or HTTP request is attempted.
        var controller = UploadsTestData.BuildController();

        var result = await controller.ImportImage(new ImportRemoteImageRequest { Url = "/local/path.jpg" }, default);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("أدخل رابط صورة صحيحًا يبدأ بـ http أو https");
    }

    [Fact]
    public async Task ImportImage_FtpScheme_Returns400_ArabicMessage()
    {
        var controller = UploadsTestData.BuildController();

        var result = await controller.ImportImage(new ImportRemoteImageRequest { Url = "ftp://example.com/x.jpg" }, default);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("أدخل رابط صورة صحيحًا يبدأ بـ http أو https");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
