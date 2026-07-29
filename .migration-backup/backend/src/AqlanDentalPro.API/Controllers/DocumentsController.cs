using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
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
    /// <summary>Optional link to an orthodontic case (standardized records — Phase 2).</summary>
    public Guid? OrthoCaseId { get; init; }
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
[ServiceFilter(typeof(PatientAccessFilter))]
public class DocumentsController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit) : ControllerBase
{
    // CLIN-01: Per-patient access check for actions where patientId is in body or inferred.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "Document", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }
    // ─── GET /api/documents?patientId={patientId} ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? patientId, [FromQuery] string? documentType, [FromQuery] Guid? orthoCaseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Documents.AsQueryable();

        if (patientId.HasValue)
            query = query.Where(d => d.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(documentType))
            query = query.Where(d => d.DocumentType == documentType);

        if (orthoCaseId.HasValue)
            query = query.Where(d => d.OrthoCaseId == orthoCaseId);

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
                d.OrthoCaseId,
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
        // CLIN-01: load patientId first for the access check before the projection.
        var patientId = await db.Documents.Where(d => d.Id == id).Select(d => d.PatientId).FirstOrDefaultAsync();
        if (patientId == Guid.Empty)
            return NotFound(new { message = "المستند غير موجود" });
        var denied = await DenyIfDoctorCannotAccess(patientId);
        if (denied is not null) return denied;

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
                d.OrthoCaseId,
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

        // SEC-DOCS: Per-patient access check before creating. The class-level
        // PatientAccessFilter only inspects route + query values for "patientId", but
        // CreateDocument receives PatientId in the REQUEST BODY, so the filter cannot
        // see it. Without this explicit check a doctor with no access to Patient X
        // could still create documents under Patient X. Mirrors PrescriptionsController.
        // (Placed before the patientExists check so we don't leak patient existence
        // to a doctor who has no access.)
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        var patientExists = await db.Patients.AnyAsync(p => p.Id == req.PatientId);
        if (!patientExists)
            return BadRequest(new { message = "المريض غير موجود" });

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المستند مطلوب" });

        // Codex review (PR #357): the case must belong to the SAME patient —
        // existence alone would allow cross-patient links.
        if (req.OrthoCaseId.HasValue &&
            !await db.OrthoCases.AnyAsync(c => c.Id == req.OrthoCaseId.Value && c.PatientId == req.PatientId))
            return BadRequest(new { message = "الحالة التقويمية غير موجودة أو لا تخص هذا المريض" });

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
            OrthoCaseId = req.OrthoCaseId,
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

        // SEC-DOCS: Per-patient access check before mutating. UpdateDocumentRequest does
        // NOT carry PatientId (it only patches Title/DocumentType/Notes/Signed), and the
        // class-level PatientAccessFilter only inspects route + query for "patientId" —
        // so the filter never sees a patientId for this action. Resolve it from the
        // fetched document and check explicitly. Mirrors the established pattern
        // (PrescriptionsController.Delete / ClinicalPhotosController.DeletePhoto).
        var denied = await DenyIfDoctorCannotAccess(doc.PatientId);
        if (denied is not null) return denied;

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

        // SEC-DOCS: Per-patient access check before soft-deleting. The route only
        // carries the document id ({id:guid}), so the class-level PatientAccessFilter
        // never sees a patientId for this action. Resolve it from the fetched document
        // and check explicitly. Mirrors PrescriptionsController.Delete.
        var denied = await DenyIfDoctorCannotAccess(doc.PatientId);
        if (denied is not null) return denied;

        if (!doc.IsActive)
            return BadRequest(new { message = "المستند محذوف بالفعل" });

        doc.IsActive = false;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedBy = currentUser.UserId;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف المستند بنجاح" });
    }
}
