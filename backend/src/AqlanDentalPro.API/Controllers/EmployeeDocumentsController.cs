using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class UploadEmployeeDocumentRequest
{
    public Guid EmployeeId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
}

[ApiController]
[Route("api/employee-documents")]
[Authorize]
public class EmployeeDocumentsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Get documents for an employee
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid employeeId,
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (employeeId == Guid.Empty)
            return BadRequest(new { message = "معرف الموظف مطلوب" });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.EmployeeDocuments
            .Where(d => d.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(documentType))
            query = query.Where(d => d.DocumentType == documentType);

        var total = await query.CountAsync();

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.EmployeeId,
                d.DocumentType,
                d.Title,
                d.FileName,
                d.ContentType,
                d.FileSize,
                d.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = documents, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    /// <summary>
    /// Upload a document for an employee
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Upload([FromBody] UploadEmployeeDocumentRequest req)
    {
        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المستند مطلوب" });

        if (string.IsNullOrWhiteSpace(req.FilePath))
            return BadRequest(new { message = "مسار الملف مطلوب" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var document = new EmployeeDocument
        {
            EmployeeId = req.EmployeeId,
            DocumentType = req.DocumentType.Trim(),
            Title = req.Title.Trim(),
            FilePath = req.FilePath.Trim(),
            FileName = req.FileName?.Trim(),
            ContentType = req.ContentType?.Trim(),
            FileSize = req.FileSize,
            UploadedBy = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
        };

        db.EmployeeDocuments.Add(document);
        await db.SaveChangesAsync();

        return Created($"/api/employee-documents/{document.Id}", new
        {
            document.Id,
            document.EmployeeId,
            document.DocumentType,
            document.Title,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.CreatedAt,
        });
    }

    /// <summary>
    /// Delete an employee document
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var document = await db.EmployeeDocuments.FindAsync(id);
        if (document is null)
            return NotFound(new { message = "المستند غير موجود" });

        document.IsActive = false;
        document.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف المستند بنجاح" });
    }
}
