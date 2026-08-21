using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Configurable cephalometric norms (normal value ± SD per measurement per
/// analysis group, optionally stratified by patient age band and sex — CLIN-10).
/// Staff can read; only admins can create, modify, or reset. CephService reads
/// these at compute time with hardcoded fallbacks.
/// </summary>
[ApiController]
[Route("api/ceph-norms")]
// CORE-P1-S4 — deny by default. Actions are StaffOnly (read) and AdminOnly (write); the
// two combine, so the stricter writes stay Admin-only. Reference norms are not patient data,
// which is why the default is StaffOnly rather than AdminOnly.
[Authorize(Policy = "StaffOnly")]
public class CephNormsController(AppDbContext db) : ControllerBase
{
    private const int InterpretationMaxLength = 300;
    private const int SexMaxLength = 1;

    // GET /api/ceph-norms
    // GET /api/ceph-norms?analysisGroup=steiner
    // GET /api/ceph-norms?analysisGroup=steiner&age=10&sex=M   (CLIN-10)
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> List(
        [FromQuery] string? analysisGroup,
        [FromQuery] int? age,
        [FromQuery] string? sex)
    {
        var normalizedSex = NormalizeSex(sex);
        if (sex is not null && normalizedSex is null)
            return BadRequest(new { message = "الجنس يجب أن يكون «M» أو «F» فقط" });

        var query = db.CephNorms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(analysisGroup))
        {
            var group = analysisGroup.Trim().ToLowerInvariant();
            query = query.Where(n => n.AnalysisGroup == group);
        }

        // CLIN-10 — optional age/sex filter narrows to rows that could apply
        // to a patient of that age/sex (age band contains age when banded, OR
        // un-banded row; sex matches when sex-null or equal). The frontend
        // settings table uses this to show "applicable norms for this patient".
        if (age.HasValue)
        {
            int ageVal = age.Value;
            query = query.Where(n =>
                (!n.AgeMin.HasValue && !n.AgeMax.HasValue) // un-stratified fallback always applies
                || ((!n.AgeMin.HasValue || n.AgeMin.Value <= ageVal)
                    && (!n.AgeMax.HasValue || n.AgeMax.Value >= ageVal)));
        }
        if (normalizedSex is not null)
        {
            query = query.Where(n => n.Sex == null || n.Sex == normalizedSex);
        }

        var result = await query
            .OrderBy(n => n.AnalysisGroup)
            .ThenBy(n => n.SortOrder)
            .ThenBy(n => n.MeasurementName)
            .Select(n => MapDto(n))
            .ToListAsync();

        return Ok(result);
    }

    // GET /api/ceph-norms/best?measurementName=SNA&analysisGroup=steiner&age=10&sex=M
    // CLIN-10 — returns the single best-matching norm for the given patient
    // age/sex using the same priority tiers as CephService.FindBestCephNorm:
    // sex-specific+age-matched > sex-null+age-matched > un-stratified > 404.
    [HttpGet("best")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Best(
        [FromQuery] string? measurementName,
        [FromQuery] string? analysisGroup,
        [FromQuery] int? age,
        [FromQuery] string? sex)
    {
        if (string.IsNullOrWhiteSpace(measurementName))
            return BadRequest(new { message = "اسم القياس مطلوب" });
        if (string.IsNullOrWhiteSpace(analysisGroup))
            return BadRequest(new { message = "مجموعة التحليل مطلوبة" });

        var normalizedSex = NormalizeSex(sex);
        if (sex is not null && normalizedSex is null)
            return BadRequest(new { message = "الجنس يجب أن يكون «M» أو «F» فقط" });

        if (age.HasValue && (age.Value < 0 || age.Value > 130))
            return BadRequest(new { message = "العمر خارج النطاق المقبول" });

        var norms = await db.CephNorms.AsNoTracking().ToListAsync();
        var best = CephService.FindBestCephNorm(
            norms,
            measurementName.Trim(),
            analysisGroup.Trim().ToLowerInvariant(),
            age,
            normalizedSex);

        if (best is null)
            return NotFound(new { message = "لا يوجد معيار مطابق للعمر/الجنس المحدد" });

        return Ok(MapDto(best));
    }

    // POST /api/ceph-norms
    // CLIN-10 — create a new configurable norm (admin only). Used to add
    // population-specific norms for an age/sex stratum not covered by the
    // factory defaults.
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateCephNormRequest req)
    {
        var validationError = ValidateCommon(req.MeasurementName, req.AnalysisGroup, req.StdDeviation,
            req.MinNormal, req.MaxNormal, req.NormalValue, req.Sex,
            req.InterpretationBelow, req.InterpretationNormal, req.InterpretationAbove);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var norm = new CephNorm
        {
            MeasurementName      = req.MeasurementName.Trim(),
            NameAr               = string.IsNullOrWhiteSpace(req.NameAr) ? null : req.NameAr.Trim(),
            AnalysisGroup        = req.AnalysisGroup.Trim().ToLowerInvariant(),
            NormalValue          = req.NormalValue,
            StdDeviation         = req.StdDeviation,
            MinNormal            = req.MinNormal,
            MaxNormal            = req.MaxNormal,
            Unit                 = string.IsNullOrWhiteSpace(req.Unit) ? "°" : req.Unit.Trim(),
            Category             = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim(),
            InterpretationBelow  = string.IsNullOrWhiteSpace(req.InterpretationBelow) ? null : req.InterpretationBelow.Trim(),
            InterpretationNormal = string.IsNullOrWhiteSpace(req.InterpretationNormal) ? null : req.InterpretationNormal.Trim(),
            InterpretationAbove  = string.IsNullOrWhiteSpace(req.InterpretationAbove) ? null : req.InterpretationAbove.Trim(),
            SortOrder            = req.SortOrder,
            AgeMin               = req.AgeMin,
            AgeMax               = req.AgeMax,
            Sex                  = NormalizeSex(req.Sex),
            IsActive             = true,
        };

        db.CephNorms.Add(norm);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = norm.Id }, new { message = "تم إنشاء معيار القياس بنجاح", norm = MapDto(norm) });
    }

    // GET /api/ceph-norms/{id} — single-norm lookup used by CreatedAtAction.
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var norm = await db.CephNorms.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
        if (norm is null)
            return NotFound(new { message = "معيار القياس غير موجود" });
        return Ok(MapDto(norm));
    }

    // PUT /api/ceph-norms/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCephNormRequest req)
    {
        var validationError = ValidateUpdate(req.StdDeviation, req.MinNormal, req.MaxNormal,
            req.NormalValue, req.Sex, req.AgeMin, req.AgeMax,
            req.InterpretationBelow, req.InterpretationNormal, req.InterpretationAbove);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var norm = await db.CephNorms.FirstOrDefaultAsync(n => n.Id == id);
        if (norm is null)
            return NotFound(new { message = "معيار القياس غير موجود" });

        norm.NormalValue          = req.NormalValue;
        norm.StdDeviation         = req.StdDeviation;
        norm.MinNormal            = req.MinNormal;
        norm.MaxNormal            = req.MaxNormal;
        norm.AgeMin               = req.AgeMin;
        norm.AgeMax               = req.AgeMax;
        norm.Sex                  = NormalizeSex(req.Sex);
        norm.InterpretationBelow  = string.IsNullOrWhiteSpace(req.InterpretationBelow) ? null : req.InterpretationBelow.Trim();
        norm.InterpretationNormal = string.IsNullOrWhiteSpace(req.InterpretationNormal) ? null : req.InterpretationNormal.Trim();
        norm.InterpretationAbove  = string.IsNullOrWhiteSpace(req.InterpretationAbove) ? null : req.InterpretationAbove.Trim();

        await db.SaveChangesAsync();

        return Ok(new { message = "تم تحديث معيار القياس بنجاح", norm = MapDto(norm) });
    }

    // POST /api/ceph-norms/reset-defaults
    [HttpPost("reset-defaults")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ResetDefaults()
    {
        var count = await CephNormSeeder.ResetToFactoryDefaultsAsync(db);
        return Ok(new { message = "تمت استعادة القيم الافتراضية لمعايير القياس بنجاح", count });
    }

    private static CephNormDto MapDto(CephNorm n) => new()
    {
        Id                   = n.Id,
        MeasurementName      = n.MeasurementName,
        NameAr               = n.NameAr,
        AnalysisGroup        = n.AnalysisGroup,
        NormalValue          = n.NormalValue,
        StdDeviation         = n.StdDeviation,
        MinNormal            = n.MinNormal,
        MaxNormal            = n.MaxNormal,
        Unit                 = n.Unit,
        Category             = n.Category,
        InterpretationBelow  = n.InterpretationBelow,
        InterpretationNormal = n.InterpretationNormal,
        InterpretationAbove  = n.InterpretationAbove,
        SortOrder            = n.SortOrder,
        AgeMin               = n.AgeMin,
        AgeMax               = n.AgeMax,
        Sex                  = n.Sex,
    };

    /// <summary>
    /// Normalizes the Sex input. Accepts "M"/"m" and "F"/"f" (case-insensitive),
    /// returns null for null/empty/whitespace. Returns null (and signals
    /// rejection via the caller's null-check) for any other value.
    /// </summary>
    private static string? NormalizeSex(string? sex)
    {
        if (string.IsNullOrWhiteSpace(sex)) return null;
        var upper = sex.Trim().ToUpperInvariant();
        return upper == "M" || upper == "F" ? upper : null;
    }

    private static string? ValidateUpdate(
        decimal sd, decimal? min, decimal? max, decimal normal,
        string? sex, int? ageMin, int? ageMax,
        string? below, string? normalAr, string? above)
    {
        if (sd <= 0)
            return "الانحراف المعياري يجب أن يكون أكبر من صفر";
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            return "الحد الأدنى للمدى الطبيعي يجب أن يكون أقل من أو يساوي الحد الأقصى";
        if ((min.HasValue && normal < min.Value) || (max.HasValue && normal > max.Value))
            return "القيمة الطبيعية يجب أن تقع ضمن المدى الطبيعي المحدد";
        if (sex is not null && NormalizeSex(sex) is null)
            return "الجنس يجب أن يكون «M» أو «F» فقط";
        if (ageMin.HasValue && ageMax.HasValue && ageMin.Value > ageMax.Value)
            return "العمر الأدنى يجب أن يكون أقل من أو يساوي العمر الأقصى";
        if (ageMin.HasValue && (ageMin.Value < 0 || ageMin.Value > 130))
            return "العمر الأدنى خارج النطاق المقبول";
        if (ageMax.HasValue && (ageMax.Value < 0 || ageMax.Value > 130))
            return "العمر الأقصى خارج النطاق المقبول";
        var interpError = ValidateInterpretationLengths(below, normalAr, above);
        return interpError;
    }

    private static string? ValidateCommon(
        string measurementName, string analysisGroup, decimal sd,
        decimal? min, decimal? max, decimal normal,
        string? sex,
        string? below, string? normalAr, string? above)
    {
        if (string.IsNullOrWhiteSpace(measurementName))
            return "اسم القياس مطلوب";
        if (string.IsNullOrWhiteSpace(analysisGroup))
            return "مجموعة التحليل مطلوبة";
        if (sd <= 0)
            return "الانحراف المعياري يجب أن يكون أكبر من صفر";
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            return "الحد الأدنى للمدى الطبيعي يجب أن يكون أقل من أو يساوي الحد الأقصى";
        if ((min.HasValue && normal < min.Value) || (max.HasValue && normal > max.Value))
            return "القيمة الطبيعية يجب أن تقع ضمن المدى الطبيعي المحدد";
        if (sex is not null && NormalizeSex(sex) is null)
            return "الجنس يجب أن يكون «M» أو «F» فقط";
        return ValidateInterpretationLengths(below, normalAr, above);
    }

    private static string? ValidateInterpretationLengths(string? below, string? normalAr, string? above)
    {
        if ((below?.Length ?? 0) > InterpretationMaxLength ||
            (normalAr?.Length ?? 0) > InterpretationMaxLength ||
            (above?.Length ?? 0) > InterpretationMaxLength)
            return $"نص التفسير يجب ألا يتجاوز {InterpretationMaxLength} حرف";
        return null;
    }
}
