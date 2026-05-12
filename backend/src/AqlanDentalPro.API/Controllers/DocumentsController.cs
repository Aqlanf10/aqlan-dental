using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── Request DTOs ───────────────────────────────────────────────────────────────

public sealed class CreateDocumentRequest
{
    public Guid PatientId { get; init; }
    public string? Title { get; init; }
    public string? DocumentType { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? MimeType { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateDocumentRequest
{
    public string? Title { get; init; }
    public string? DocumentType { get; init; }
    public string? Notes { get; init; }
    public bool? Signed { get; init; }
}

// ─── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/documents")]
[Authorize(Policy = "StaffOnly")]
public class DocumentsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // ─── GET /api/documents?patientId={patientId} ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? patientId, [FromQuery] string? documentType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Documents.AsQueryable();

        if (patientId.HasValue)
            query = query.Where(d => d.PatientId == patientId.Value);

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
                d.PatientId,
                d.DocumentType,
                d.Title,
                d.FileUrl,
                d.FileName,
                d.FileSize,
                d.MimeType,
                d.Notes,
                d.UploadedBy,
                d.Signed,
                SignedAt = d.SignedAt != null ? d.SignedAt.Value.ToString("yyyy-MM-dd") : null,
                d.IsActive,
                CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = d.UpdatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();

        return Ok(new { data = documents, total, page, pageSize });
    }

    // ─── GET /api/documents/{id} ──────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocument(Guid id)
    {
        var doc = await db.Documents
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.PatientId,
                d.DocumentType,
                d.Title,
                d.FileUrl,
                d.FileName,
                d.FileSize,
                d.MimeType,
                d.Notes,
                d.UploadedBy,
                d.Signed,
                SignedAt = d.SignedAt != null ? d.SignedAt.Value.ToString("yyyy-MM-dd") : null,
                d.IsActive,
                CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = d.UpdatedAt.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();

        if (doc is null)
            return NotFound(new { message = "المستند غير موجود" });

        return Ok(doc);
    }

    // ─── POST /api/documents ──────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentRequest req)
    {
        if (req.PatientId == Guid.Empty)
            return BadRequest(new { message = "معرّف المريض مطلوب" });

        var patientExists = await db.Patients.AnyAsync(p => p.Id == req.PatientId);
        if (!patientExists)
            return BadRequest(new { message = "المريض غير موجود" });

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المستند مطلوب" });

        var document = new Document
        {
            PatientId = req.PatientId,
            Title = req.Title,
            DocumentType = req.DocumentType,
            FileUrl = req.FileUrl,
            FileName = req.FileName,
            FileSize = req.FileSize,
            MimeType = req.MimeType,
            Notes = req.Notes,
            UploadedBy = currentUser.UserId,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return Ok(new
        {
            document.Id,
            message = "تم إضافة المستند بنجاح"
        });
    }

    // ─── PUT /api/documents/{id} ──────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] UpdateDocumentRequest req)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null)
            return NotFound(new { message = "المستند غير موجود" });

        if (!doc.IsActive)
            return BadRequest(new { message = "لا يمكن تعديل مستند محذوف" });

        if (req.Title != null)
            doc.Title = req.Title;
        if (req.DocumentType != null)
            doc.DocumentType = req.DocumentType;
        if (req.Notes != null)
            doc.Notes = req.Notes;
        if (req.Signed.HasValue && req.Signed.Value && !doc.Signed)
        {
            doc.Signed = true;
            doc.SignedAt = DateTime.UtcNow;
        }

        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تحديث المستند بنجاح" });
    }

    // ─── DELETE /api/documents/{id} (soft-delete) ─────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null)
            return NotFound(new { message = "المستند غير موجود" });

        if (!doc.IsActive)
            return BadRequest(new { message = "المستند محذوف بالفعل" });

        doc.IsActive = false;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedBy = currentUser.UserId;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف المستند بنجاح" });
    }
}
