using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateRolePermissionRequest
{
    public string Role { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public bool CanView { get; init; } = false;
    public bool CanCreate { get; init; } = false;
    public bool CanEdit { get; init; } = false;
    public bool CanDelete { get; init; } = false;
    public bool CanExport { get; init; } = false;
    public bool CanApprove { get; init; } = false;
}

public sealed class CreateRolePermissionRequestValidator : AbstractValidator<CreateRolePermissionRequest>
{
    public CreateRolePermissionRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب")
            .MaximumLength(100).WithMessage("الدور يجب ألا يتجاوز 100 حرف");
        RuleFor(x => x.Resource)
            .NotEmpty().WithMessage("المورد مطلوب")
            .MaximumLength(200).WithMessage("المورد يجب ألا يتجاوز 200 حرف");
    }
}

public sealed class UpdateRolePermissionRequest
{
    public bool? CanView { get; init; }
    public bool? CanCreate { get; init; }
    public bool? CanEdit { get; init; }
    public bool? CanDelete { get; init; }
    public bool? CanExport { get; init; }
    public bool? CanApprove { get; init; }
}

public sealed class BulkUpdateRolePermissionItem
{
    public Guid Id { get; init; }
    public bool? CanView { get; init; }
    public bool? CanCreate { get; init; }
    public bool? CanEdit { get; init; }
    public bool? CanDelete { get; init; }
    public bool? CanExport { get; init; }
    public bool? CanApprove { get; init; }
}

public sealed class BulkUpdateRolePermissionRequest
{
    public List<BulkUpdateRolePermissionItem> Items { get; init; } = [];
}

public sealed class BulkUpdateRolePermissionRequestValidator : AbstractValidator<BulkUpdateRolePermissionRequest>
{
    public BulkUpdateRolePermissionRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("قائمة الصلاحيات مطلوبة");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).NotEmpty().WithMessage("معرف الصلاحية مطلوب");
        });
    }
}

[ApiController]
[Route("api/role-permissions")]
[Authorize(Policy = "AdminOnly")]
public class RolePermissionsController(AppDbContext db) : ControllerBase
{
    // GET /api/role-permissions
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role = null)
    {
        var query = db.RolePermissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(rp => rp.Role == role);

        var permissions = await query
            .OrderBy(rp => rp.Role).ThenBy(rp => rp.Resource)
            .Select(rp => new
            {
                rp.Id,
                rp.Role,
                rp.Resource,
                rp.CanView,
                rp.CanCreate,
                rp.CanEdit,
                rp.CanDelete,
                rp.CanExport,
                rp.CanApprove,
            })
            .ToListAsync();

        return Ok(permissions);
    }

    // POST /api/role-permissions
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRolePermissionRequest req)
    {
        // Check if this role+resource combination already exists
        if (await db.RolePermissions.AnyAsync(rp => rp.Role == req.Role && rp.Resource == req.Resource))
            return Conflict(new { message = "صلاحية هذا الدور والمورد موجودة بالفعل" });

        var permission = new RolePermission
        {
            Role = req.Role,
            Resource = req.Resource,
            CanView = req.CanView,
            CanCreate = req.CanCreate,
            CanEdit = req.CanEdit,
            CanDelete = req.CanDelete,
            CanExport = req.CanExport,
            CanApprove = req.CanApprove,
        };

        db.RolePermissions.Add(permission);
        await db.SaveChangesAsync();

        return Created($"/api/role-permissions/{permission.Id}", new
        {
            permission.Id,
            permission.Role,
            permission.Resource,
            permission.CanView,
            permission.CanCreate,
            permission.CanEdit,
            permission.CanDelete,
            permission.CanExport,
            permission.CanApprove,
        });
    }

    // PUT /api/role-permissions/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRolePermissionRequest req)
    {
        var permission = await db.RolePermissions.FindAsync(id);
        if (permission is null) return NotFound(new { message = "الصلاحية غير موجودة" });

        if (req.CanView.HasValue) permission.CanView = req.CanView.Value;
        if (req.CanCreate.HasValue) permission.CanCreate = req.CanCreate.Value;
        if (req.CanEdit.HasValue) permission.CanEdit = req.CanEdit.Value;
        if (req.CanDelete.HasValue) permission.CanDelete = req.CanDelete.Value;
        if (req.CanExport.HasValue) permission.CanExport = req.CanExport.Value;
        if (req.CanApprove.HasValue) permission.CanApprove = req.CanApprove.Value;

        await db.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الصلاحية بنجاح" });
    }

    // PUT /api/role-permissions/bulk
    [HttpPut("bulk")]
    public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRolePermissionRequest req)
    {
        var ids = req.Items.Select(i => i.Id).ToList();
        var permissions = await db.RolePermissions
            .Where(rp => ids.Contains(rp.Id))
            .ToListAsync();

        var foundIds = permissions.Select(p => p.Id).ToHashSet();
        var missingIds = ids.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
            return NotFound(new { message = "بعض الصلاحيات غير موجودة", missingIds });

        foreach (var item in req.Items)
        {
            var permission = permissions.First(p => p.Id == item.Id);
            if (item.CanView.HasValue) permission.CanView = item.CanView.Value;
            if (item.CanCreate.HasValue) permission.CanCreate = item.CanCreate.Value;
            if (item.CanEdit.HasValue) permission.CanEdit = item.CanEdit.Value;
            if (item.CanDelete.HasValue) permission.CanDelete = item.CanDelete.Value;
            if (item.CanExport.HasValue) permission.CanExport = item.CanExport.Value;
            if (item.CanApprove.HasValue) permission.CanApprove = item.CanApprove.Value;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الصلاحيات بنجاح", count = req.Items.Count });
    }

    // DELETE /api/role-permissions/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var permission = await db.RolePermissions.FindAsync(id);
        if (permission is null) return NotFound(new { message = "الصلاحية غير موجودة" });

        db.RolePermissions.Remove(permission);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الصلاحية بنجاح" });
    }
}
