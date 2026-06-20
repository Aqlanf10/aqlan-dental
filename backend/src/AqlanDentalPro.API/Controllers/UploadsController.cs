using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Sockets;

namespace AqlanDentalPro.API.Controllers;

public sealed class ImportRemoteImageRequest
{
    public string Url { get; init; } = string.Empty;
}

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "StaffOnly")]
public class UploadsController(
    ILogger<UploadsController> logger,
    IHttpClientFactory httpClientFactory) : ControllerBase
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
    public async Task<IActionResult> Upload(IFormFile file)
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

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Return relative URL so frontend can construct full URL based on current host
        var fileUrl = $"/uploads/{fileName}";

        return Ok(new
        {
            url = fileUrl,
            fileName,
            originalName = file.FileName,
            size = file.Length,
            contentType = file.ContentType
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

        var client = httpClientFactory.CreateClient("RemoteClinicalImage");
        Uri currentUri = sourceUri;
        HttpResponseMessage? response = null;

        try
        {
            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                if (!await IsSafeRemoteUriAsync(currentUri, cancellationToken))
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
        return Ok(new { message = "تم حذف الملف" });
    }

    // SEC-03: Authenticated file-serving endpoint. The legacy UseStaticFiles(/uploads) in
    // Program.cs serves files BEFORE UseAuthentication — anyone with a URL can read clinical
    // photos/X-rays/docs. This endpoint requires StaffOnly auth and serves the same files.
    // Frontend migration to use this endpoint (with Authorization header via fetch+blob) is
    // tracked separately; until then, UseStaticFiles is gated behind a Production-only auth
    // middleware (see Program.cs) so Dev convenience is preserved but Prod is protected.
    [HttpGet("{fileName}")]
    public IActionResult Download(string fileName)
    {
        // Prevent path traversal (same guard as Delete)
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest(new { message = "اسم الملف غير صالح" });

        var uploadsPath = ResolveUploadsDirectory();
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "الملف غير موجود" });

        var ext = Path.GetExtension(fileName).ToLower();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".webm" => "audio/webm",
            ".ogg" => "audio/ogg",
            ".mp4" => "audio/mp4",
            ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType, fileName);
    }

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
