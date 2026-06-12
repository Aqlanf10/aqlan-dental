using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "StaffOnly")]
public class UploadsController(ILogger<UploadsController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // image/tiff added for cephalometric radiographs (note: TIFF may not
        // preview in-browser — the ceph uploader warns about this).
        "image/jpeg", "image/png", "image/webp", "image/tiff", "application/pdf",
        "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".pdf",
        ".webm", ".ogg", ".mp4", ".m4a", ".mp3", ".wav"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

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

    [HttpDelete("{fileName}")]
    public IActionResult Delete(string fileName)
    {
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
}
