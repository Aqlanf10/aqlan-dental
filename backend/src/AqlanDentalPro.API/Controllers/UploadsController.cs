using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace AqlanDentalPro.API.Controllers;

public sealed class ImportRemoteImageRequest
{
    public string Url { get; init; } = string.Empty;
    public Guid? PatientId { get; init; }
    public string Purpose { get; init; } = "clinical-image";
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
}

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "StaffOnly")]
public class UploadsController(
    ILogger<UploadsController> logger,
    IHttpClientFactory httpClientFactory,
    ICurrentUserService currentUser,
    IBranchResourceScope branchScope,
    AppDbContext db,
    IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
        "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf",
        ".webm", ".ogg", ".mp4", ".m4a", ".mp3", ".wav"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private const int MaxRedirects = 3;
    private const string PublicPurpose = "public-website";

    // Resolved once at first use — same logic as Program.cs static files config
    private static string? _resolvedPath;
    private static readonly object _pathLock = new();

    private string ResolveUploadsDirectory()
    {
        if (_resolvedPath is not null) return _resolvedPath;
        lock (_pathLock)
        {
            if (_resolvedPath is not null) return _resolvedPath;

            var envPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                Directory.CreateDirectory(envPath);
                _resolvedPath = envPath;
                return _resolvedPath;
            }

            var primaryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            try
            {
                Directory.CreateDirectory(primaryPath);
                var probe = Path.Combine(primaryPath, $".write-test-{Guid.NewGuid()}");
                System.IO.File.WriteAllText(probe, "test");
                System.IO.File.Delete(probe);
                _resolvedPath = primaryPath;
            }
            catch
            {
                var fallback = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
                Directory.CreateDirectory(fallback);
                logger.LogWarning(
                    "UPLOADS_PATH غير مضبوط — سيُستخدم المجلد المؤقت {Path}. " +
                    "الملفات ستُفقد عند إعادة النشر. اضبط UPLOADS_PATH على Railway.",
                    fallback);
                _resolvedPath = fallback;
            }
        }
        return _resolvedPath!;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] Guid? patientId = null,
        [FromForm] string purpose = "general",
        [FromForm] string? resourceType = null,
        [FromForm] Guid? resourceId = null,
        [FromForm] bool isPublic = false,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "الملف مطلوب" });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "حجم الملف يتجاوز 10 ميجابايت" });

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "نوع الملف غير مدعوم. المسموح به: JPG، PNG، WEBP، PDF" });

        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new { message = "نوع MIME غير مدعوم" });

        var ownership = await ResolveUploadOwnershipAsync(
            patientId,
            purpose,
            resourceType,
            resourceId,
            isPublic,
            cancellationToken);
        if (ownership.Error is not null)
            return ownership.Error;

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var detected = DetectFileType(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        if (detected is null || !ExtensionMatches(ext, detected.Value.Extension))
            return BadRequest(new { message = "محتوى الملف لا يطابق النوع المعلن" });

        string uploadsPath;
        try
        {
            uploadsPath = ResolveUploadsDirectory();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve uploads directory");
            return StatusCode(500, new { message = "فشل إنشاء مجلد المرفقات" });
        }

        var storedExtension = detected.Value.Extension == ".jpeg" ? ".jpg" : detected.Value.Extension;
        var fileName = $"{Guid.NewGuid()}{storedExtension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        buffer.Position = 0;
        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await buffer.CopyToAsync(stream, cancellationToken);

        var uploadedFile = new UploadedFile
        {
            FileName = fileName,
            OriginalName = Path.GetFileName(file.FileName),
            ContentType = detected.Value.MimeType,
            Size = buffer.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant(),
            Purpose = NormalizePurpose(purpose),
            ResourceType = NormalizeOptional(resourceType),
            ResourceId = resourceId,
            PatientId = ownership.PatientId,
            BranchId = ownership.BranchId,
            UploadedBy = currentUser.UserId!.Value,
            IsPublic = isPublic
        };

        try
        {
            db.UploadedFiles.Add(uploadedFile);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(filePath);
            throw;
        }

        // Return relative URL so frontend can construct full URL based on current host
        var fileUrl = $"/uploads/{fileName}";

        return Ok(new
        {
            url = fileUrl,
            fileName,
            originalName = file.FileName,
            size = buffer.Length,
            contentType = detected.Value.MimeType
        });
    }

    [HttpPost("import-image")]
    public async Task<IActionResult> ImportImage(
        [FromBody] ImportRemoteImageRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var sourceUri) ||
            sourceUri.Scheme is not ("http" or "https"))
        {
            return BadRequest(new { message = "أدخل رابط صورة صحيحًا يبدأ بـ http أو https" });
        }

        var ownership = await ResolveUploadOwnershipAsync(
            request.PatientId,
            request.Purpose,
            request.ResourceType,
            request.ResourceId,
            isPublic: false,
            cancellationToken);
        if (ownership.Error is not null)
            return ownership.Error;

        if (!IsAllowedRemoteHost(sourceUri))
            return BadRequest(new { message = "مصدر الصورة غير مسموح به" });

        var client = httpClientFactory.CreateClient("RemoteClinicalImage");
        Uri currentUri = sourceUri;
        HttpResponseMessage? response = null;

        try
        {
            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                if (!IsAllowedRemoteHost(currentUri) ||
                    !await IsSafeRemoteUriAsync(currentUri, cancellationToken))
                    return BadRequest(new { message = "لا يمكن استيراد الصور من عنوان محلي أو شبكة خاصة" });

                response?.Dispose();
                response = await client.GetAsync(
                    currentUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!IsRedirect(response.StatusCode))
                    break;

                if (redirect == MaxRedirects || response.Headers.Location is null)
                    return BadRequest(new { message = "رابط الصورة يحتوي على تحويلات كثيرة أو غير صالحة" });

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
            }

            if (response is null || !response.IsSuccessStatusCode)
                return BadRequest(new { message = "تعذر تنزيل الصورة من الرابط المحدد" });

            if (response.Content.Headers.ContentLength is > MaxFileSize)
                return BadRequest(new { message = "حجم الصورة يتجاوز 10 ميجابايت" });

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxFileSize)
                    return BadRequest(new { message = "حجم الصورة يتجاوز 10 ميجابايت" });
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }

            var detected = DetectImageType(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
            if (detected is null)
                return BadRequest(new { message = "الرابط لا يشير إلى صورة JPG أو PNG أو WEBP صالحة" });

            var uploadsPath = ResolveUploadsDirectory();
            var fileName = $"{Guid.NewGuid()}{detected.Value.Extension}";
            var filePath = Path.Combine(uploadsPath, fileName);
            buffer.Position = 0;
            await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await buffer.CopyToAsync(output, cancellationToken);

            var uploadedFile = new UploadedFile
            {
                FileName = fileName,
                OriginalName = Path.GetFileName(currentUri.LocalPath),
                ContentType = detected.Value.MimeType,
                Size = buffer.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant(),
                Purpose = NormalizePurpose(request.Purpose),
                ResourceType = NormalizeOptional(request.ResourceType),
                ResourceId = request.ResourceId,
                PatientId = ownership.PatientId,
                BranchId = ownership.BranchId,
                UploadedBy = currentUser.UserId!.Value,
                IsPublic = false
            };

            try
            {
                db.UploadedFiles.Add(uploadedFile);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                System.IO.File.Delete(filePath);
                throw;
            }

            return Ok(new
            {
                url = $"/uploads/{fileName}",
                fileName,
                originalName = Path.GetFileName(currentUri.LocalPath),
                size = buffer.Length,
                contentType = detected.Value.MimeType,
                sourceUrl = sourceUri.ToString(),
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "انتهت مهلة تنزيل الصورة من الرابط" });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Remote clinical image import failed for host {Host}", sourceUri.Host);
            return BadRequest(new { message = "تعذر الاتصال بخادم الصورة" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected remote clinical image import failure for host {Host}", sourceUri.Host);
            return StatusCode(500, new { message = "تعذر استيراد الصورة حاليًا" });
        }
        finally
        {
            response?.Dispose();
        }
    }

    [HttpDelete("{fileName}")]
    public IActionResult Delete(string fileName)
    {
        // SEC-24 FIX: Restrict delete to Admin only. Previously any staff member could delete
        // any uploaded file by GUID — a receptionist could delete clinical photos uploaded by
        // doctors. No ownership check was performed.
        if (!currentUser.IsAdmin)
            return StatusCode(403, new { message = "غير مصرح لك بحذف الملفات — يتطلب صلاحية مدير" });

        // Prevent path traversal
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest(new { message = "اسم الملف غير صالح" });

        var uploadsPath = ResolveUploadsDirectory();
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "الملف غير موجود" });

        System.IO.File.Delete(filePath);
        var metadata = db.UploadedFiles.FirstOrDefault(x => x.FileName == fileName);
        if (metadata is not null)
        {
            db.UploadedFiles.Remove(metadata);
            db.SaveChanges();
        }
        return Ok(new { message = "تم حذف الملف" });
    }

    // Both API and legacy URLs terminate here. The physical path is never exposed;
    // resource metadata and branch ownership are checked before every response.
    [HttpGet("{fileName}")]
    [HttpGet("/uploads/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken = default)
    {
        // Prevent path traversal (same guard as Delete)
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest(new { message = "اسم الملف غير صالح" });

        var uploadsPath = ResolveUploadsDirectory();
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "الملف غير موجود" });

        var metadata = await db.UploadedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FileName == fileName, cancellationToken);

        if (metadata is not null)
        {
            if (!metadata.IsPublic)
            {
                if (!currentUser.IsAuthenticated)
                    return Unauthorized(new { message = "يجب تسجيل الدخول لعرض الملف" });
                if (!branchScope.CanAccess(metadata.BranchId))
                    return StatusCode(403, new { message = "لا تملك صلاحية الوصول إلى هذا الملف" });
            }

            return PhysicalFile(filePath, metadata.ContentType, metadata.OriginalName);
        }

        var legacy = await ResolveLegacyAccessAsync(fileName, cancellationToken);
        if (!legacy.Found)
            return NotFound(new { message = "الملف غير مرتبط بمورد مصرح" });
        if (!legacy.IsPublic)
        {
            if (!currentUser.IsAuthenticated)
                return Unauthorized(new { message = "يجب تسجيل الدخول لعرض الملف" });
            if (!branchScope.CanAccess(legacy.BranchId))
                return StatusCode(403, new { message = "لا تملك صلاحية الوصول إلى هذا الملف" });
        }

        return PhysicalFile(filePath, ContentTypeForExtension(Path.GetExtension(fileName)), fileName);
    }

    private async Task<(Guid? PatientId, Guid? BranchId, IActionResult? Error)> ResolveUploadOwnershipAsync(
        Guid? patientId,
        string purpose,
        string? resourceType,
        Guid? resourceId,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return (null, null, Unauthorized(new { message = "يجب تسجيل الدخول لرفع الملفات" }));

        if (isPublic)
        {
            if (!currentUser.IsAdmin || !string.Equals(NormalizePurpose(purpose), PublicPurpose, StringComparison.Ordinal))
                return (null, null, StatusCode(403, new { message = "نشر الملفات العامة يتطلب صلاحية المدير وغرض الموقع العام" }));
            return (null, branchScope.ResolveWriteBranch(), null);
        }

        Guid? branchId = null;
        if (patientId.HasValue)
        {
            branchId = await db.Patients
                .Where(x => x.Id == patientId.Value)
                .Select(x => x.BranchId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!branchScope.CanAccess(branchId))
                return (null, null, StatusCode(403, new { message = "لا تملك صلاحية رفع ملف لهذا المريض" }));
        }
        else if (resourceId.HasValue &&
                 string.Equals(resourceType, "OrthoCase", StringComparison.OrdinalIgnoreCase))
        {
            var owner = await db.OrthoCases
                .Where(x => x.Id == resourceId.Value)
                .Select(x => new { x.PatientId, x.BranchId })
                .FirstOrDefaultAsync(cancellationToken);
            if (owner is null || !branchScope.CanAccess(owner.BranchId))
                return (null, null, StatusCode(403, new { message = "لا تملك صلاحية رفع ملف لهذه الحالة" }));
            patientId = owner.PatientId;
            branchId = owner.BranchId;
        }
        else
        {
            branchId = branchScope.ResolveWriteBranch();
            if (!branchId.HasValue)
                return (null, null, StatusCode(403, new { message = "يجب تحديد مورد أو فرع يملك الملف" }));
        }

        return (patientId, branchId, null);
    }

    private bool IsAllowedRemoteHost(Uri uri)
    {
        var allowedHosts = configuration
            .GetSection("Uploads:RemoteImageAllowedHosts")
            .Get<string[]>() ?? [];

        return allowedHosts.Any(host =>
            !string.IsNullOrWhiteSpace(host) &&
            string.Equals(uri.IdnHost, host.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(bool Found, bool IsPublic, Guid? BranchId)> ResolveLegacyAccessAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var url = $"/uploads/{fileName}";

        var isPublic = await db.Settings.AnyAsync(
            x => x.Value == url &&
                 (x.Key.Contains("logo") || x.Key.Contains("favicon") ||
                  x.Key.Contains("banner") || x.Key.Contains("image")),
            cancellationToken);
        if (isPublic)
            return (true, true, null);

        var patientBranch = await db.ClinicalPhotos
            .Where(x => x.FileUrl == url)
            .Select(x => x.Patient.BranchId)
            .Concat(db.Radiographs.Where(x => x.FileUrl == url).Select(x => x.Patient.BranchId))
            .Concat(db.Documents.Where(x => x.FileUrl == url).Select(x => x.Patient.BranchId))
            .FirstOrDefaultAsync(cancellationToken);
        if (patientBranch.HasValue)
            return (true, false, patientBranch);

        var orthoBranch = await db.OrthoClinicalPhotos
            .Where(x => x.PhotoUrl == url)
            .Select(x => x.OrthoCase.BranchId)
            .Concat(db.CephAnalyses.Where(x => x.XrayFileUrl == url).Select(x => x.OrthoCase.BranchId))
            .Concat(db.PhotoAnalyses.Where(x => x.ImageFileUrl == url).Select(x => x.OrthoCase.BranchId))
            .FirstOrDefaultAsync(cancellationToken);
        if (orthoBranch.HasValue)
            return (true, false, orthoBranch);

        var businessBranch = await db.SupplierBills
            .Where(x => x.AttachmentUrl == url)
            .Select(x => (Guid?)x.BranchId)
            .Concat(db.OperationalExpenses.Where(x => x.ReceiptAttachmentUrl == url).Select(x => (Guid?)x.BranchId))
            .Concat(db.Inventory.Where(x => x.ImageUrl == url).Select(x => x.BranchId))
            .FirstOrDefaultAsync(cancellationToken);
        if (businessBranch.HasValue)
            return (true, false, businessBranch);

        var messageBranch = await db.Messages
            .Where(x => x.AttachmentUrl == url || x.Attachments.Any(a => a.FileUrl == url))
            .Select(x => x.Conversation.BranchId)
            .FirstOrDefaultAsync(cancellationToken);
        return messageBranch.HasValue
            ? (true, false, messageBranch)
            : (false, false, null);
    }

    private static string NormalizePurpose(string? purpose) =>
        string.IsNullOrWhiteSpace(purpose) ? "general" : purpose.Trim().ToLowerInvariant()[..Math.Min(purpose.Trim().Length, 100)];

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private static bool ExtensionMatches(string declared, string detected) =>
        string.Equals(declared, detected, StringComparison.OrdinalIgnoreCase) ||
        (declared is ".jpg" or ".jpeg" && detected is ".jpg" or ".jpeg") ||
        (declared is ".m4a" or ".mp4" && detected is ".m4a" or ".mp4");

    private static (string Extension, string MimeType)? DetectFileType(ReadOnlySpan<byte> bytes)
    {
        var image = DetectImageType(bytes);
        if (image is not null) return image;

        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8))
            return (".pdf", "application/pdf");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            return (".wav", "audio/wav");
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual("OggS"u8))
            return (".ogg", "audio/ogg");
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            return (".webm", "audio/webm");
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
            return bytes.Slice(8, 4).SequenceEqual("M4A "u8)
                ? (".m4a", "audio/mp4")
                : (".mp4", "audio/mp4");
        if ((bytes.Length >= 3 && bytes[..3].SequenceEqual("ID3"u8)) ||
            (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0))
            return (".mp3", "audio/mpeg");

        return null;
    }

    private static string ContentTypeForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".webm" => "audio/webm",
            ".ogg" => "audio/ogg",
            ".mp4" or ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static async Task<bool> IsSafeRemoteUriAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (uri.IsLoopback || string.IsNullOrWhiteSpace(uri.Host))
            return false;

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
            addresses = [literal];
        else
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !(bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                bytes[0] == 0 ||
                bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return false;
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
                return false;
        }

        return true;
    }

    private static (string Extension, string MimeType)? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return (".jpg", "image/jpeg");

        if (bytes.Length >= 8 &&
            bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return (".png", "image/png");

        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");

        return null;
    }
}
