using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif",
        "application/pdf"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Resolves the uploads directory path. Tries wwwroot/uploads first,
    /// falls back to /tmp/uploads if the primary path is not writable.
    /// </summary>
    private static string EnsureUploadsDirectory()
    {
        var primaryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        try
        {
            Directory.CreateDirectory(primaryPath);
            // Test write permission
            var testFile = Path.Combine(primaryPath, $".write-test-{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return primaryPath;
        }
        catch
        {
            // Fallback to /tmp/uploads for containerized environments where wwwroot is read-only
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
            return BadRequest(new { message = "نوع الملف غير مدعوم. المسموح به: JPG، PNG، WebP، GIF، PDF" });

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
        if (fileName.Contains('/') || fileName.Contains('\') || fileName.Contains(".."))
            return BadRequest(new { message = "اسم الملف غير صالح" });

        var uploadsPath = EnsureUploadsDirectory();
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "الملف غير موجود" });

        System.IO.File.Delete(filePath);
        return Ok(new { message = "تم حذف الملف" });
    }
}
