using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class AddPhotoRequest
{
    public Guid PatientId { get; init; }
    public Guid? OrthoCaseId { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public string Category { get; init; } = "intraoral";
    public string? PhotoType { get; init; }
    public string? Stage { get; init; }
    public string? Notes { get; init; }
    public string? PhotoDate { get; init; }
}

public sealed class AddRadiographRequest
{
    public Guid PatientId { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public string XrayType { get; init; } = string.Empty;
    public string? ToothRelated { get; init; }
    public string? Notes { get; init; }
    public Guid? DoctorId { get; init; }
    public string? XrayDate { get; init; }
}

[ApiController]
[Route("api/clinical-photos")]
[Authorize]
public class ClinicalPhotosController(AppDbContext db) : ControllerBase
{
    [HttpGet("{patientId:guid}")]
    public async Task<IActionResult> GetPhotos(Guid patientId, [FromQuery] string? category, [FromQuery] string? stage, [FromQuery] Guid? orthoCaseId)
    {
        var query = db.ClinicalPhotos.Where(p => p.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(p => p.Stage == stage);
        if (orthoCaseId.HasValue) query = query.Where(p => p.OrthoCaseId == orthoCaseId);

        var photos = await query
            .OrderByDescending(p => p.PhotoDate)
            .Select(p => new {
                p.Id, p.Category, p.PhotoType, p.FileUrl, p.ThumbnailUrl,
                p.Stage, p.Notes, PhotoDate = p.PhotoDate.ToString("yyyy-MM-dd"),
                p.OrthoCaseId
            })
            .ToListAsync();
        return Ok(photos);
    }

    [HttpPost]
    public async Task<IActionResult> AddPhoto([FromBody] AddPhotoRequest req)
    {
        var photo = new ClinicalPhoto
        {
            PatientId   = req.PatientId,
            OrthoCaseId = req.OrthoCaseId,
            FileUrl     = req.FileUrl,
            ThumbnailUrl= req.FileUrl, // same URL for now
            Category    = req.Category,
            PhotoType   = req.PhotoType,
            Stage       = req.Stage,
            Notes       = req.Notes,
            PhotoDate   = req.PhotoDate != null ? DateOnly.Parse(req.PhotoDate) : DateOnly.FromDateTime(DateTime.Today),
        };
        db.ClinicalPhotos.Add(photo);
        await db.SaveChangesAsync();
        return Ok(new { photo.Id, message = "تم إضافة الصورة" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePhoto(Guid id, [FromBody] AddPhotoRequest req)
    {
        var photo = await db.ClinicalPhotos.FindAsync(id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        if (req.FileUrl is not null) photo.FileUrl = req.FileUrl;
        if (req.FileUrl is not null) photo.ThumbnailUrl = req.FileUrl;
        if (req.Category is not null) photo.Category = req.Category;
        if (req.PhotoType is not null) photo.PhotoType = req.PhotoType;
        if (req.Stage is not null) photo.Stage = req.Stage;
        if (req.Notes is not null) photo.Notes = req.Notes;
        if (req.PhotoDate is not null) photo.PhotoDate = DateOnly.Parse(req.PhotoDate);

        await db.SaveChangesAsync();
        return Ok(new { photo.Id, message = "تم تحديث الصورة" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id)
    {
        var photo = await db.ClinicalPhotos.FindAsync(id);
        if (photo is null) return NotFound();
        db.ClinicalPhotos.Remove(photo);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الصورة" });
    }
}

[ApiController]
[Route("api/radiographs")]
[Authorize]
public class RadiographsController(AppDbContext db) : ControllerBase
{
    [HttpGet("{patientId:guid}")]
    public async Task<IActionResult> GetRadiographs(Guid patientId)
    {
        var xrays = await db.Radiographs
            .Include(r => r.Doctor)
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.XrayDate)
            .Select(r => new {
                r.Id, r.XrayType, r.FileUrl, r.ToothRelated, r.Notes,
                XrayDate = r.XrayDate.ToString("yyyy-MM-dd"),
                DoctorName = r.Doctor != null ? r.Doctor.Name : null,
            })
            .ToListAsync();
        return Ok(xrays);
    }

    [HttpPost]
    public async Task<IActionResult> AddRadiograph([FromBody] AddRadiographRequest req)
    {
        var xray = new Radiograph
        {
            PatientId   = req.PatientId,
            FileUrl     = req.FileUrl,
            XrayType    = req.XrayType,
            ToothRelated= req.ToothRelated,
            Notes       = req.Notes,
            DoctorId    = req.DoctorId,
            XrayDate    = req.XrayDate != null ? DateOnly.Parse(req.XrayDate) : DateOnly.FromDateTime(DateTime.Today),
        };
        db.Radiographs.Add(xray);
        await db.SaveChangesAsync();
        return Ok(new { xray.Id, message = "تم إضافة الأشعة" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRadiograph(Guid id)
    {
        var xray = await db.Radiographs.FindAsync(id);
        if (xray is null) return NotFound();
        db.Radiographs.Remove(xray);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم الحذف" });
    }
}
