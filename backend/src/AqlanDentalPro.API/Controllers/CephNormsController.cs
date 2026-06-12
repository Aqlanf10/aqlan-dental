using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Configurable cephalometric norms (normal value ± SD per measurement per
/// analysis group). Staff can read; only admins can modify or reset.
/// CephService reads these at compute time with hardcoded fallbacks.
/// </summary>
[ApiController]
[Route("api/ceph-norms")]
public class CephNormsController(AppDbContext db) : ControllerBase
{
    private const int InterpretationMaxLength = 300;

    // GET /api/ceph-norms
    // GET /api/ceph-norms?analysisGroup=steiner
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> List([FromQuery] string? analysisGroup)
    {
        var query = db.CephNorms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(analysisGroup))
        {
            var group = analysisGroup.Trim().ToLowerInvariant();
            query = query.Where(n => n.AnalysisGroup == group);
        }

        var result = await query
            .OrderBy(n => n.AnalysisGroup)
            .ThenBy(n => n.SortOrder)
            .ThenBy(n => n.MeasurementName)
            .Select(n => MapDto(n))
            .ToListAsync();

        return Ok(result);
    }

    // PUT /api/ceph-norms/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCephNormRequest req)
    {
        if (req.StdDeviation <= 0)
            return BadRequest(new { message = "الانحراف المعياري يجب أن يكون أكبر من صفر" });

        if (req.MinNormal.HasValue && req.MaxNormal.HasValue && req.MinNormal.Value > req.MaxNormal.Value)
            return BadRequest(new { message = "الحد الأدنى للمدى الطبيعي يجب أن يكون أقل من أو يساوي الحد الأقصى" });

        if ((req.MinNormal.HasValue && req.NormalValue < req.MinNormal.Value) ||
            (req.MaxNormal.HasValue && req.NormalValue > req.MaxNormal.Value))
            return BadRequest(new { message = "القيمة الطبيعية يجب أن تقع ضمن المدى الطبيعي المحدد" });

        if ((req.InterpretationBelow?.Length ?? 0) > InterpretationMaxLength ||
            (req.InterpretationNormal?.Length ?? 0) > InterpretationMaxLength ||
            (req.InterpretationAbove?.Length ?? 0) > InterpretationMaxLength)
            return BadRequest(new { message = $"نص التفسير يجب ألا يتجاوز {InterpretationMaxLength} حرف" });

        var norm = await db.CephNorms.FirstOrDefaultAsync(n => n.Id == id);
        if (norm is null)
            return NotFound(new { message = "معيار القياس غير موجود" });

        norm.NormalValue          = req.NormalValue;
        norm.StdDeviation         = req.StdDeviation;
        norm.MinNormal            = req.MinNormal;
        norm.MaxNormal            = req.MaxNormal;
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
        SortOrder            = n.SortOrder
    };
}
