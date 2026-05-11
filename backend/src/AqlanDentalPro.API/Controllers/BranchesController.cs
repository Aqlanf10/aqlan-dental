using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateBranchRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool IsMain { get; init; } = false;
}

public sealed class UpdateBranchRequest
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool? IsMain { get; init; }
}

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController(
    AppDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var query = db.Branches.IgnoreQueryFilters().AsQueryable();

        query = status?.ToLower() switch
        {
            "active" => query.Where(b => b.IsActive),
            "inactive" => query.Where(b => !b.IsActive),
            _ => query
        };

        var branches = await query
            .OrderByDescending(b => b.IsMain)
            .ThenBy(b => b.Name)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.IsMain,
                b.IsActive,
                DoctorsCount = db.Doctors.Count(d => d.BranchId == b.Id),
                PatientsCount = db.Patients.Count(p => p.BranchId == b.Id)
            })
            .ToListAsync();

        return Ok(branches);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var branch = await db.Branches.IgnoreQueryFilters()
            .Where(b => b.Id == id)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.IsMain,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                DoctorsCount = db.Doctors.Count(d => d.BranchId == b.Id),
                PatientsCount = db.Patients.Count(p => p.BranchId == b.Id)
            })
            .FirstOrDefaultAsync();

        if (branch is null)
            return NotFound(new { message = "الفرع غير موجود" });

        return Ok(branch);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الفرع مطلوب" });

        if (req.IsMain)
        {
            var hasMain = await db.Branches.AnyAsync(b => b.IsMain);
            if (hasMain)
                return BadRequest(new { message = "لا يمكن أن يكون هناك أكثر من فرع رئيسي واحد" });

            // Unset IsMain on all other branches (safety measure)
            var existingMain = await db.Branches.Where(b => b.IsMain).ToListAsync();
            foreach (var b in existingMain)
                b.IsMain = false;
        }

        var branch = new Branch
        {
            Name = req.Name.Trim(),
            Address = req.Address?.Trim(),
            Phone = req.Phone?.Trim(),
            IsMain = req.IsMain
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        return Created($"/api/branches/{branch.Id}", new
        {
            branch.Id,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.IsMain,
            branch.IsActive
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest req)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch is null)
            return NotFound(new { message = "الفرع غير موجود" });

        if (req.Name is not null && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الفرع مطلوب" });

        if (req.Name is not null)
            branch.Name = req.Name.Trim();

        if (req.Address is not null)
            branch.Address = req.Address.Trim();

        if (req.Phone is not null)
            branch.Phone = req.Phone.Trim();

        if (req.IsMain.HasValue && req.IsMain.Value)
        {
            if (!branch.IsMain)
            {
                var existingMain = await db.Branches.Where(b => b.IsMain && b.Id != id).ToListAsync();
                foreach (var b in existingMain)
                    b.IsMain = false;

                branch.IsMain = true;
            }
        }
        else if (req.IsMain.HasValue && !req.IsMain.Value && branch.IsMain)
        {
            branch.IsMain = false;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            branch.Id,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.IsMain,
            branch.IsActive
        });
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch is null)
            return NotFound(new { message = "الفرع غير موجود" });

        if (branch.IsActive)
        {
            // Deactivating — check constraints
            if (branch.IsMain)
                return BadRequest(new { message = "لا يمكن تعطيل الفرع الرئيسي. قم بتعيين فرع رئيسي آخر أولاً" });

            var activeCount = await db.Branches.CountAsync(b => b.IsActive);
            if (activeCount <= 1)
                return BadRequest(new { message = "لا يمكن تعطيل الفروع النشطة الوحيدة. يجب أن يكون هناك فرع نشط واحد على الأقل" });
        }

        branch.IsActive = !branch.IsActive;
        await db.SaveChangesAsync();

        return Ok(new { message = branch.IsActive ? "تم تفعيل الفرع بنجاح" : "تم تعطيل الفرع بنجاح" });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var branch = await db.Branches.FindAsync(id);
        if (branch is null)
            return NotFound(new { message = "الفرع غير موجود" });

        if (branch.IsMain)
            return BadRequest(new { message = "لا يمكن حذف الفرع الرئيسي. قم بتعيين فرع رئيسي آخر أولاً" });

        var activeCount = await db.Branches.CountAsync(b => b.IsActive);
        if (branch.IsActive && activeCount <= 1)
            return BadRequest(new { message = "لا يمكن حذف جميع الفروع النشطة. يجب أن يكون هناك فرع نشط واحد على الأقل" });

        branch.IsActive = false;
        branch.DeletedAt = DateTime.UtcNow;
        branch.DeletedBy = currentUser.UserId;

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الفرع بنجاح" });
    }
}
