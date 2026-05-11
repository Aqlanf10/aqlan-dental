using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "StaffOnly")]
public class UploadsController : ControllerBase
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

    /// <summary>
    /// Resolves the uploads directory path. Priority:
    /// 1. UPLOADS_PATH environment variable (for Railway persistent volumes)
    /// 2. wwwroot/uploads relative to current directory
    /// 3. /tmp/aqlan-uploads fallback for read-only container environments
    /// </summary>
    private static string EnsureUploadsDirectory()
    {
        // 1. Check UPLOADS_PATH env var (set by Railway for persistent volume)
        var envPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            Directory.CreateDirectory(envPath);
            // Verify write permission
            var testFile = Path.Combine(envPath, $".write-test-{Guid.NewGuid()}");
            System.IO.File.WriteAllText(testFile, "test");
            System.IO.File.Delete(testFile);
            return envPath;
        }

        // 2. Try wwwroot/uploads (works if directory is pre-created with proper ownership)
        var primaryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        try
        {
            Directory.CreateDirectory(primaryPath);
            // Test write permission
            var testFile = Path.Combine(primaryPath, $".write-test-{Guid.NewGuid()}");
            System.IO.File.WriteAllText(testFile, "test");
            System.IO.File.Delete(testFile);
            return primaryPath;
        }
        catch
        {
            // 3. Fallback to /tmp for containerized environments where wwwroot is read-only
            var fallbackPath = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }
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
            uploadsPath = EnsureUploadsDirectory();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "فشل إنشاء مجلد المرفقات", detail = ex.Message });
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

        var uploadsPath = EnsureUploadsDirectory();
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "الملف غير موجود" });

        System.IO.File.Delete(filePath);
        return Ok(new { message = "تم حذف الملف" });
    }
}
