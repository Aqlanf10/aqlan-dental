using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController(IFileService fileService) : ControllerBase
{
    private static readonly HashSet<string> AllowedSubDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "clinical-photos", "radiographs", "ceph-xrays", "documents"
    };

    /// <summary>
    /// POST /api/files/upload — multipart form upload
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string subDirectory,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "لم يتم اختيار ملف أو أن الملف فارغ" });

        if (string.IsNullOrWhiteSpace(subDirectory) || !AllowedSubDirectories.Contains(subDirectory))
            return BadRequest(new { message = $"الدليل الفرعي غير صالح. المسموح: {string.Join(", ", AllowedSubDirectories)}" });

        if (file.Length > 20 * 1024 * 1024) // 20 MB limit
            return BadRequest(new { message = "حجم الملف يتجاوز الحد المسموح (20 ميجابايت)" });

        await using var stream = file.OpenReadStream();
        var url = await fileService.UploadAsync(stream, file.FileName, subDirectory, ct);

        return Ok(new { url });
    }

    /// <summary>
    /// DELETE /api/files?url={relativeUrl} — delete a file
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { message = "يجب تحديد رابط الملف" });

        await fileService.DeleteAsync(url, ct);
        return Ok(new { message = "تم حذف الملف بنجاح" });
    }
}
