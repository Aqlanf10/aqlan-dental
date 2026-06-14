using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Saves facial photo analyses (profile/frontal) against an orthodontic case.
/// Measurements/landmarks are stored as JSON exactly as computed on the
/// frontend (honest geometry — no AI). The DoctorId stored references
/// <c>Doctors.Id</c>, resolved from the current user via <c>Doctors.UserId</c>.
/// </summary>
public class PhotoAnalysisService(AppDbContext db, ICurrentUserService currentUser)
{
    private static readonly HashSet<string> ViewTypes = new(StringComparer.OrdinalIgnoreCase) { "profile", "frontal" };

    public async Task<List<PhotoAnalysisListItemDto>> ListAsync(Guid orthoCaseId)
    {
        return await db.PhotoAnalyses
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PhotoAnalysisListItemDto
            {
                Id = p.Id,
                OrthoCaseId = p.OrthoCaseId,
                ViewType = p.ViewType,
                ImageFileUrl = p.ImageFileUrl,
                AnalysisDate = p.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();
    }

    public async Task<PhotoAnalysisDetailDto?> GetByIdAsync(Guid id)
    {
        var p = await db.PhotoAnalyses.FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? null : Map(p);
    }

    public async Task<(PhotoAnalysisDetailDto? Result, string? Error)> CreateAsync(SavePhotoAnalysisRequest req)
    {
        if (!ViewTypes.Contains(req.ViewType))
            return (null, "نوع الصورة غير صالح");

        var caseExists = await db.OrthoCases.AnyAsync(c => c.Id == req.OrthoCaseId && c.IsActive);
        if (!caseExists)
            return (null, "حالة التقويم غير موجودة");

        // DoctorId references Doctors.Id, NOT Users.Id — resolve via Doctors.UserId.
        Guid? doctorId = null;
        if (currentUser.UserId is Guid userId)
            doctorId = await db.Doctors
                .Where(d => d.UserId == userId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();

        var entity = new PhotoAnalysis
        {
            OrthoCaseId = req.OrthoCaseId,
            ViewType = req.ViewType.ToLowerInvariant(),
            ImageFileUrl = req.ImageFileUrl,
            LandmarksJson = req.LandmarksJson,
            MeasurementsJson = req.MeasurementsJson,
            Notes = req.Notes,
            DoctorId = doctorId,
        };

        db.PhotoAnalyses.Add(entity);
        await db.SaveChangesAsync();
        return (Map(entity), null);
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var entity = await db.PhotoAnalyses.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return false;
        entity.IsActive = false;
        await db.SaveChangesAsync();
        return true;
    }

    private static PhotoAnalysisDetailDto Map(PhotoAnalysis p) => new()
    {
        Id = p.Id,
        OrthoCaseId = p.OrthoCaseId,
        ViewType = p.ViewType,
        ImageFileUrl = p.ImageFileUrl,
        LandmarksJson = p.LandmarksJson,
        MeasurementsJson = p.MeasurementsJson,
        Notes = p.Notes,
        AnalysisDate = p.CreatedAt.ToString("yyyy-MM-dd"),
    };
}
